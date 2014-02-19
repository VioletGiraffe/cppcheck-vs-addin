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
	[ProvideToolWindow(typeof(MainToolWindow), Style=VsDockStyle.Tabbed, Window=Microsoft.VisualStudio.Shell.Interop.ToolWindowGuids.Outputwindow, MultiInstances=false, Transient=false)]
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

			_fileAnalysisOutputPane = _dte.AddOutputWindowPane("[cppcheck] File analysis output");

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

		protected override void Dispose(bool disposing)
		{
			cleanup();
			base.Dispose(disposing);
		}

		protected override int QueryClose(out bool canClose)
		{
			int result = base.QueryClose(out canClose);
			if (canClose)
			{
				cleanup();
			}
			return result;
		}
		#endregion

		private void cleanup()
		{
			foreach (var item in _analyzers)
			{
				item.Dispose();
			}
		}

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
			if (document.ActiveWindow == null)
			{
				// We get here when new files are being created and added to the project and
				// then trying to obtain document.ProjectItem yields an exception. Will just skip this.
				return;
			}

			try
			{
				dynamic project = document.ProjectItem.ContainingProject.Object;
				if (!isVisualCppProject(project))
				{
					return;
				}
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

			Configuration currentConfig = null;
			List<SourceFile> files = new List<SourceFile>();
			foreach (dynamic o in activeProjects)
			{
				dynamic project = o.Object;
				if (!isVisualCppProject(project))
				{
					System.Windows.MessageBox.Show("Only C++ projects can be checked.");
					return;
				}
				currentConfig = ((Project)o).ConfigurationManager.ActiveConfiguration;
				dynamic projectFiles = project.Files;
				foreach (dynamic file in projectFiles)
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
				_projectAnalysisOutputPane = _dte.AddOutputWindowPane("[cppcheck] Project analysis output");
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
			Debug.Assert(currentConfig != null);
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
				String toolSetName = config.PlatformToolsetShortName;
				if (String.IsNullOrEmpty(toolSetName))
					toolSetName = config.PlatformToolsetFriendlyName;
				SourceFile sourceForAnalysis = new SourceFile(filePath, project.ProjectDirectory.Replace("\"", ""), toolSetName);
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
						String macrosToUndefine = tool.UndefinePreprocessorDefinitions;
						String[] includePaths = includes.Split(';');
						for (int i = 0; i < includePaths.Length; ++i)
							includePaths[i] = config.Evaluate(includePaths[i]);

						sourceForAnalysis.addIncludePaths(includePaths);
						sourceForAnalysis.addMacros(definitions.Split(';'));
						sourceForAnalysis.addMacrosToUndefine(macrosToUndefine.Split(';'));
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

		private static bool isVisualCppProject(object project)
		{
			Type projectObjectType = project.GetType();
			var projectInterface = projectObjectType.GetInterface("Microsoft.VisualStudio.VCProjectEngine.VCProject");
			return projectInterface != null;
		}

		private void showToolWindow()
		{
			try
			{
				ToolWindowPane window = FindToolWindow(typeof(MainToolWindow), 0, true);
				IVsWindowFrame frame = window.Frame as IVsWindowFrame;
				if (frame != null)
					frame.Show();
			}
			catch {}
		}

		private DTE _dte = null;
		private DocumentEvents _eventsHandlers = null;
		private List<ICodeAnalyzer> _analyzers = new List<ICodeAnalyzer>();

		private static OutputWindowPane _fileAnalysisOutputPane = null, _projectAnalysisOutputPane = null;
	}
}
