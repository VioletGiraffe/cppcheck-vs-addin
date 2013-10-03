using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.VCCodeModel;
using Microsoft.VisualStudio.VCProjectEngine;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.OLE;
using EnvDTE;
using System.Collections.Generic;
using System.Linq;

namespace VSPackage.CPPCheckPlugin
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [DefaultRegistryRoot(@"Software\Microsoft\VisualStudio\11.0")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // This attribute registers a tool window exposed by this package.
    [ProvideToolWindow(typeof(MyToolWindow))]
    [Guid(GuidList.guidCPPCheckPluginPkgString)]
    public sealed class CPPCheckPluginPackage : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public CPPCheckPluginPackage()
        {
        }

        /// <summary>
        /// This function is called when the user clicks the menu item that shows the 
        /// tool window. See the Initialize method to see how the menu item is associated to 
        /// this function using the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void ShowToolWindow(object sender, EventArgs e)
        {
            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = this.FindToolWindow(typeof(MyToolWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException(Resources.CanNotCreateWindow);
            }
            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }


        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            _dte = (EnvDTE.DTE)GetService(typeof(SDTE));
            _eventsHandlers = _dte.Events.DocumentEvents;
            _eventsHandlers.DocumentSaved += documentSaved;

            OutputWindow outputWindow = (OutputWindow)_dte.Application.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
            _outputWindow = outputWindow.OutputWindowPanes.Add("Code analysis output");

            _analyzers.Add(new AnalyzerCppcheck());

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidSettings);
                MenuCommand menuItem = new MenuCommand(MenuItemCallback, menuCommandID );
                mcs.AddCommand( menuItem );
                // Create the command for the tool window
                CommandID toolwndCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidMyTool);
                MenuCommand menuToolWin = new MenuCommand(ShowToolWindow, toolwndCommandID);
                mcs.AddCommand( menuToolWin );
            }
        }
        #endregion

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void MenuItemCallback(object sender, EventArgs e)
        {
        }

        private void documentSaved(Document document)
        {            
            if (document.Language == "C/C++")
            {
                try
                {
                    VCProject project = document.ProjectItem.ContainingProject.Object as VCProject;
                    String currentConfigName = document.ProjectItem.ConfigurationManager.ActiveConfiguration.ConfigurationName as String;
                    VCConfiguration config = project.Configurations.Item(currentConfigName);
                    IVCCollection toolsCollection = config.Tools;
                    SourceFile sourceForAnalysis = new SourceFile(document.FullName, project.ProjectDirectory.Replace(@"""", ""));
                    foreach (var tool in toolsCollection)
                    {
                        // Project-specific includes
                        if (tool is VCCLCompilerTool)
                        {
                            VCCLCompilerTool compilerTool = tool as VCCLCompilerTool;
                            String includes = compilerTool.AdditionalIncludeDirectories;
                            sourceForAnalysis.addIncludePath(includes.Split(';').ToList());
                            break;
                        }
                    }

                    // Global platform includes
                    VCPlatform platfrom = config.Platform as VCPlatform;
                    var globalIncludes = platfrom.IncludeDirectories.Split(';').ToList();
                    // Resolving variables in include paths
                    for (int i = 0; i < globalIncludes.Count; ++i)
                    {
                        globalIncludes[i] = platfrom.Evaluate(globalIncludes[i]);
                    }
                    sourceForAnalysis.addIncludePath(globalIncludes);

                    _outputWindow.Clear();
                    foreach (var analyzer in _analyzers)
                    {
                        analyzer.analyze(sourceForAnalysis, _outputWindow);
                    }
                }
                catch (System.Exception ex)
                {
                    if (_outputWindow != null)
                    {
                        _outputWindow.Clear();
                        _outputWindow.OutputString("Exception occurred in cppcheck add-in: " + ex.Message);
                    }
                    Debug.WriteLine("Exception occurred in cppcheck add-in: " + ex.Message);
                    return;
                }
            }
        }

        private DTE _dte = null;
        private DocumentEvents _eventsHandlers = null;
        private List<ICodeAnalyzer> _analyzers = new List<ICodeAnalyzer>();

        private static OutputWindowPane _outputWindow = null;
    }
}
