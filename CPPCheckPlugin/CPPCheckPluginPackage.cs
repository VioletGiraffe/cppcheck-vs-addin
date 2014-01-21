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
	[Guid(GuidList.guidCPPCheckPluginPkgString)]
	public sealed class CPPCheckPluginPackage : Package
	{
		public CPPCheckPluginPackage()
		{
		}

		#region Package Members

		protected override void Initialize()
		{
			Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
			base.Initialize();

			_dte = (EnvDTE.DTE)GetService(typeof(SDTE));
			_eventsHandlers = _dte.Events.DocumentEvents;
			_eventsHandlers.DocumentSaved += documentSaved;

			OutputWindow outputWindow = (OutputWindow)_dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
			_outputWindow = outputWindow.OutputWindowPanes.Add("Code analysis output");

			_analyzers.Add(new AnalyzerCppcheck());

			if (String.IsNullOrEmpty(Properties.Settings.Default.DefaultArguments))
				Properties.Settings.Default["DefaultArguments"] = CppcheckSettings.DefaultArguments;

			// Add our command handlers for menu (commands must exist in the .vsct file)
			OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
			if ( null != mcs )
			{
				// Create the command for the menu item.
				CommandID menuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidCheckProjectCppcheck);
				MenuCommand menuItem = new MenuCommand(onCheckCurrentProjectRequested, menuCommandID);
				mcs.AddCommand( menuItem );
				// Create the command for the settings window
				CommandID settingsWndCmdId = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidSettings);
				MenuCommand menuSettings = new MenuCommand(onSettingsWindowRequested, settingsWndCmdId);
				mcs.AddCommand(menuSettings);
			}


		}
		#endregion

		private void onCheckCurrentProjectRequested(object sender, EventArgs e)
		{
			checkCurrentProject();
		}

		private void onSettingsWindowRequested(object sender, EventArgs e)
		{
			CppcheckSettings settings = new CppcheckSettings();
			settings.Show();
		}

		private void documentSaved(Document document)
		{
			if (document == null || document.Language != "C/C++")
				return;

			try
			{
				VCProject project = (VCProject)document.ProjectItem.ContainingProject.Object;
				var currentConfig = document.ProjectItem.ConfigurationManager.ActiveConfiguration;
				SourceFile sourceForAnalysis = createSourceFile(document.FullName, currentConfig, project);
				if (sourceForAnalysis == null)
					return;

				runAnalysis(sourceForAnalysis, currentConfig, false);
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

		private void checkCurrentProject()
		{
			Object[] activeProjects = (Object[])_dte.ActiveSolutionProjects;
			if (!activeProjects.Any())
			{
				System.Windows.MessageBox.Show("No project selected in Solution Explorer - nothing to check.");
				return;
			}

			var currentConfig = _dte.Solution.Projects.Item(1).ConfigurationManager.ActiveConfiguration;
			List<SourceFile> files = new List<SourceFile>();
			foreach (dynamic o in activeProjects)
			{
				VCProject project = o.Object as VCProject;
				if (project == null)
				{
					System.Windows.MessageBox.Show("Only C++ projects can be checked.");
					return;
				}
				foreach (VCFile file in project.Files)
				{
					// Only checking cpp files (performance)
					if (file.FileType == eFileType.eFileTypeCppCode)
					{
						if (!(file.Name.StartsWith("moc_") && file.Name.EndsWith(".cpp")) && !(file.Name.StartsWith("ui_") && file.Name.EndsWith(".h")) && !(file.Name.StartsWith("qrc_") && file.Name.EndsWith(".cpp"))) // Ignoring Qt MOC and UI files
						{
							SourceFile f = createSourceFile(file.FullPath, currentConfig, project);
							if (f != null)
								files.Add(f);
						}
					}
				}
				break; // Only checking one project at a time for now
			}

			runAnalysis(files, currentConfig, true);
		}

		private void runAnalysis(SourceFile file, Configuration currentConfig, bool bringOutputToFrontAfterAnalysis)
		{
			var list = new List<SourceFile>();
			list.Add(file);
			runAnalysis(list, currentConfig, bringOutputToFrontAfterAnalysis);
		}

		private void runAnalysis(List<SourceFile> files, Configuration currentConfig, bool bringOutputToFrontAfterAnalysis)
		{
			_outputWindow.Clear();
			var currentConfigName = currentConfig.ConfigurationName;
			foreach (var analyzer in _analyzers)
			{
				analyzer.analyze(files, _outputWindow, currentConfigName.Contains("64"), currentConfigName.ToLower().Contains("debug"), bringOutputToFrontAfterAnalysis);
			}
		}

		SourceFile createSourceFile(string filePath, Configuration targetConfig, VCProject project)
		{
			try
			{
				var configurationName = targetConfig.ConfigurationName;
				VCConfiguration config = project.Configurations.Item(configurationName);
				SourceFile sourceForAnalysis = new SourceFile(filePath, project.ProjectDirectory.Replace(@"""", ""));
				IVCCollection toolsCollection = config.Tools;
				foreach (var tool in toolsCollection)
				{
					// Project-specific includes
					VCCLCompilerTool compilerTool = tool as VCCLCompilerTool;
					if (compilerTool != null)
					{
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
