using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using VSPackage.CPPCheckPlugin.Properties;
using Microsoft.VisualStudio.VCProjectEngine;

using Task = System.Threading.Tasks.Task;
using System.Windows.Forms;


namespace VSPackage.CPPCheckPlugin
{
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[DefaultRegistryRoot(@"Software\Microsoft\VisualStudio\11.0")]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
	// This attribute is used to register the information needed to show this package
	// in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#112", "1.2.0", IconResourceID = 400)]
	// This attribute is needed to let the shell know that this package exposes some menus.
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideToolWindow(typeof(MainToolWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.Outputwindow, MultiInstances = false, Transient = false)]
	[Guid(GuidList.guidCPPCheckPluginPkgString)]
	public sealed class CPPCheckPluginPackage : AsyncPackage
	{
		private int completedFileCount = 0, lastAnalyzerTotalFiles = 0;

		public CPPCheckPluginPackage()
		{
			_instance = this;

			CreateDefaultGlobalSuppressions();
		}

		public static CPPCheckPluginPackage Instance
		{
			get { return _instance; }
		}

		public static async Task AddTextToOutputWindowAsync(string text)
		{
			try
			{
				await Instance.JoinableTaskFactory.SwitchToMainThreadAsync();

				Assumes.NotNull(Instance._outputPane);
				Instance._outputPane.OutputString(text);
			}
			catch (Exception e)
			{
				Debug.WriteLine("Exception in addTextToOutputWindow(): " + e.Message);
			}
		}

		public static string solutionName()
		{
			return _instance.JoinableTaskFactory.Run<string>(async () =>
			{
				await _instance.JoinableTaskFactory.SwitchToMainThreadAsync();
				try { return Path.GetFileNameWithoutExtension(_dte.Solution.FullName); }
				catch (Exception) { return ""; }
			});
		}

		public static string solutionPath()
		{
			return _instance.JoinableTaskFactory.Run<string>(async () =>
			{
				await _instance.JoinableTaskFactory.SwitchToMainThreadAsync();
				try { return Path.GetDirectoryName(_dte.Solution.FullName); }
				catch (Exception) { return ""; }
			});
		}

		public static async Task<string> activeProjectNameAsync()
		{
			var projects = await _instance.findSelectedCppProjectsAsync();
			Assumes.NotNull(projects);
			return projects.Any() ? (projects.First() as dynamic).Name : "";
		}

		public static async Task<string> activeProjectPathAsync()
		{
			var projects = await _instance.findSelectedCppProjectsAsync();
			Assumes.NotNull(projects);

			if (projects.Any())
			{
				string projectDirectory = (projects.First() as dynamic).ProjectDirectory;
				return projectDirectory.Replace("\"", "");
			}

			return "";
		}

		#region Package Members

		MenuCommand menuCheckCurrentProject, menuCheckCurrentProjectContext, menuCheckCurrentProjectsContext;
		MenuCommand menuShowSettingsWindow;
		MenuCommand menuCancelCheck;
		MenuCommand menuCheckSelections, checkMultiSelections;

		private void setMenuState(bool bBusy)
		{
			menuCheckCurrentProject.Enabled = !bBusy;
			menuCheckCurrentProjectContext.Enabled = !bBusy;
			menuCheckCurrentProjectsContext.Enabled = !bBusy;
			menuShowSettingsWindow.Enabled = !bBusy;
			menuCancelCheck.Enabled = bBusy;
			menuCheckSelections.Enabled = !bBusy;
			checkMultiSelections.Enabled = !bBusy;
		}

		private void CommandEvents_BeforeExecute(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
		{
			if (ID == commandEventIdSave || ID == commandEventIdSaveAll)
			{
				if (Settings.Default.CheckSavedFilesHasValue && Settings.Default.CheckSavedFiles == true)
				{
					// Stop running analysis to prevent a save dialog pop-up
					stopAnalysis();
				}
			}
		}

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			// Switches to the UI thread in order to consume some services used in command initialization
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));

			_dte = await GetServiceAsync(typeof(DTE)) as DTE;
			Assumes.Present(_dte);
			if (_dte == null)
				return;

			_eventsHandlers = _dte.Events.DocumentEvents;
			_eventsHandlers.DocumentSaved += documentSavedSync;

			_commandEventsHandlers = _dte.Events.CommandEvents;
			_commandEventsHandlers.BeforeExecute += new _dispCommandEvents_BeforeExecuteEventHandler(CommandEvents_BeforeExecute);

			var outputWindow = (OutputWindow)_dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
			_outputPane = outputWindow.OutputWindowPanes.Add("cppcheck analysis output");

			AnalyzerCppcheck cppcheckAnalayzer = new AnalyzerCppcheck();
			cppcheckAnalayzer.ProgressUpdated += checkProgressUpdated;
			_analyzers.Add(cppcheckAnalayzer);

			if (string.IsNullOrEmpty(Settings.Default.DefaultArguments))
				Settings.Default.DefaultArguments = CppcheckSettings.DefaultArguments;

			// Add our command handlers for menu (commands must exist in the .vsct file)
			OleMenuCommandService mcs = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
			if (mcs == null)
			{
				Debug.Assert(mcs != null);
				return;
			}


			// Create the command for the menu item.
			{
				CommandID menuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidCheckProjectCppcheck);
				menuCheckCurrentProject = new MenuCommand(onCheckCurrentProjectRequested, menuCommandID);
				mcs.AddCommand(menuCheckCurrentProject);
			}

			{
				// Create the command for the settings window
				CommandID settingsWndCmdId = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidSettings);
				menuShowSettingsWindow = new MenuCommand(onSettingsWindowRequested, settingsWndCmdId);
				mcs.AddCommand(menuShowSettingsWindow);
			}

			{
				CommandID stopCheckMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidStopCppcheck);
				menuCancelCheck = new MenuCommand(onStopCheckRequested, stopCheckMenuCommandID);
				mcs.AddCommand(menuCancelCheck);
			}

			{
				CommandID selectionsMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidCheckMultiItemCppcheck);
				menuCheckSelections = new MenuCommand(onCheckSelectionsRequested, selectionsMenuCommandID);
				mcs.AddCommand(menuCheckSelections);
			}

			{
				CommandID projectMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginProjectCmdSet, (int)PkgCmdIDList.cmdidCheckProjectCppcheck1);
				menuCheckCurrentProjectContext = new MenuCommand(onCheckCurrentProjectRequested, projectMenuCommandID);
				mcs.AddCommand(menuCheckCurrentProjectContext);
			}

			{
				CommandID projectsMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginMultiProjectCmdSet, (int)PkgCmdIDList.cmdidCheckProjectsCppcheck);
				menuCheckCurrentProjectsContext = new MenuCommand(onCheckAllProjectsRequested, projectsMenuCommandID);
				mcs.AddCommand(menuCheckCurrentProjectsContext);
			}

			{
				CommandID selectionsMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginMultiItemProjectCmdSet, (int)PkgCmdIDList.cmdidCheckMultiItemCppcheck1);
				checkMultiSelections = new MenuCommand(onCheckSelectionsRequested, selectionsMenuCommandID);
				mcs.AddCommand(checkMultiSelections);
			}

			setMenuState(false);
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
			JoinableTaskFactory.Run(async () =>
			{
				await JoinableTaskFactory.SwitchToMainThreadAsync();
				_ = checkFirstActiveProjectAsync();
			});
		}

		private void onCheckAllProjectsRequested(object sender, EventArgs e)
		{
			JoinableTaskFactory.Run(async () =>
			{
				var cppProjects = await findAllCppProjectsAsync();
				Assumes.NotNull(cppProjects);

				if (cppProjects.Any())
					_ = checkProjectsAsync(cppProjects);
				else
				{
					await JoinableTaskFactory.SwitchToMainThreadAsync();
					MessageBox.Show("No C++ projects found in the solution - nothing to check.");
				}
			});
		}

		private void onCheckSelectionsRequested(object sender, EventArgs e)
		{
			JoinableTaskFactory.Run(async () =>
			{
				var cppProjects = await findSelectedCppProjectsAsync();
				Assumes.NotNull(cppProjects);

				if (cppProjects.Any())
					_ = checkProjectsAsync(cppProjects);
				else
				{
					await JoinableTaskFactory.SwitchToMainThreadAsync();
					MessageBox.Show("No C++ projects selected - nothing to check.");
				}
			});
		}

		private void onStopCheckRequested(object sender, EventArgs e)
		{
			stopAnalysis();
		}

		private void onSettingsWindowRequested(object sender, EventArgs e)
		{
			var settings = new CppcheckSettings();
			settings.ShowDialog();
		}

		private void documentSavedSync(Document document)
		{
			JoinableTaskFactory.Run(async () =>
			{
				await JoinableTaskFactory.SwitchToMainThreadAsync();

				if (document == null || document.Language != "C/C++")
					return;

				if (Settings.Default.CheckSavedFilesHasValue && Settings.Default.CheckSavedFiles == false)
					return;

				if (document.ActiveWindow == null)
				{
					// We get here when new files are being created and added to the project and
					// then trying to obtain document.ProjectItem yields an exception. Will just skip this.
					return;
				}
				try
				{
					var kind = document.ProjectItem.ContainingProject.Kind;
					if (!isVisualCppProjectKind(document.ProjectItem.ContainingProject.Kind))
					{
						return;
					}

					Configuration currentConfig = null;
					try { currentConfig = document.ProjectItem.ConfigurationManager.ActiveConfiguration; }
					catch (Exception) { currentConfig = null; }
					if (currentConfig == null)
					{
						MessageBox.Show("Cannot perform check - no valid configuration selected", "Cppcheck error");
						return;
					}

					//dynamic project = document.ProjectItem.ContainingProject.Object;
					Project project = document.ProjectItem.ContainingProject;
					SourceFile sourceForAnalysis = await createSourceFileAsync(document.ProjectItem);
					if (sourceForAnalysis == null)
						return;

					if (!Settings.Default.CheckSavedFilesHasValue)
					{
						askCheckSavedFiles();

						if (!Settings.Default.CheckSavedFiles)
							return;
					}

					MainToolWindow.Instance.showIfWindowNotCreated();
					MainToolWindow.Instance.ContentsType = ICodeAnalyzer.AnalysisType.DocumentSavedAnalysis;
					runSavedFileAnalysis(sourceForAnalysis, currentConfig);
				}
				catch (Exception ex)
				{
					if (_outputPane != null)
					{
						_outputPane.Clear();
						_ = AddTextToOutputWindowAsync("Exception occurred in cppcheck add-in: " + ex.Message);
					}
					DebugTracer.Trace(ex);
				}
			});
		}

		public static void askCheckSavedFiles()
		{
			DialogResult reply = MessageBox.Show("Do you want to start analysis any time a file is saved? It will clear previous analysis results.\nYou can change this behavior in cppcheck settings.", "Cppcheck: start analysis when file is saved?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
			Settings.Default.CheckSavedFiles = (reply == DialogResult.Yes);
			Settings.Default.Save();
		}

		private async Task<List<Project>> findSelectedCppProjectsAsync()
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync();

			var cppProjects = new List<Project>();
			foreach (dynamic project in _dte.ActiveSolutionProjects as Object[])
			{
				if (isVisualCppProjectKind(project.Kind))
					cppProjects.Add(project);
			}

			//System.Windows.MessageBox.Show("No project selected in Solution Explorer - nothing to check.");
			return cppProjects;
		}

		private async Task<List<Project>> findAllCppProjectsAsync()
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync();

			var cppProjects = new List<Project>();
			foreach (Project project in _dte.Solution.Projects)
			{
				if (isVisualCppProjectKind(project.Kind))
					cppProjects.Add(project);
			}

			return cppProjects;
		}

		// Looks at the project item. If it's a supported file type it's added to the list, and if it's a filter it keeps digging into it.
		private async Task scanProjectItemForSourceFilesAsync(ProjectItem item, SourceFilesWithConfiguration configuredFiles, Configuration configuration, Project project)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync();

			var itemType = await getTypeOfProjectItemAsync(item);

			if (itemType == ProjectItemType.folder)
			{
				foreach (ProjectItem subItem in item.ProjectItems)
				{
					await scanProjectItemForSourceFilesAsync(subItem, configuredFiles, configuration, project);
				}
			}
			else if (itemType == ProjectItemType.headerFile || itemType == ProjectItemType.cFile || itemType == ProjectItemType.cppFile)
			{
				var itemName = item.Name;

				if (item.FileCount == 0)
				{
					Debug.Fail("isCppFileAsync(item) is true, but item.FileCount is null!");
					return;
				}

				var itemFileName = item.FileNames[1];
				if (!configuredFiles.Exists(itemFileName))
				{
					// Don't bother rebuilding the entire definition if it already exists.
					SourceFile sourceFile = await createSourceFileAsync(item);
					if (sourceFile != null)
					{
						configuredFiles.addOrUpdateFile(sourceFile);
						scanProgressUpdated(configuredFiles.Count());
					}
				}
			}
		}

		private async Task<SourceFilesWithConfiguration> getAllSupportedFilesFromProjectAsync(Project project)
		{
			var sourceFiles = new SourceFilesWithConfiguration();
			sourceFiles.Configuration = await getConfigurationAsync(project);

			await JoinableTaskFactory.SwitchToMainThreadAsync();

			foreach (ProjectItem item in project.ProjectItems)
			{
				ProjectItemType itemType = await getTypeOfProjectItemAsync(item);
				if (itemType == ProjectItemType.cFile || itemType == ProjectItemType.cppFile || itemType == ProjectItemType.headerFile)
				{

					//List<SourceFile> projectSourceFileList = await getProjectFilesAsync(project, configuration);
					//foreach (SourceFile projectSourceFile in projectSourceFileList)
					//	addEntry(currentConfiguredFiles, projectSourceFileList, project);
				}
			}

			return sourceFiles;
		}

		private async Task checkFirstActiveProjectAsync()
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync();

			var activeProjects = await findSelectedCppProjectsAsync();
			Assumes.NotNull(activeProjects);

			if (activeProjects.Any())
				_ = checkProjectsAsync(new List<Project> { activeProjects.First() });
			else
				MessageBox.Show("No project selected in Solution Explorer - nothing to check.");
		}

		private async Task<Configuration> getConfigurationAsync(Project project)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync();

			try
			{
				return project.ConfigurationManager.ActiveConfiguration;
			}
			catch (Exception)
			{
				return null;
			}
		}

		private async Task checkProjectsAsync(List<Project> projects)
		{
			Debug.Assert(projects.Any());
			setMenuState(true);

			List<SourceFilesWithConfiguration> allConfiguredFiles = new List<SourceFilesWithConfiguration>();
			foreach (var project in projects)
			{
				await JoinableTaskFactory.SwitchToMainThreadAsync();

				Configuration config = await getConfigurationAsync(project);
				if (config == null)
				{
					MessageBox.Show("No valid configuration in project " + project.Name);
					continue;
				}

				SourceFilesWithConfiguration sourceFiles = new SourceFilesWithConfiguration();
				sourceFiles.Configuration = config;

				foreach (ProjectItem projectItem in project.ProjectItems)
				{
					await scanProjectItemForSourceFilesAsync(projectItem, sourceFiles, config, project);
				}

				// Although we're using the same base configuration, it's possible for each file to override that.
				// Group files into separate configs based on actual parameters. We'll be iterating in reverse order so
				// reverse the list first to keep the final order the same.
				List<SourceFile> allSourceFiles = sourceFiles.Files.ToList();
				allSourceFiles.Reverse();

				while (allSourceFiles.Any())
				{
					SourceFilesWithConfiguration newConfig = new SourceFilesWithConfiguration();
					newConfig.Configuration = config;

					SourceFile templateFile = allSourceFiles.Last();
					newConfig.addOrUpdateFile(templateFile);
					allSourceFiles.RemoveAt(allSourceFiles.Count - 1);

					for (int i = allSourceFiles.Count - 1; i >= 0; i--)
					{
						SourceFile otherFile = allSourceFiles[i];

						if (otherFile.Macros.All(templateFile.Macros.Contains) && templateFile.Macros.All(otherFile.Macros.Contains) &&
							otherFile.MacrosToUndefine.All(templateFile.MacrosToUndefine.Contains) && templateFile.MacrosToUndefine.All(otherFile.MacrosToUndefine.Contains) &&
							otherFile.IncludePaths.All(templateFile.IncludePaths.Contains) && templateFile.IncludePaths.All(otherFile.IncludePaths.Contains) &&
							otherFile.ProjectName == templateFile.ProjectName
							)
						{
							newConfig.addOrUpdateFile(otherFile);
							allSourceFiles.RemoveAt(i);
						}
					}

					if (newConfig.Any())
					{
						allConfiguredFiles.Add(newConfig);
					}
				}
			}

			_ = JoinableTaskFactory.RunAsync(async () =>
			{
				await JoinableTaskFactory.SwitchToMainThreadAsync();

				MainToolWindow.Instance.ContentsType = ICodeAnalyzer.AnalysisType.ProjectAnalysis;
				MainToolWindow.Instance.showIfWindowNotCreated();
			});

			scanProgressUpdated(-1);
			runAnalysis(allConfiguredFiles, false);
		}

		private async Task<ProjectItemType> getTypeOfProjectItemAsync(ProjectItem item)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync();
			var itemKind = item.Kind;
			var itemName = item.Name;

			switch (itemKind)
			{
				case VSConstants.ItemTypeGuid.PhysicalFolder_string:
				case VSConstants.ItemTypeGuid.VirtualFolder_string:
					return ProjectItemType.folder;

				case "{8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A}":
				case VSConstants.ItemTypeGuid.PhysicalFile_string:
					if (item.ConfigurationManager != null)
					{
						try
						{
							VCFile vcFile = item.Object as VCFile;
							VCProject vcProject = item.ContainingProject.Object as VCProject;
							VCFileConfiguration fileConfig = vcFile.FileConfigurations.Item(vcProject.ActiveConfiguration.Name);

							if (!fileConfig.ExcludedFromBuild)
							{
								if (item.FileCodeModel != null && item.FileCodeModel.Language == CodeModelLanguageConstants.vsCMLanguageVC)
								{
									switch (vcFile.ItemType)
									{
										case "ClInclude":
											return ProjectItemType.headerFile;

										case "ClCompile":
											return ProjectItemType.cppFile;
									}
								}
							}
						}
						catch (Exception)
						{
						}
					}

					return ProjectItemType.other;

				default:
					return ProjectItemType.other;
			}
		}

		private void runSavedFileAnalysis(SourceFile file, Configuration currentConfig)
		{
			Debug.Assert(currentConfig != null);

			var configuredFiles = new SourceFilesWithConfiguration();
			configuredFiles.addOrUpdateFile(file);
			configuredFiles.Configuration = currentConfig;

			_ = System.Threading.Tasks.Task.Run(async delegate
			{
				await System.Threading.Tasks.Task.Delay(750);
				await JoinableTaskFactory.SwitchToMainThreadAsync();
				runAnalysis(new List<SourceFilesWithConfiguration> { configuredFiles }, true);
			});
		}

		public void stopAnalysis()
		{
			foreach (var analyzer in _analyzers)
			{
				analyzer.abortThreadIfAny();
			}
		}

		private void runAnalysis(List<SourceFilesWithConfiguration> configuredFiles, bool analysisOnSavedFile)
		{
			completedFileCount = 0;

			foreach (var analyzer in _analyzers)
			{
				lastAnalyzerTotalFiles = 0;
				analyzer.analyze(configuredFiles, analysisOnSavedFile);
			}
		}

		private static void recursiveAddToolDetails(SourceFile sourceForAnalysis, VCConfiguration projectConfig, VCCLCompilerTool tool, dynamic propertySheets, ref bool bInheritDefs, ref bool bInheritUndefs)
		{
			// TODO: If the special keyword "\\\"$(INHERIT)\\\"" appears, we should inherit at that specific point.
			if (bInheritDefs)
			{
				string[] macrosToDefine = tool.PreprocessorDefinitions.Split(';');
				bInheritDefs = !macrosToDefine.Contains("\\\"$(NOINHERIT)\\\"");
				for (int i = 0; i < macrosToDefine.Length; ++i)
				{
					macrosToDefine[i] = Environment.ExpandEnvironmentVariables(projectConfig.Evaluate(macrosToDefine[i]));
				}

				sourceForAnalysis.addMacros(macrosToDefine);
			}

			if (bInheritUndefs)
			{
				string[] macrosToUndefine = tool.UndefinePreprocessorDefinitions.Split(';');
				bInheritUndefs = !macrosToUndefine.Contains("\\\"$(NOINHERIT)\\\"");
				for (int i = 0; i < macrosToUndefine.Length; ++i)
				{
					macrosToUndefine[i] = Environment.ExpandEnvironmentVariables(projectConfig.Evaluate(macrosToUndefine[i]));
				}

				sourceForAnalysis.addMacrosToUndefine(macrosToUndefine);
			}

			if (propertySheets != null && (bInheritDefs || bInheritUndefs))
			{
				// Scan any inherited property sheets.
				foreach (var propSheet in propertySheets)
				{
					VCCLCompilerTool propSheetTool = (VCCLCompilerTool)propSheet.Tools.Item("VCCLCompilerTool");
					if (propSheetTool != null)
					{
						// When looping over the inherited property sheets, don't allow rules to filter back up the way.
						bool bInheritDefs1 = bInheritDefs, bInheritUndefs1 = bInheritUndefs;
						recursiveAddToolDetails(sourceForAnalysis, projectConfig, propSheetTool, propSheet.PropertySheets, ref bInheritDefs1, ref bInheritUndefs1);
					}
				}
			}
		}

		private static async Task<SourceFile> createSourceFileAsync(ProjectItem item)
		{
			try
			{
				await Instance.JoinableTaskFactory.SwitchToMainThreadAsync();

				Debug.Assert(isVisualCppProjectKind(item.ContainingProject.Kind));

				VCFile vcFile = item.Object as VCFile;
				VCProject vcProject = item.ContainingProject.Object as VCProject;
				VCConfiguration vcconfig = vcProject.ActiveConfiguration;
				VCFileConfiguration fileConfig = vcFile.FileConfigurations.Item(vcconfig.Name);

				string toolSetName = ((dynamic)vcconfig).PlatformToolsetFriendlyName;

				string projectDirectory = vcProject.ProjectDirectory;
				string projectName = vcProject.Name;
				SourceFile sourceForAnalysis = null;

				if (item.FileCount > 0)
				{
					bool bInheritDefs = true, bInheritUndefs = true;
					string[] includePaths = { };

					// Do the file-level first in case it disables inheritance. Include files don't have file-level config.
					if (implementsInterface(fileConfig.Tool, "Microsoft.VisualStudio.VCProjectEngine.VCCLCompilerTool"))
					{
						VCCLCompilerTool vcTool = (VCCLCompilerTool)fileConfig.Tool;
						sourceForAnalysis = new SourceFile(item.FileNames[1], projectDirectory, projectName, toolSetName);
						includePaths = vcTool.FullIncludePath.Split(';');
						string macros = vcTool.PreprocessorDefinitions;
						// Other details may be gathered from the file, project or any inherited property sheets.
						recursiveAddToolDetails(sourceForAnalysis, vcconfig, vcTool, null, ref bInheritDefs, ref bInheritUndefs);
					}

					// Now get the full include path
					VCCLCompilerTool projectTool = (VCCLCompilerTool)vcconfig.Tools.Item("VCCLCompilerTool");
					if (projectTool != null && implementsInterface(projectTool, "Microsoft.VisualStudio.VCProjectEngine.VCCLCompilerTool"))
					{
						if (sourceForAnalysis == null)
						{
							sourceForAnalysis = new SourceFile(item.FileNames[1], projectDirectory, projectName, toolSetName);
							includePaths = projectTool.FullIncludePath.Split(';');
						}

						// Take the full include path from file level, which is already fully resolved.
						for (int i = 0; i < includePaths.Length; ++i)
						{
							includePaths[i] = Environment.ExpandEnvironmentVariables(vcconfig.Evaluate(includePaths[i]));
						}
						sourceForAnalysis.addIncludePaths(includePaths);

						recursiveAddToolDetails(sourceForAnalysis, vcconfig, projectTool, vcconfig.PropertySheets, ref bInheritDefs, ref bInheritUndefs);
					}
				}

				return sourceForAnalysis;
			}
			catch (Exception ex)
			{
				DebugTracer.Trace(ex);
				return null;
			}
		}

		private static bool isVisualCppProjectKind(string kind)
		{
			return kind.Equals(VSConstants.UICONTEXT.VCProject_string);
		}

		private static bool implementsInterface(object objectToCheck, string interfaceName)
		{
			Type objectType = objectToCheck.GetType();
			var requestedInterface = objectType.GetInterface(interfaceName);
			return requestedInterface != null;
		}

		private async void scanProgressUpdated(int filesScanned)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync();

			EnvDTE.StatusBar statusBar = _dte.StatusBar;
			if (statusBar == null)
				return;

			try
			{
				if (filesScanned >= 0)
				{
					string label = "cppcheck scanning for files (" + filesScanned + ")";
					statusBar.Text = label;
				}
				else
				{
					statusBar.Clear();
				}
			}
			catch (Exception) { }
		}

		private async void updateStatusBarProgress(bool inProgress, string label, int currentPercentage)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync();
			try
			{
				_dte.StatusBar.Progress(inProgress, label, currentPercentage, 100);
			}
			catch (Exception) { }
		}

		private async void checkProgressUpdated(object sender, ICodeAnalyzer.ProgressEvenArgs e)
		{
			int progress = e.Progress;
			if (progress == 0)
				progress = 1; // statusBar.Progress won't display a progress bar with 0%

			await JoinableTaskFactory.SwitchToMainThreadAsync();

			string label = "";
			if (progress < 100)
			{
				if (e.FilesChecked == 0 || e.TotalFilesNumber == 0)
					label = "cppcheck analysis in progress...";
				else
					label = "cppcheck analysis in progress (" + (completedFileCount + e.FilesChecked) + " out of " + (completedFileCount + e.TotalFilesNumber) + " files checked)";

				lastAnalyzerTotalFiles = e.TotalFilesNumber;

				updateStatusBarProgress(true, label, progress);
			}
			else
			{
				label = "cppcheck analysis completed";
				completedFileCount += lastAnalyzerTotalFiles;
				lastAnalyzerTotalFiles = 0;

				updateStatusBarProgress(true, label, Math.Max(progress, 100));

				_ = System.Threading.Tasks.Task.Run(async delegate
				{
					await System.Threading.Tasks.Task.Delay(5000);
					await JoinableTaskFactory.SwitchToMainThreadAsync();
					updateStatusBarProgress(false, label, 100);
				});

				setMenuState(false);
			}
		}

		private static void CreateDefaultGlobalSuppressions()
		{
			string globalsuppressionsFilePath = ICodeAnalyzer.suppressionsFilePathByStorage(ICodeAnalyzer.SuppressionStorage.Global);
			if (!File.Exists(globalsuppressionsFilePath))
			{
				SuppressionsInfo suppressionsInfo = new SuppressionsInfo();
				suppressionsInfo.SkippedIncludesMask.Add(".*Microsoft Visual Studio.*");
				suppressionsInfo.SkippedIncludesMask.Add(".*Microsoft SDKs.*");
				suppressionsInfo.SkippedIncludesMask.Add(".*Windows Kits.*");
				suppressionsInfo.SkippedIncludesMask.Add(".*boost.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\ActiveQt.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\Qt$");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtCore.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtDeclarative.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtGui.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtMultimedia.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtNetwork.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtOpenGL.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtOpenVG.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtScript.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtScriptTools.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtSql.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtSvg.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtTest.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtWebKit.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtXml.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtXmlPatterns.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtConcurrent.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtMultimediaWidgets.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtOpenGLExtensions.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtQml.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtQuick.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtSensors.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtWebKitWidgets.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtWidgets.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtZlib.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\include\\QtV8.*");
				suppressionsInfo.SkippedIncludesMask.Add(@".*\\mkspecs\\win32-.*");

				suppressionsInfo.SkippedFilesMask.Add("^moc_.*\\.cpp$");
				suppressionsInfo.SkippedFilesMask.Add("^qrc_.*\\.cpp$");
				suppressionsInfo.SkippedFilesMask.Add("^ui_.*\\.h$");

				suppressionsInfo.SaveToFile(globalsuppressionsFilePath);
			}
		}

		enum ProjectItemType { cppFile, cFile, headerFile, folder, other };

		private static DTE _dte = null;
		private DocumentEvents _eventsHandlers = null;
		private CommandEvents _commandEventsHandlers = null;
		private List<ICodeAnalyzer> _analyzers = new List<ICodeAnalyzer>();

		private OutputWindowPane _outputPane = null;

		private const int commandEventIdSave = 331;
		private const int commandEventIdSaveAll = 224;

		private static CPPCheckPluginPackage _instance = null;
	}
}
