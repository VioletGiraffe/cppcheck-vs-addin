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
            _outputWindow = outputWindow.OutputWindowPanes.Add("cppcheck output");

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
                    List<string> includePaths = null;
                    foreach (var tool in toolsCollection)
                    {
                        // Project-specific includes
                        if (tool is VCCLCompilerTool)
                        {
                            VCCLCompilerTool compilerTool = tool as VCCLCompilerTool;
                            String includes = compilerTool.AdditionalIncludeDirectories;
                            includePaths = includes.Split(';').ToList();
                            break;
                        }
                    }

                    // Global platform includes
                    VCPlatform platfrom = config.Platform as VCPlatform;
                    includePaths.AddRange(platfrom.IncludeDirectories.Split(';').ToList());

                    String cppheckargs = @"--enable=style,information --template=vs --check-config";
                    String basePath = project.ProjectDirectory.Replace(@"""", "");
                    if (includePaths != null)
                        foreach (string path in includePaths)
                        {
                            if (!String.IsNullOrEmpty(path))
                            {
                                String fullIncludePath = path.Contains(':') ? path : (basePath + path);
                                if (fullIncludePath.EndsWith("\\"))
                                    fullIncludePath = fullIncludePath.Substring(0, fullIncludePath.Length - 1);
                                String includeArgument = @" -I""" + fullIncludePath.Replace("\"", "") + @"""";
                                cppheckargs += (" " + includeArgument);
                            }
                        }
                    runAnalyzer("c:\\Program Files (x86)\\Cppcheck\\cppcheck.exe", cppheckargs + @" """ + document.FullName + @"""");
                }
                catch (System.Exception ex)
                {
                    return;
                }
            }
        }

        private void runAnalyzer(String analyzerExePath, String parameters)
        {
            if (_outputWindow != null)
                _outputWindow.Clear();

            System.Diagnostics.Process process;
            process = new System.Diagnostics.Process();
            process.StartInfo.FileName = analyzerExePath;
            process.StartInfo.Arguments = parameters;
            process.StartInfo.CreateNoWindow = true;

            // Set UseShellExecute to false for output redirection.
            process.StartInfo.UseShellExecute = false;

            // Redirect the standard output of the command.   
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            // Set our event handler to asynchronously read the sort output.
            process.OutputDataReceived += new DataReceivedEventHandler(analyzerOutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(analyzerOutputHandler);

            // Start the process.
            process.Start();

            // Start the asynchronous read of the sort output stream.
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            // Wait for analysis completion
            process.WaitForExit();
            string o = process.StandardOutput.ReadToEnd();
            string e = process.StandardError.ReadToEnd();
            int retCode = process.ExitCode;
            process.Close();
        }

        private static void analyzerOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                String output = outLine.Data;
                if (_outputWindow != null)
                    _outputWindow.OutputString(output + "\n");
            }
        }

        private string SafeGetPropertyValue(Property prop)
        {
            try
            {
                return string.Format("{0} = {1}", prop.Name, prop.Value);
            }
            catch (Exception ex)
            {
                return string.Format("{0} = {1}", prop.Name, ex.GetType());
            }
        }

        private DTE _dte = null;
        private DocumentEvents _eventsHandlers = null;

        private static OutputWindowPane _outputWindow = null;
    }
}
