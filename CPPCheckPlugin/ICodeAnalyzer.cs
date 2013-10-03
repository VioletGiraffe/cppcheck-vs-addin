using System.Collections.Generic;
using System.Text;
using System;
using EnvDTE;
using System.Diagnostics;

namespace VSPackage.CPPCheckPlugin
{
    abstract class ICodeAnalyzer
    {
        public abstract void analyze(List<SourceFile> filesToAnalyze, OutputWindowPane outputWindow);

        public void analyze(SourceFile fileToAnalyze, OutputWindowPane outputWindow)
        {
            List<SourceFile> list = new List<SourceFile>();
            list.Add(fileToAnalyze);
            analyze(list, outputWindow);
        }

        protected void run(string analyzerExePath, string arguments, OutputWindowPane outputWindow)
        {
            Debug.Assert(!String.IsNullOrEmpty(analyzerExePath) && !String.IsNullOrEmpty(arguments) && outputWindow != null);

            _outputWindow = outputWindow;

            System.Diagnostics.Process process;
            process = new System.Diagnostics.Process();
            process.StartInfo.FileName = analyzerExePath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;

            // Set UseShellExecute to false for output redirection.
            process.StartInfo.UseShellExecute = false;

            // Redirect the standard output of the command.   
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            // Set our event handler to asynchronously read the sort output.
            process.OutputDataReceived += new DataReceivedEventHandler(analyzerOutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(analyzerOutputHandler);

            // Start the process.
            process.Start();

            // Start the asynchronous read of the sort output stream.
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            // Wait for analysis completion
            process.WaitForExit();
            if (process.ExitCode != 0)
                _outputWindow.OutputString("The tool " + analyzerExePath + " has exited with code " + process.ExitCode.ToString() + "\n");
            process.Close();
        }

        private static void analyzerOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                String output = outLine.Data;
                _outputWindow.OutputString(output + "\n");
            }
        }

        private static OutputWindowPane _outputWindow = null;
    }
}
