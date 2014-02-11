using System;
using System.Collections.Generic;
using EnvDTE;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace VSPackage.CPPCheckPlugin
{
	class AnalyzerCppcheck : ICodeAnalyzer
	{
		public override void analyze(List<SourceFile> filesToAnalyze, OutputWindowPane outputWindow, bool is64bitConfiguration,
			bool isDebugConfiguration, bool bringOutputToFrontAfterAnalysis)
		{
			if (filesToAnalyze.Count == 0)
				return;

			Debug.Assert(_numCores > 0);
			String cppheckargs = CppcheckSettings.DefaultArguments;

			HashSet<string> suppressions = new HashSet<string> { "passedByValue", "cstyleCast", "missingIncludeSystem", "unusedStructMember", "unmatchedSuppression", "class_X_Y", "missingInclude", "constStatement", "unusedPrivateFunction" };

			// Creating the list of all different project locations (no duplicates)
			HashSet<string> projectPaths = new HashSet<string>(); // enforce uniqueness on the list of project paths
			foreach (var file in filesToAnalyze)
			{
				projectPaths.Add(file.BaseProjectPath);
			}

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
				cppheckargs += (" --suppress=" + suppression);
			}

			// We only add include paths once, and then specify a set of files to check
			HashSet<string> includePaths = new HashSet<string>();
			foreach (var file in filesToAnalyze)
				foreach (string path in file.IncludePaths)
				{
					includePaths.Add(path);
				}

			foreach (string path in includePaths)
			{
				if (!path.ToLower().Contains("qt"))
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
				// Creating the list of all different macros (no duplicates)
				HashSet<string> macros = new HashSet<string>();
				macros.Add("__cplusplus=199711L"); // At least in VS2012, this is still 199711L
				// Assuming all files passed here are from the same project / same toolset, which should be true, so peeking the first file for global settings
				switch (filesToAnalyze[0].vcCompilerVersion)
				{
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
					foreach (string macro in file.Macros)
						macros.Add(macro);
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
					String macroArgument = " -D" + macro;
					cppheckargs += macroArgument;
				}
			}

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
			run(analyzerPath, cppheckargs, outputWindow, bringOutputToFrontAfterAnalysis);
		}

		protected override HashSet<string> readSuppressions(string projectBasePath)
		{
			string settingsFilePath = projectBasePath + "\\suppressions.cfg";
			HashSet<string> suppressions = new HashSet<string>();
			if (File.Exists(settingsFilePath))
			{
				StreamReader stream = File.OpenText(settingsFilePath);
				string line = null;

				string currentGroup = "";
				while ((line = stream.ReadLine()) != null)
				{
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
							suppression += ":"+ components[2];

						if (!string.IsNullOrEmpty(suppression))
							suppressions.Add(suppression.Replace("\\\\", "\\"));
					}
				}
			}

			return suppressions;
		}

		protected virtual void Dispose(bool disposing)
		{
			base.Dispose(disposing);
		}
	}
}
