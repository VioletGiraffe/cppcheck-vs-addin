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
            String cppheckargs = @"--enable=style,information --template=vs --check-config";
            Debug.Assert(filesToAnalyze.Count == 1);
            foreach (SourceFile file in filesToAnalyze)
            {
                foreach (string path in file.IncludePaths)
                {
                    String includeArgument = @" -I""" + path + @"""";
                    cppheckargs += (" " + includeArgument);
                }
                cppheckargs += @" """ + file.FilePath + @"""";
            }

            run(_analyzerPath, cppheckargs, outputWindow);
        }

        const string _analyzerPath = "c:\\Program Files (x86)\\Cppcheck\\cppcheck.exe";
    }
}
