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

            String[] suppressions = { "cstyleCast", "missingIncludeSystem", "unusedStructMember", "unmatchedSuppression"};
            String cppheckargs = @"--enable=style,information,warning,performance,portability --template=vs --force --quiet -j " + _numCores.ToString();
            foreach (string suppression in suppressions)
            {
                cppheckargs += (" --suppress=" + suppression);
            }

            // For the sake of simplicity, assume all the files in the list are from the same project
            // So they share a common set of include paths
            // Let's assert this
            bool includePathsAreCommon = true;
            foreach (SourceFile file1 in filesToAnalyze)
            {
                foreach (SourceFile file2 in filesToAnalyze)
                {
                    if (file1 != file2)
                        foreach (string path in file1.IncludePaths)
                        {
                            if (!includePathsAreCommon)
                                break;

                            if (!file2.IncludePaths.Contains(path))
                            {
                                includePathsAreCommon = false;
                                break;
                            }
                        }
                }
            }
            Debug.Assert(includePathsAreCommon);

            // So we can only add include paths once, and then specify a set of files to check
            if (filesToAnalyze.Count > 0)
                foreach (string path in filesToAnalyze[0].IncludePaths)
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
