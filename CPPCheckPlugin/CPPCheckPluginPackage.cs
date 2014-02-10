using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
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

			{
				var outputWindow = (OutputWindow)_dte.GetOutputWindow().Object;
				_fileAnalysisOutputPane = outputWindow.OutputWindowPanes.Add("[cppcheck] File analysis output");
			}

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
				dynamic project = document.ProjectItem.ContainingProject.Object;
				var currentConfig = document.ProjectItem.ConfigurationManager.ActiveConfiguration;
				SourceFile sourceForAnalysis = createSourceFile(document.FullName, currentConfig, project);
				if (sourceForAnalysis == null)
					return;

				runAnalysis(sourceForAnalysis, currentConfig, false, _fileAnalysisOutputPane);
			}
			catch (System.Exception ex)
			{
				if (_fileAnalysisOutputPane != null)
				{
					_fileAnalysisOutputPane.Clear();
					_fileAnalysisOutputPane.OutputString("Exception occurred in cppcheck add-in: " + ex.Message);
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
				dynamic project = o.Object;
				Type projectObjectType = project.GetType();
				var projectInterface = projectObjectType.GetInterface("Microsoft.VisualStudio.VCProjectEngine.VCProject");
				if (projectInterface == null)
				{
					System.Windows.MessageBox.Show("Only C++ projects can be checked.");
					return;
				}
				foreach (dynamic file in project.Files)
				{
					Type fileObjectType = file.GetType();
					// Automatic property binding fails with VS2013 for some unknown reason, using Reflection directly instead.
					var vcFileInterface = fileObjectType.GetInterface("Microsoft.VisualStudio.VCProjectEngine.VCFile");
					var fileType = vcFileInterface.GetProperty("FileType").GetValue(file);
					Type fileTypeEnumType = fileType.GetType();
					var fileTypeEnumConstant = Enum.GetName(fileTypeEnumType, fileType);
					if (fileTypeEnumConstant == "eFileTypeCppCode") // Only checking cpp files (performance)
					{
						String fileName = file.Name;
						// Ignoring Qt MOC and UI files
						if (!(fileName.StartsWith("moc_") && fileName.EndsWith(".cpp")) && !(fileName.StartsWith("ui_") && fileName.EndsWith(".h")) && !(fileName.StartsWith("qrc_") && fileName.EndsWith(".cpp")))
						{
							SourceFile f = createSourceFile(file.FullPath, currentConfig, project);
							if (f != null)
								files.Add(f);
						}
					}
					
				}
				break; // Only checking one project at a time for now
			}

			if (_projectAnalysisOutputPane == null)
			{
				var outputWindow = (OutputWindow)_dte.GetOutputWindow().Object;
				_projectAnalysisOutputPane = outputWindow.OutputWindowPanes.Add("[cppcheck] Project analysis output");
			}

			runAnalysis(files, currentConfig, true, _projectAnalysisOutputPane);
		}

		private void runAnalysis(SourceFile file, Configuration currentConfig, bool bringOutputToFrontAfterAnalysis, OutputWindowPane outputPane)
		{
			var list = new List<SourceFile>();
			list.Add(file);
			runAnalysis(list, currentConfig, bringOutputToFrontAfterAnalysis, outputPane);
		}

		private void runAnalysis(List<SourceFile> files, Configuration currentConfig, bool bringOutputToFrontAfterAnalysis, OutputWindowPane outputPane)
		{
			Debug.Assert(outputPane != null);
			outputPane.Clear();
			var currentConfigName = currentConfig.ConfigurationName;
			foreach (var analyzer in _analyzers)
			{
				analyzer.analyze(files, outputPane, currentConfigName.Contains("64"), currentConfigName.ToLower().Contains("debug"), bringOutputToFrontAfterAnalysis);
			}
		}

		SourceFile createSourceFile(string filePath, Configuration targetConfig, dynamic project)
		{
			try
			{
				var configurationName = targetConfig.ConfigurationName;
				dynamic config = project.Configurations.Item(configurationName);
				SourceFile sourceForAnalysis = new SourceFile(filePath, project.ProjectDirectory.Replace(@"""", ""));
				dynamic toolsCollection = config.Tools;
				foreach (var tool in toolsCollection)
				{
					// Project-specific includes
					Type toolType = tool.GetType();
					var compilerToolInterface = toolType.GetInterface("Microsoft.VisualStudio.VCProjectEngine.VCCLCompilerTool");
					if (compilerToolInterface != null)
					{
						String includes = tool.AdditionalIncludeDirectories;
						String definitions = tool.PreprocessorDefinitions;
						sourceForAnalysis.addIncludePaths(includes.Split(';').ToList());
						sourceForAnalysis.addMacros(definitions.Split(';').ToList());
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

		private static OutputWindowPane _fileAnalysisOutputPane = null, _projectAnalysisOutputPane = null;
	}
}
