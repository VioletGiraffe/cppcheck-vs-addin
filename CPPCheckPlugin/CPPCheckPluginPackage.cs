using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
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


        #region Package Members

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
                CommandID menuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidCheckProjectCppcheck);
                MenuCommand menuItem = new MenuCommand(MenuItemCallback, menuCommandID );
                mcs.AddCommand( menuItem );
                // Create the command for the tool window
                CommandID toolwndCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidMyTool);
                MenuCommand menuToolWin = new MenuCommand(ShowToolWindow, toolwndCommandID);
                mcs.AddCommand( menuToolWin );
            }
        }
        #endregion

        private void MenuItemCallback(object sender, EventArgs e)
        {
            checkCurrentProject();
        }

        private void documentSaved(Document document)
        {            
            if (document != null && document.Language == "C/C++")
            {
                try
                {
                    VCProject project = document.ProjectItem.ContainingProject.Object as VCProject;
                    String currentConfigName = document.ProjectItem.ConfigurationManager.ActiveConfiguration.ConfigurationName as String;
                    SourceFile sourceForAnalysis = createSourceFile(document.FullName, currentConfigName, project);
                    if (sourceForAnalysis == null)
                        return;

                    _outputWindow.Clear();
                    foreach (var analyzer in _analyzers)
                    {
						analyzer.analyze(sourceForAnalysis, _outputWindow, currentConfigName.Contains("64"));
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
                }
            }
        }

        private void checkCurrentProject()
        {
			if ((_dte.ActiveSolutionProjects as Object[]).Length <= 0)
			{
				System.Windows.MessageBox.Show("No project selected in Solution Explorer - nothing to check.");
				return;
			}

			String currentConfigName = _dte.Solution.Projects.Item(1).ConfigurationManager.ActiveConfiguration.ConfigurationName as String;
			List<SourceFile> files = new List<SourceFile>();
			Object[] activeProjects = _dte.ActiveSolutionProjects as Object[];
			foreach (dynamic o in activeProjects)
			{
                VCProject project = o.Object as VCProject;
				foreach (VCFile file in project.Files)
				{
					if (file.FileType == eFileType.eFileTypeCppHeader || file.FileType == eFileType.eFileTypeCppCode || file.FileType == eFileType.eFileTypeCppClass)
					{
						if (!(file.Name.StartsWith("moc_") && file.Name.EndsWith(".cpp")) && !(file.Name.StartsWith("ui_") && file.Name.EndsWith(".h")) && !(file.Name.StartsWith("qrc_") && file.Name.EndsWith(".cpp"))) // Ignoring Qt MOC and UI files
						{
							SourceFile f = createSourceFile(file.FullPath, currentConfigName, project);
							if (f != null)
								files.Add(f);
						}
					}
				}
                break; // Only checking one project at a time for now
			}
            
            _outputWindow.Clear();
            foreach (var analyzer in _analyzers)
            {
				analyzer.analyze(files, _outputWindow, currentConfigName.Contains("64"));
            }
        }

        SourceFile createSourceFile(string filePath, string configurationName, VCProject project)
        {
            try
            {
                VCConfiguration config = project.Configurations.Item(configurationName);
                IVCCollection toolsCollection = config.Tools;
                SourceFile sourceForAnalysis = new SourceFile(filePath, project.ProjectDirectory.Replace(@"""", ""));
                foreach (var tool in toolsCollection)
                {
                    // Project-specific includes
                    if (tool is VCCLCompilerTool)
                    {
                        VCCLCompilerTool compilerTool = tool as VCCLCompilerTool;
                        String includes = compilerTool.AdditionalIncludeDirectories;
                        sourceForAnalysis.addIncludePaths(includes.Split(';').ToList());
                        sourceForAnalysis.addMacros(compilerTool.PreprocessorDefinitions.Split(';').ToList());
                        break;
                    }
                }
                return sourceForAnalysis;
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine("Exception occurred in cppcheck add-in: " + ex.Message);
                return null;
            }
        }

        private DTE _dte = null;
        private DocumentEvents _eventsHandlers = null;
        private List<ICodeAnalyzer> _analyzers = new List<ICodeAnalyzer>();

        private static OutputWindowPane _outputWindow = null;
    }
}
