using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace VSPackage.CPPCheckPlugin
{
	class AnalyzerCppcheck : ICodeAnalyzer
	{
		private const string tempFilePrefix = "CPPCheckPlugin";

		public AnalyzerCppcheck()
		{
			// Perform some cleanup of old temporary files
			string tempPath = Path.GetTempPath();

			try
			{
				// Get all files that have our unique prefix
				string[] oldFiles = Directory.GetFiles(tempPath, tempFilePrefix + "*");

				foreach (string file in oldFiles)
				{
					DateTime fileModifiedDate = File.GetLastWriteTime(file);

					if (fileModifiedDate.AddMinutes(120) < DateTime.Now)
					{
						// File hasn't been written to in the last 120 minutes, so it must be 
						// from an earlier instance which didn't exit gracefully.
						File.Delete(file);
					}
				}
			}
			catch (System.Exception) { }
		}

		~AnalyzerCppcheck()
		{
			cleanupTempFiles();
		}

		public static string cppcheckExePath()
		{
			System.Collections.Specialized.StringCollection analyzerPathCandidates = Properties.Settings.Default.CPPcheckPath;
			string analyzerPath = "";

			foreach (string candidatePath in analyzerPathCandidates)
			{
				if (File.Exists(candidatePath))
				{
					analyzerPath = candidatePath;
					break;
				}
			}

			if (String.IsNullOrEmpty(analyzerPath))
			{
				OpenFileDialog dialog = new OpenFileDialog();
				dialog.Filter = "cppcheck executable|cppcheck.exe";
				if (dialog.ShowDialog() != DialogResult.OK)
					return String.Empty;

				analyzerPath = dialog.FileName;
				if (File.Exists(analyzerPath))
				{
					Properties.Settings.Default.CPPcheckPath.Add(analyzerPath);
					Properties.Settings.Default.Save();
				}
			}

			return analyzerPath;
		}

		private string getCPPCheckArgs(ConfiguredFiles configuredFiles, bool analysisOnSavedFile, bool multipleProjects, string tempFileName)
		{
			Debug.Assert(_numCores > 0);
			String cppheckargs = Properties.Settings.Default.DefaultArguments;

			if (Properties.Settings.Default.SeveritiesString.Length != 0)
				cppheckargs += " --enable=" + Properties.Settings.Default.SeveritiesString;

			HashSet<string> suppressions = new HashSet<string>(Properties.Settings.Default.SuppressionsString.Split(','));
			suppressions.Add("unmatchedSuppression");

			HashSet<string> skippedFilesMask = new HashSet<string>();
			HashSet<string> skippedIncludeMask = new HashSet<string>();

			SuppressionsInfo unitedSuppressionsInfo = readSuppressions(ICodeAnalyzer.SuppressionStorage.Global);
			unitedSuppressionsInfo.UnionWith(readSuppressions(ICodeAnalyzer.SuppressionStorage.Solution));

			var filesToAnalyze = configuredFiles.Files;
			// Creating the list of all different project locations (no duplicates)
			HashSet<string> projectPaths = new HashSet<string>(); // enforce uniqueness on the list of project paths
			foreach (var file in filesToAnalyze)
			{
				projectPaths.Add(file.BaseProjectPath);
			}

			Debug.Assert(projectPaths.Count == 1);
			_projectBasePath = projectPaths.First();
			_projectName = filesToAnalyze[0].ProjectName;

			// Creating the list of all different suppressions (no duplicates)
			foreach (var path in projectPaths)
			{
				unitedSuppressionsInfo.UnionWith(readSuppressions(SuppressionStorage.Project, path, filesToAnalyze[0].ProjectName));
			}

			if (!multipleProjects)
				cppheckargs += (" --relative-paths=\"" + filesToAnalyze[0].BaseProjectPath + "\"");
			cppheckargs += (" -j " + _numCores.ToString());
			if (Properties.Settings.Default.InconclusiveChecksEnabled)
				cppheckargs += " --inconclusive ";

			suppressions.UnionWith(unitedSuppressionsInfo.SuppressionLines);
			foreach (string suppression in suppressions)
			{
				if (!String.IsNullOrWhiteSpace(suppression))
					cppheckargs += (" --suppress=" + suppression);
			}

			if (!(analysisOnSavedFile && Properties.Settings.Default.IgnoreIncludePaths))
			{
				// We only add include paths once, and then specify a set of files to check
				HashSet<string> includePaths = new HashSet<string>();
				foreach (var file in filesToAnalyze)
				{
					if (!matchMasksList(file.FilePath, unitedSuppressionsInfo.SkippedFilesMask))
						includePaths.UnionWith(file.IncludePaths);
				}

				includePaths.Add(filesToAnalyze[0].BaseProjectPath); // Fix for #60

				foreach (string path in includePaths)
				{
					if (!matchMasksList(path, unitedSuppressionsInfo.SkippedIncludesMask))
					{
						String includeArgument = " -I\"" + path + "\"";
						cppheckargs = cppheckargs + " " + includeArgument;
					}
				}
			}

			using (StreamWriter tempFile = new StreamWriter(tempFileName))
			{
				foreach (SourceFile file in filesToAnalyze)
				{
					if (!matchMasksList(file.FilePath, unitedSuppressionsInfo.SkippedFilesMask))
						tempFile.WriteLine(file.FilePath);
				}
			}

			cppheckargs += " --file-list=\"" + tempFileName + "\"";

			if ((analysisOnSavedFile && Properties.Settings.Default.FileOnlyCheckCurrentConfig) ||
				(!analysisOnSavedFile && Properties.Settings.Default.ProjectOnlyCheckCurrentConfig)) // Only checking current macros configuration (for speed)
			{
				cppheckargs = cppheckargs.Replace("--force", "");
				// Creating the list of all different macros (no duplicates)
				HashSet<string> macros = new HashSet<string>();
				// TODO: handle /Zc:__cplusplus
				// https://devblogs.microsoft.com/cppblog/msvc-now-correctly-reports-__cplusplus/
				macros.Add("__cplusplus=199711L"); // At least in VS2012, this is still 199711L
												   // Assuming all files passed here are from the same project / same toolset, which should be true, so peeking the first file for global settings
				switch (filesToAnalyze[0].vcCompilerVersion)
				{
					case SourceFile.VCCompilerVersion.vc2003:
						macros.Add("_MSC_VER=1310");
						break;
					case SourceFile.VCCompilerVersion.vc2005:
						macros.Add("_MSC_VER=1400");
						break;
					case SourceFile.VCCompilerVersion.vc2008:
						macros.Add("_MSC_VER=1500");
						break;
					case SourceFile.VCCompilerVersion.vc2010:
						macros.Add("_MSC_VER=1600");
						break;
					case SourceFile.VCCompilerVersion.vc2012:
						macros.Add("_MSC_VER=1700");
						break;
					case SourceFile.VCCompilerVersion.vc2013:
						macros.Add("_MSC_VER=1800");
						break;
					case SourceFile.VCCompilerVersion.vc2015:
						macros.Add("_MSC_VER=1900");
						break;
					case SourceFile.VCCompilerVersion.vc2017:
						macros.Add("_MSC_VER=1916");
						break;
					case SourceFile.VCCompilerVersion.vc2019:
						macros.Add("_MSC_VER=1920");
						break;
				}

				foreach (var file in filesToAnalyze)
				{
					macros.UnionWith(file.Macros);
				}
				macros.Add("WIN32");
				macros.Add("_WIN32");

				CPPCheckPluginPackage.Instance.JoinableTaskFactory.Run(async () =>
				{
					if (await configuredFiles.is64bitConfigurationAsync())
					{
						macros.Add("_M_X64");
						macros.Add("_WIN64");
					}
					else
					{
						macros.Add("_M_IX86");
					}

					if (await configuredFiles.isDebugConfigurationAsync())
						macros.Add("_DEBUG");
				});

				foreach (string macro in macros)
				{
					if (!String.IsNullOrEmpty(macro) && !macro.Contains(" ") /* macros with spaces are invalid in VS */)
					{
						String macroArgument = " -D" + macro;
						cppheckargs += macroArgument;
					}
				}

				HashSet<string> macrosToUndefine = new HashSet<string>();
				foreach (var file in filesToAnalyze)
				{
					macrosToUndefine.UnionWith(file.MacrosToUndefine);
				}

				foreach (string macro in macrosToUndefine)
				{
					if (!String.IsNullOrEmpty(macro) && !macro.Contains(" ") /* macros with spaces are invalid in VS */)
					{
						String macroUndefArgument = " -U" + macro;
						cppheckargs += macroUndefArgument;
					}
				}
			}
			else if (!cppheckargs.Contains("--force"))
				cppheckargs += " --force";

			return cppheckargs;
		}

		public override void analyze(List<ConfiguredFiles> allConfiguredFiles, bool analysisOnSavedFile)
		{
			if (!allConfiguredFiles.Any())
				return;
			else
			{
				bool validFilesQueuedForCheck = false;
				foreach (ConfiguredFiles files in allConfiguredFiles)
					if (files.Files.Any())
					{
						validFilesQueuedForCheck = true;
						break;
					}

				if (!validFilesQueuedForCheck)
					return;
			}


			List<string> cppheckargs = new List<string>();
			foreach (var configuredFiles in allConfiguredFiles)
				cppheckargs.Add(getCPPCheckArgs(configuredFiles, analysisOnSavedFile, allConfiguredFiles.Count > 1, createNewTempFileName()));

			run(cppcheckExePath(), cppheckargs);
		}

		public override void suppressProblem(Problem p, SuppressionScope scope)
		{
			if (p == null)
				return;

			String simpleFileName = p.FileName;

			String suppressionLine = null;
			switch (scope)
			{
				case SuppressionScope.suppressAllMessagesThisFileGlobally:
				case SuppressionScope.suppressAllMessagesThisFileSolutionWide:
				case SuppressionScope.suppressAllMessagesThisFileProjectWide:
					suppressionLine = "*:" + simpleFileName;
					break;
				case SuppressionScope.suppressThisTypeOfMessageFileWide:
					suppressionLine = p.MessageId + ":" + simpleFileName;
					break;
				case SuppressionScope.suppressThisTypeOfMessagesGlobally:
				case SuppressionScope.suppressThisTypeOfMessageProjectWide:
				case SuppressionScope.suppressThisTypeOfMessagesSolutionWide:
					suppressionLine = p.MessageId;
					break;
				case SuppressionScope.suppressThisMessage:
				case SuppressionScope.suppressThisMessageSolutionWide:
				case SuppressionScope.suppressThisMessageGlobally:
					suppressionLine = p.MessageId + ":" + simpleFileName + ":" + p.Line;
					break;
				default:
					throw new InvalidOperationException("Unsupported value: " + scope.ToString());
			}

			String suppressionsFilePath = suppressionsFilePathByScope(scope, p.BaseProjectPath, p.ProjectName);
			Debug.Assert(suppressionsFilePath != null);

			SuppressionsInfo suppressionsInfo = new SuppressionsInfo();
			suppressionsInfo.LoadFromFile(suppressionsFilePath);

			suppressionsInfo.AddSuppressionLine(suppressionLine);

			suppressionsInfo.SaveToFile(suppressionsFilePath);
		}

		protected override SuppressionsInfo readSuppressions(SuppressionStorage storage, string projectBasePath = null, string projectName = null)
		{
			SuppressionsInfo suppressionsInfo = new SuppressionsInfo();

			String suppressionsFilePath = suppressionsFilePathByStorage(storage, projectBasePath, projectName);
			suppressionsInfo.LoadFromFile(suppressionsFilePath);

			return suppressionsInfo;
		}

		protected override List<Problem> parseOutput(String output)
		{
			// template={file}|{line}|{severity}|{id}|{message}

			if (String.IsNullOrWhiteSpace(output))
				return null;

			try
			{
				Match progressValueMatch = Regex.Match(output, @"([0-9]+)% done");
				if (progressValueMatch.Success)
				{
					// This is a progress update
					int progress = Convert.ToInt32(progressValueMatch.Groups[1].Value.Replace("% done", ""));
					int filesChecked = 0, totalFiles = 0;
					Match filesProgressMatch = Regex.Match(output, @"([0-9]+)/([0-9]+) files checked");
					if (filesProgressMatch.Success)
					{
						filesChecked = Convert.ToInt32(filesProgressMatch.Groups[1].ToString());
						totalFiles = Convert.ToInt32(filesProgressMatch.Groups[2].ToString());
					}
					onProgressUpdated(progress, filesChecked, totalFiles);

					if (_unfinishedProblem == null)
					{
						return null;
					}
					List<Problem> list = new List<Problem>();
					list.Add(_unfinishedProblem); // Done with the current message
					_unfinishedProblem = null;
					return list;
				}
			}
			catch (System.Exception) { }

			if (output.StartsWith("Checking "))
			{
				if (_unfinishedProblem == null)
				{
					return null;
				}
				List<Problem> list = new List<Problem>();
				list.Add(_unfinishedProblem); // Done with the current message
				_unfinishedProblem = null;
				return list;
			}
			else if (!output.Contains("|")) // This line does not represent a new defect found by cppcheck; could be continuation of a multi-line issue description
			{
				if (_unfinishedProblem != null)
					_unfinishedProblem.Message += "\n" + output;

				return null; // Not done with the current message yet
			}

			String[] parsed = output.Split('|');
			if (parsed.Length != 5)
				return null;

			// New issue found - finalize the previous one
			List<Problem> result = new List<Problem>();
			if (_unfinishedProblem != null)
				result.Add(_unfinishedProblem);

			Problem.SeverityLevel severity = Problem.SeverityLevel.info;
			if (parsed[2] == "error")
				severity = Problem.SeverityLevel.error;
			else if (parsed[2] == "warning")
				severity = Problem.SeverityLevel.warning;

			_unfinishedProblem = new Problem(this, severity, parsed[3], parsed[4], parsed[0], String.IsNullOrWhiteSpace(parsed[1]) ? 0 : Int32.Parse(parsed[1]), _projectBasePath, _projectName);

			MainToolWindow.Instance.bringToFront();

			return result;
		}

		protected override void analysisFinished(string arguments)
		{
			if (_unfinishedProblem != null)
				addProblemToToolwindow(_unfinishedProblem);

			const string fileListPattern = "--file-list=\"";
			int filenamePos = arguments.IndexOf(fileListPattern) + fileListPattern.Length;
			int filenameLength = arguments.IndexOf('\"', filenamePos) - filenamePos;
			string tempFileName = arguments.Substring(filenamePos, filenameLength);

			File.Delete(tempFileName);
		}

		private void cleanupTempFiles()
		{
			// Delete the temp files. Doesn't throw an exception if the file was never
			// created, so we don't need to worry about that.
			foreach (string name in _tempFileNamesInUse)
				File.Delete(name);

			_tempFileNamesInUse.Clear();
		}

		private string createNewTempFileName()
		{
			string name = Path.GetTempPath() + tempFilePrefix + "_" + Path.GetRandomFileName();
			_tempFileNamesInUse.Add(name);
			return name;
		}

		private Problem _unfinishedProblem = null;
		private List<string> _tempFileNamesInUse = new List<string>();
	}
}
