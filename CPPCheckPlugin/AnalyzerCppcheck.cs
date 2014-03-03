using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace VSPackage.CPPCheckPlugin
{
	class AnalyzerCppcheck : ICodeAnalyzer
	{
		public override void analyze(List<SourceFile> filesToAnalyze, OutputWindowPane outputWindow, bool is64bitConfiguration,
			bool isDebugConfiguration)
		{
			if (filesToAnalyze.Count == 0)
				return;

			Debug.Assert(_numCores > 0);
			String cppheckargs = Properties.Settings.Default.DefaultArguments;

			if (Properties.Settings.Default.SeveritiesString.Length != 0)
				cppheckargs += " --enable=" + Properties.Settings.Default.SeveritiesString;

			HashSet<string> suppressions = new HashSet<string>(Properties.Settings.Default.SuppressionsString.Split(','));
			suppressions.Add("unmatchedSuppression");

			// Creating the list of all different project locations (no duplicates)
			HashSet<string> projectPaths = new HashSet<string>(); // enforce uniqueness on the list of project paths
			foreach (var file in filesToAnalyze)
			{
				projectPaths.Add(file.BaseProjectPath);
			}

			Debug.Assert(projectPaths.Count == 1);
			_projectBasePath = projectPaths.First();

			// Creating the list of all different suppressions (no duplicates)
			foreach (var path in projectPaths)
			{
				suppressions.UnionWith(readSuppressions(path));
			}

			cppheckargs += (" --relative-paths=\"" + filesToAnalyze[0].BaseProjectPath + "\"");
			cppheckargs += (" -j " + _numCores.ToString());
			if (Properties.Settings.Default.InconclusiveChecksEnabled)
				cppheckargs += " --inconclusive ";

			foreach (string suppression in suppressions)
			{
				if (!String.IsNullOrWhiteSpace(suppression))
					cppheckargs += (" --suppress=" + suppression);
			}

			// We only add include paths once, and then specify a set of files to check
			HashSet<string> includePaths = new HashSet<string>();
			foreach (var file in filesToAnalyze)
			{
				includePaths.UnionWith(file.IncludePaths);
			}

			includePaths.Add(filesToAnalyze[0].BaseProjectPath); // Fix for #60

			foreach (string path in includePaths)
			{
				if (!path.ToLower().Contains("qt")) // TODO: make ignore include path setting
				{
					String includeArgument = " -I\"" + path + "\"";
					cppheckargs = cppheckargs + " " + includeArgument;
				}
			}

			foreach (SourceFile file in filesToAnalyze)
			{
				cppheckargs += " \"" + file.FilePath + "\"";
			}

			if ((filesToAnalyze.Count == 1 && Properties.Settings.Default.FileOnlyCheckCurrentConfig) || (filesToAnalyze.Count > 1 && Properties.Settings.Default.ProjectOnlyCheckCurrentConfig)) // Only checking current macros configuration (for speed)
			{
				cppheckargs.Replace("--force", "");
				// Creating the list of all different macros (no duplicates)
				HashSet<string> macros = new HashSet<string>();
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
					case SourceFile.VCCompilerVersion.vcFuture:
						macros.Add("_MSC_VER=1900");
						break;
				}

				foreach (var file in filesToAnalyze)
				{
					macros.UnionWith(file.Macros);
				}
				macros.Add("WIN32");
				macros.Add("_WIN32");

				if (is64bitConfiguration)
				{
					macros.Add("_M_X64");
					macros.Add("_WIN64");
				}
				else
				{
					macros.Add("_M_IX86");
				}

				if (isDebugConfiguration)
					macros.Add("_DEBUG");

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

			string analyzerPath = Properties.Settings.Default.CPPcheckPath;
			while (!File.Exists(analyzerPath))
			{
				OpenFileDialog dialog = new OpenFileDialog();
				dialog.Filter = "cppcheck executable|cppcheck.exe";
				if (dialog.ShowDialog() != DialogResult.OK)
					return;

				analyzerPath = dialog.FileName;
			}

			Properties.Settings.Default["CPPcheckPath"] = analyzerPath;
			Properties.Settings.Default.Save();
			run(analyzerPath, cppheckargs, outputWindow);
		}

		public override void suppressProblem(Problem p, SuppressionScope scope)
		{
			if (p == null)
				return;
			if (!Directory.Exists(p.BaseProjectPath))
			{
				Debug.WriteLine("Error: directory " + p.BaseProjectPath + " doesn't exist, can't add suppression");
				return;
			}

			String suppressionLine = null;
			switch (scope)
			{
			case ICodeAnalyzer.SuppressionScope.suppressAllMessagesThisFile:
				suppressionLine = "*:" + p.FileName;
				break;
			case ICodeAnalyzer.SuppressionScope.suppressThisMessageFileLine:
				suppressionLine = p.MessageId + ":" + p.FileName + ":" + p.Line;
				break;
			case ICodeAnalyzer.SuppressionScope.suppressThisMessageFileOnly:
				suppressionLine = p.MessageId + ":" + p.FileName;
				break;
			case ICodeAnalyzer.SuppressionScope.suppressThisMessageGlobally:
				suppressionLine = p.MessageId; // TODO:
				break;
			case ICodeAnalyzer.SuppressionScope.suppressThisMessageProjectOnly:
				suppressionLine = p.MessageId;
				break;
			}

			String suppresionsFilePath = p.BaseProjectPath + "\\suppressions.cfg";
			List<String> contentsLines = new List<String>();
			if (File.Exists(suppresionsFilePath))
				contentsLines = File.ReadAllLines(suppresionsFilePath).ToList();

			int lineToInsertAfter = -1;
			for (int i = 0; i < contentsLines.Count; ++i)
			{
				if (!String.IsNullOrWhiteSpace(contentsLines[i]) && contentsLines[i].Contains("[cppcheck]"))
				{
					lineToInsertAfter = i;
					break;
				}
			}
			if (lineToInsertAfter == -1)
			{
				contentsLines.Add("[cppcheck]");
				lineToInsertAfter = 0;
			}

			contentsLines.Insert(lineToInsertAfter+1, suppressionLine);
			File.WriteAllLines(suppresionsFilePath, contentsLines);
		}

		protected override HashSet<string> readSuppressions(string projectBasePath)
		{
			string suppresionsFilePath = projectBasePath + "\\suppressions.cfg";
			HashSet<string> suppressions = new HashSet<string>();
			if (File.Exists(suppresionsFilePath))
			{
				using( StreamReader stream = File.OpenText(suppresionsFilePath) )
				{
					string currentGroup = "";
					while (true)
					{
						var line = stream.ReadLine();
						if (line == null)
						{
							break;
						}
						if (line.Contains("["))
						{
							currentGroup = line.Replace("[", "").Replace("]", "");
							continue; // to the next line
						}
						if (currentGroup == "cppcheck")
						{
							var components = line.Split(':');
							if (components.Length >= 2 && !components[1].StartsWith("*"))           // id and some path without "*"
								components[1] = "*" + components[1]; // adding * in front

							string suppression = components[0];
							if (components.Length > 1)
								suppression += ":" + components[1];
							if (components.Length > 2)
								suppression += ":" + components[2];

							if (!string.IsNullOrEmpty(suppression))
								suppressions.Add(suppression.Replace("\\\\", "\\"));
						}
					}
				}
			}
			return suppressions;
		}

		protected override List<Problem> parseOutput(String output)
		{
			// template={file}|{line}|{severity}|{id}|{message}

			List<Problem> list = new List<Problem>();

			if (String.IsNullOrWhiteSpace(output) || !output.Contains("|"))
				return list;

			String[] parsed = output.Split('|');
			if (parsed.Length != 5)
				return list;

			Problem.SeverityLevel severity = Problem.SeverityLevel.info;
			if (parsed[2] == "error")
				severity = Problem.SeverityLevel.error;
			else if (parsed[2] == "warning")
				severity = Problem.SeverityLevel.warning;

			list.Add(new Problem(this, severity, parsed[3], parsed[4], parsed[0], String.IsNullOrWhiteSpace(parsed[1]) ? 0 : Int32.Parse(parsed[1]), _projectBasePath));
			return list;
		}
	}
}
