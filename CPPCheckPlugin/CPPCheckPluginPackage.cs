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
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.VisualStudio.VCProjectEngine;

namespace VSPackage.CPPCheckPlugin
{
	[PackageRegistration(UseManagedResourcesOnly = true)]
	[DefaultRegistryRoot(@"Software\Microsoft\VisualStudio\11.0")]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
	// This attribute is used to register the information needed to show this package
	// in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#112", "1.2.0", IconResourceID = 400)]
	// This attribute is needed to let the shell know that this package exposes some menus.
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideToolWindow(typeof(MainToolWindow), Style=VsDockStyle.Tabbed, Window=Microsoft.VisualStudio.Shell.Interop.ToolWindowGuids.Outputwindow, MultiInstances=false, Transient=false)]
	[Guid(GuidList.guidCPPCheckPluginPkgString)]
	public sealed class CPPCheckPluginPackage : Package
	{
		public CPPCheckPluginPackage()
		{
			CreateDefaultGlobalSuppressions();
		}

		private static void CreateDefaultGlobalSuppressions()
		{
			String globalsuppressionsFilePath = ICodeAnalyzer.suppressionsFilePathByStorage(ICodeAnalyzer.SuppressionStorage.Global);
			if (!System.IO.File.Exists(globalsuppressionsFilePath))
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

		public static String solutionName()
		{
			try { return System.IO.Path.GetFileNameWithoutExtension(_dte.Solution.FullName); }
			catch(Exception) { return ""; }
			
		}

		public static String solutionPath()
		{
			try { return System.IO.Path.GetDirectoryName(_dte.Solution.FullName); }
			catch (Exception) { return ""; }
		}

		public static String activeProjectName()
		{
			var project = activeProject();
			if (project != null)
				return project.Name;

			return "";
		}

		public static String activeProjectPath()
		{
			var project = activeProject();
			if (project != null)
				return project.ProjectDirectory.Replace("\"", "");

			return "";
		}

		private static dynamic activeProject()
		{
			Object[] activeProjects = (Object[])_dte.ActiveSolutionProjects;
			if (!activeProjects.Any())
			{
				return null;
			}

			foreach (dynamic o in activeProjects)
			{
				dynamic project = o.Object;
				if (!isVisualCppProject(project))
				{
					return null;
				}
				return project;
			}

			return null;
		}

		#region Package Members

		protected override void Initialize()
		{
			Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
			base.Initialize();

			_dte = (EnvDTE.DTE)GetService(typeof(SDTE));
            _eventsHandlers = _dte.Events.DocumentEvents;
			_eventsHandlers.DocumentSaved += documentSaved;

			_outputPane = _dte.AddOutputWindowPane("cppcheck analysis output");

			AnalyzerCppcheck cppcheckAnalayzer = new AnalyzerCppcheck();
			cppcheckAnalayzer.ProgressUpdated += checkProgressUpdated;
			_analyzers.Add(cppcheckAnalayzer);

			if (String.IsNullOrEmpty(Properties.Settings.Default.DefaultArguments))
				Properties.Settings.Default.DefaultArguments = CppcheckSettings.DefaultArguments;

			// Add our command handlers for menu (commands must exist in the .vsct file)
			OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
			if ( null != mcs )
			{
                // Create the command for the menu item.
                {
                    CommandID menuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidCheckProjectCppcheck);
                    MenuCommand menuItem = new MenuCommand(onCheckCurrentProjectRequested, menuCommandID);
                    mcs.AddCommand(menuItem);
                }

                {
                    // Create the command for the settings window
                    CommandID settingsWndCmdId = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidSettings);
                    MenuCommand menuSettings = new MenuCommand(onSettingsWindowRequested, settingsWndCmdId);
                    mcs.AddCommand(menuSettings);
                }

                {
                    CommandID stopCheckMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidStopCppcheck);
                    MenuCommand stopCheckMenuItem = new MenuCommand(onStopCheckRequested, stopCheckMenuCommandID);
                    mcs.AddCommand(stopCheckMenuItem);
                }

                {
                    CommandID selectionsMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginCmdSet, (int)PkgCmdIDList.cmdidCheckMultiItemCppcheck);
                    MenuCommand selectionsMenuItem = new MenuCommand(onCheckSelectionsRequested, selectionsMenuCommandID);
                    mcs.AddCommand(selectionsMenuItem);
                }

                {
                    CommandID projectMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginProjectCmdSet, (int)PkgCmdIDList.cmdidCheckProjectCppcheck1);
                    MenuCommand projectMenuItem = new MenuCommand(onCheckCurrentProjectRequested, projectMenuCommandID);
                    mcs.AddCommand(projectMenuItem);
                }

                {
                    CommandID projectsMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginMultiProjectCmdSet, (int)PkgCmdIDList.cmdidCheckProjectsCppcheck);
                    MenuCommand projectsMenuItem = new MenuCommand(onCheckAllProjectsRequested, projectsMenuCommandID);
                    mcs.AddCommand(projectsMenuItem);
                }

                {
                    CommandID selectionsMenuCommandID = new CommandID(GuidList.guidCPPCheckPluginMultiItemProjectCmdSet, (int)PkgCmdIDList.cmdidCheckMultiItemCppcheck1);
                    MenuCommand selectionsMenuItem = new MenuCommand(onCheckSelectionsRequested, selectionsMenuCommandID);
                    mcs.AddCommand(selectionsMenuItem);
                }

                
            }

			// Creating the tool window
			FindToolWindow(typeof(MainToolWindow), 0, true);
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
			checkFirstActiveProject();
		}

		private void onCheckAllProjectsRequested(object sender, EventArgs e)
		{
			checkAllActiveProjects();
		}

        private void onCheckSelectionsRequested(object sender, EventArgs e)
        {
            checkSelections();
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

		private void documentSaved(Document document)
		{
			if (document == null || document.Language != "C/C++")
				return;

			if (Properties.Settings.Default.CheckSavedFilesHasValue && Properties.Settings.Default.CheckSavedFiles == false)
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

				Configuration currentConfig = null;
				try { currentConfig = document.ProjectItem.ConfigurationManager.ActiveConfiguration; }
				catch (Exception) { currentConfig = null; }
				if (currentConfig == null)
				{
					MessageBox.Show("Cannot perform check - no valid configuration selected", "Cppcheck error");
					return;
				}

				SourceFile sourceForAnalysis = createSourceFile(document.FullName, currentConfig, project);
				if (sourceForAnalysis == null)
					return;

				if (!Properties.Settings.Default.CheckSavedFilesHasValue)
				{
					askCheckSavedFiles();

					if (!Properties.Settings.Default.CheckSavedFiles)
						return;
				}

				MainToolWindow.Instance.showIfWindowNotCreated();
				MainToolWindow.Instance.ContentsType = ICodeAnalyzer.AnalysisType.DocumentSavedAnalysis;
				runSavedFileAnalysis(sourceForAnalysis, currentConfig, _outputPane);
			}
			catch (System.Exception ex)
			{
				if (_outputPane != null)
				{
					_outputPane.Clear();
					_outputPane.OutputString("Exception occurred in cppcheck add-in: " + ex.Message);
				}
				DebugTracer.Trace(ex);
			}
		}

		public static void askCheckSavedFiles()
		{
			DialogResult reply = MessageBox.Show("Do you want to start analysis any time a file is saved? It will clear previous analysis results.\nYou can change this behavior in cppcheck settings.", "Cppcheck: start analysis when file is saved?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
			Properties.Settings.Default.CheckSavedFiles = (reply == DialogResult.Yes);
		}

		private Object[] getActiveProjects()
		{
			Object[] activeProjects = (Object[])_dte.ActiveSolutionProjects;
			if (!activeProjects.Any())
			{
				System.Windows.MessageBox.Show("No project selected in Solution Explorer - nothing to check.");
				return null;
			}
			return activeProjects;
		}

        private static void addEntry(ConfiguredFiles configuredFiles, SourceFile sourceFile, Project project)
        {
            if(sourceFile != null)
            {
                List<SourceFile> sourceFileList = new List<SourceFile>();
                sourceFileList.Add(sourceFile);
                addEntry(configuredFiles, sourceFileList, project);
            }
        }

        private static void addEntry(ConfiguredFiles configuredFiles, List<SourceFile> sourceFileList, Project project)
        {
            Boolean foundFlag = false;
            foreach (SourceFile newSourceFile in sourceFileList)
            {
                if (newSourceFile == null)
                    continue;

                foundFlag = false;
                for (int index = 0; index < configuredFiles.Files.Count && !foundFlag; index++)
                {
                    if (newSourceFile.FileName.CompareTo(configuredFiles.Files[index].FileName) == 0 &&
                        newSourceFile.FilePath.CompareTo(configuredFiles.Files[index].FilePath) == 0)
                    {
                        // file already exists in list
                        foundFlag = true;
                    }
                }

                if(!foundFlag)
                {
                    configuredFiles.Files.Add(newSourceFile);
                    string projectName = project.Name;
                    _outputPane.OutputString("Will check: " + projectName + " | " + newSourceFile.FilePath + "/" + newSourceFile.FileName);
                }
            }
        }

        private static void scanFilter(dynamic filter, List<SourceFile> sourceFileList, ConfiguredFiles configuredFiles,
            Configuration configuration, Project project)
        {
            foreach(dynamic item in filter.Items)
            {
                if(isFilter(item))
                {
                    scanFilter(item, sourceFileList, configuredFiles, configuration, project);
                }
                else if(isCppFile(item))
                {
                    dynamic file = item.ProjectItem.Object;

                    // non project selected
                    if (file != null)
                    {
                        // document selected
                        SourceFile sourceFile = createSourceFile(file.FullPath, configuration, project.Object);
                        addEntry(configuredFiles, sourceFile, project);
                    }
                }
            }
        }

        private List<ConfiguredFiles> getActiveSelections()
        {           
            Dictionary<Project, ConfiguredFiles> confMap = new Dictionary<Project, ConfiguredFiles>();

            foreach (SelectedItem selItem in _dte.SelectedItems)
            {
                Project project = null;

                if (project == null && selItem.ProjectItem != null)
                {
                    project = selItem.ProjectItem.ContainingProject;
                }
                
                if(project == null)
                {
                    project = selItem.Project;
                }

                if (project == null || !isVisualCppProject(project.Object))
                {
                    continue;
                }

                Configuration configuration = getConfiguration(project);

                if (!confMap.ContainsKey(project))
                {
                    // create new Map key entry for project
                    ConfiguredFiles configuredFiles = new ConfiguredFiles();
                    confMap.Add(project, configuredFiles);
                    configuredFiles.Files = new List<SourceFile>();
                    configuredFiles.Configuration = configuration;
                }

                ConfiguredFiles currentConfiguredFiles = confMap[project];

                if(currentConfiguredFiles == null)
                {
                    continue;
                }

                if (selItem.ProjectItem == null)
                {
                    // project selected
                    List<SourceFile> projectSourceFileList = getProjectFiles(project, configuration);
                    foreach (SourceFile projectSourceFile in projectSourceFileList)
                        addEntry(currentConfiguredFiles, projectSourceFileList, project);
                }
                else
                {
                    dynamic projectItem = selItem.ProjectItem.Object;

                    if (isFilter(projectItem))
                    {
                        List<SourceFile> sourceFileList = new List<SourceFile>();
                        scanFilter(projectItem, sourceFileList, currentConfiguredFiles, configuration, project);
                        addEntry(currentConfiguredFiles, sourceFileList, project);
                    }
                    else if (isCppFile(projectItem))
                    {
                        dynamic file = selItem.ProjectItem.Object;

                        // non project selected
                        if (file != null)
                        {
                            // document selected
                            SourceFile sourceFile = createSourceFile(file.FullPath, configuration, project.Object);
                            addEntry(currentConfiguredFiles, sourceFile, project);
                        }
                    }
                }
            }

            List<ConfiguredFiles> configuredFilesList = new List<ConfiguredFiles>();
            foreach (ConfiguredFiles configuredFiles in confMap.Values)
            {
                configuredFilesList.Add(configuredFiles);
            }

            return configuredFilesList;
        }

		private void checkFirstActiveProject()
		{
			Object[] activeProjects = getActiveProjects();
			if (activeProjects != null)
				checkProjects(new Object[1] { activeProjects[0] });
		}

		private void checkAllActiveProjects()
		{
			Object[] activeProjects = getActiveProjects();
			if (activeProjects != null)
				checkProjects(activeProjects);
		}

        private void checkSelections()
        {
            List<ConfiguredFiles> configuredFilesList = getActiveSelections();

            MainToolWindow.Instance.ContentsType = ICodeAnalyzer.AnalysisType.ProjectAnalysis;
			MainToolWindow.Instance.showIfWindowNotCreated();

            if(configuredFilesList.Count > 0)
            {
                runAnalysis(configuredFilesList, _outputPane, false);
            }
        }

        private List<SourceFile> getProjectFiles(Project p, Configuration currentConfig)
		{
			dynamic project = p.Object;
			if (!isVisualCppProject(project))
			{
				System.Windows.MessageBox.Show("Only C++ projects can be checked.");
				return null;
			}
			List<SourceFile> files = new List<SourceFile>();
			dynamic projectFiles = project.Files;
			foreach (dynamic file in projectFiles)
			{
				if (isCppFile(file))
				{
					String fileName = file.Name;
					SourceFile f = createSourceFile(file.FullPath, currentConfig, project);
					if (f != null)
						files.Add(f);
				}
			}
			return files;
		}

		private Configuration getConfiguration(Project project)
		{
			try 
			{ 
				return project.ConfigurationManager.ActiveConfiguration; 
			}
			catch (Exception) 
			{ 
				return null; 
			}
		}

		private void checkProjects(Object[] activeProjects)
		{
			Debug.Assert(activeProjects.Any());

			List<ConfiguredFiles> allConfiguredFiles = new List<ConfiguredFiles>();
			foreach (dynamic o in activeProjects)
			{
				Configuration configuration = getConfiguration(o);
				if (configuration == null)
				{
					MessageBox.Show("Cannot perform check - no valid configuration selected", "Cppcheck error");
					return;
				}
				dynamic projectFiles = getProjectFiles(o, configuration);
				if (projectFiles == null)
					continue;

				ConfiguredFiles configuredFiles = new ConfiguredFiles();
				configuredFiles.Files = projectFiles;
				configuredFiles.Configuration = configuration;
				allConfiguredFiles.Add(configuredFiles);
			}

			MainToolWindow.Instance.ContentsType = ICodeAnalyzer.AnalysisType.ProjectAnalysis;
			MainToolWindow.Instance.showIfWindowNotCreated();
			runAnalysis(allConfiguredFiles, _outputPane, false);
		}

        private static bool isCppFile(dynamic file)
        {
            // Checking file.FileType == eFileType.eFileTypeCppCode...
            // Automatic property binding fails with VS2013 because there the FileType property
            // is *explicitly implemented* and so only accessible via the declaring interface.
            // Using Reflection to get to the interface and access the property directly instead.
            Type fileObjectType = file.GetType();
            var vcFileInterface = fileObjectType.GetInterface("Microsoft.VisualStudio.VCProjectEngine.VCFile");
            var fileTypeValue = vcFileInterface.GetProperty("FileType").GetValue((object)file);
            Type fileTypeEnumType = fileTypeValue.GetType();
            Debug.Assert(fileTypeEnumType.FullName == "Microsoft.VisualStudio.VCProjectEngine.eFileType");
            var fileTypeEnumValue = Enum.GetName(fileTypeEnumType, fileTypeValue);
            var fileTypeCppCodeConstant = "eFileTypeCppCode";
            // First check the enum contains the value we're looking for
            Debug.Assert(Enum.GetNames(fileTypeEnumType).Contains(fileTypeCppCodeConstant));
            if (fileTypeEnumValue == fileTypeCppCodeConstant)
                return true;
            return false;
        }

        private static bool isFilter(dynamic checkObject)
        {
            Type checkObjectType = checkObject.GetType();
            var vcFilterInterface = checkObjectType.GetInterface("Microsoft.VisualStudio.VCProjectEngine.VCFilter");
            if (vcFilterInterface != null)
                return true;
            return false;
        }

        private void runSavedFileAnalysis(SourceFile file, Configuration currentConfig, OutputWindowPane outputPane)
		{
			Debug.Assert(currentConfig != null);
			
			var configuredFiles = new ConfiguredFiles();
			configuredFiles.Files = new List<SourceFile> {file};
			configuredFiles.Configuration = currentConfig;
			runAnalysis(new List<ConfiguredFiles> {configuredFiles}, outputPane, true);
		}

        public void stopAnalysis()
        {
            foreach (var analyzer in _analyzers)
            {
                analyzer.abortThreadIfAny();
            }
        }

		private void runAnalysis(List<ConfiguredFiles> configuredFiles, OutputWindowPane outputPane, bool analysisOnSavedFile)
		{
			Debug.Assert(outputPane != null);
			// outputPane.Clear();

			foreach (var analyzer in _analyzers)
			{
				analyzer.analyze(configuredFiles, outputPane, analysisOnSavedFile);
			}
		}

		private static SourceFile createSourceFile(string filePath, Configuration targetConfig, dynamic project)
		{
			Debug.Assert(isVisualCppProject((object)project));
			try
			{
				var configurationName = targetConfig.ConfigurationName;
				dynamic config = project.Configurations.Item(configurationName);
				String toolSetName = config.PlatformToolsetShortName;
				if (String.IsNullOrEmpty(toolSetName))
					toolSetName = config.PlatformToolsetFriendlyName;
				String projectDirectory = project.ProjectDirectory;
				String projectName = project.Name;
				SourceFile sourceForAnalysis = new SourceFile(filePath, projectDirectory, projectName, toolSetName);
				dynamic toolsCollection = config.Tools;
				foreach (var tool in toolsCollection)
				{
					// Project-specific includes
					Type toolType = tool.GetType();
					var compilerToolInterface = toolType.GetInterface("Microsoft.VisualStudio.VCProjectEngine.VCCLCompilerTool");
					if (compilerToolInterface != null)
					{
						String includes = tool.FullIncludePath;
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
				DebugTracer.Trace(ex);
				return null;
			}
		}

		private static bool isVisualCppProject(object project)
		{
			Type projectObjectType = project.GetType();
			var projectInterface = projectObjectType.GetInterface("Microsoft.VisualStudio.VCProjectEngine.VCProject");
			return projectInterface != null;
		}

		private void checkProgressUpdated(object sender, ICodeAnalyzer.ProgressEvenArgs e)
		{
			int progress = e.Progress;
			if (progress == 0)
				progress = 1; // statusBar.Progress won't display a progress bar with 0%
			EnvDTE.StatusBar statusBar = _dte.StatusBar;
			if (statusBar != null)
			{
				String label = "";
				if (progress < 100)
				{
					if (e.FilesChecked == 0 || e.TotalFilesNumber == 0)
						label = "cppcheck analysis in progress...";
					else
						label = "cppcheck analysis in progress (" + e.FilesChecked + " out of " + e.TotalFilesNumber + " files checked)";

					statusBar.Progress(true, label, progress, 100);
				}
				else
				{
					label = "cppcheck analysis completed";
					statusBar.Progress(true, label, progress, 100);
					System.Threading.Tasks.Task.Run(async delegate
					{
						await System.Threading.Tasks.Task.Delay(5000);
						statusBar.Progress(false, label, 100, 100);
					});
				}
			}
		}

		private static DTE _dte = null;
		private DocumentEvents _eventsHandlers = null;
		private List<ICodeAnalyzer> _analyzers = new List<ICodeAnalyzer>();

		private static OutputWindowPane _outputPane = null;
	}
}
