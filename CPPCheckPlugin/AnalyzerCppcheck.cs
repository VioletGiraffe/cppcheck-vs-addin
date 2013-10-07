using System;
using System.Collections.Generic;
using System.Text;
using EnvDTE;
using System.Diagnostics;

namespace VSPackage.CPPCheckPlugin
{
    class AnalyzerCppcheck : ICodeAnalyzer
    {
        public override void analyze(List<SourceFile> filesToAnalyze, OutputWindowPane outputWindow)
        {
            Debug.Assert(filesToAnalyze.Count == 1);
            Debug.Assert(_numCores > 0);
            String cppheckargs = "";
//             string[] problematicQtMacros = { "Q_DECL_EXPORT", "Q_CORE_EXPORT", "Q_GUI_EXPORT", "Q_WIDGETS_EXPORT" };
//             foreach (string macro in problematicQtMacros)
//             {
//                 cppheckargs += " -D" + macro;
//             }

            String[] suppressions = { "cstyleCast", "missingIncludeSystem", "unusedStructMember", "unmatchedSuppression", "class_X_Y", "missingInclude"};
            cppheckargs += (@"--enable=style,information,warning,performance,portability --template=vs --force --quiet -j " + _numCores.ToString());
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
				String includeArgument = @" -I""" + path + @"""";
				cppheckargs = cppheckargs + " " + includeArgument;
			}

            foreach (SourceFile file in filesToAnalyze)
            {
                cppheckargs += @" """ + file.FilePath + @"""";
            }

            run(_analyzerPath, cppheckargs, outputWindow);
        }

        const string _analyzerPath = "c:\\Program Files (x86)\\Cppcheck\\cppcheck.exe";
    }
}
