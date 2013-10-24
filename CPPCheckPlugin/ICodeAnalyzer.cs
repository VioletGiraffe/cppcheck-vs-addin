using System.Collections.Generic;
using System.Text;
using System;
using EnvDTE;
using System.Threading;
using System.Diagnostics;
using System.Management;

namespace VSPackage.CPPCheckPlugin
{
    abstract class ICodeAnalyzer
    {
		protected ICodeAnalyzer()
		{
			_numCores = Environment.ProcessorCount;
		}

		public abstract void analyze(List<SourceFile> filesToAnalyze, OutputWindowPane outputWindow, bool is64bitConfiguration);

		public void analyze(SourceFile fileToAnalyze, OutputWindowPane outputWindow, bool is64bitConfiguration)
		{
			List<SourceFile> list = new List<SourceFile>();
			list.Add(fileToAnalyze);
			analyze(list, outputWindow, is64bitConfiguration);
		}

		protected abstract HashSet<string> readSuppressions(string projectBasePath);

		protected void run(string analyzerExePath, string arguments, OutputWindowPane outputWindow)
		{
			_outputWindow = outputWindow;
			try
			{
				_process.Kill();
				_thread.Abort();
			}
			catch (System.Exception /*ex*/) {}

			_process = new System.Diagnostics.Process(); // Reusing the same process instance seems to not be possible because of BeginOutputReadLine and BeginErrorReadLine
			_thread = new System.Threading.Thread(() => analyzerThreadFunc(analyzerExePath, arguments));
			_thread.Name = "cppcheck";
			_thread.Start();
		}

		private void analyzerThreadFunc(string analyzerExePath, string arguments)
		{
			try
			{
				Debug.Assert(!String.IsNullOrEmpty(analyzerExePath) && !String.IsNullOrEmpty(arguments) && _outputWindow != null);
				_process.StartInfo.FileName = analyzerExePath;
				_process.StartInfo.Arguments = arguments;
				_process.StartInfo.CreateNoWindow = true;

				// Set UseShellExecute to false for output redirection.
				_process.StartInfo.UseShellExecute = false;

				// Redirect the standard output of the command.
				_process.StartInfo.RedirectStandardOutput = true;
				_process.StartInfo.RedirectStandardError = true;

				// Set our event handler to asynchronously read the sort output.
				_process.OutputDataReceived += new DataReceivedEventHandler(this.analyzerOutputHandler);
				_process.ErrorDataReceived += new DataReceivedEventHandler(this.analyzerOutputHandler);

				var timer = Stopwatch.StartNew();
				// Start the process.
				_process.Start();

				// Start the asynchronous read of the sort output stream.
				_process.BeginOutputReadLine();
				_process.BeginErrorReadLine();
				// Wait for analysis completion
				_process.WaitForExit();
				timer.Stop();
				float timeElapsed = timer.ElapsedMilliseconds / 1000.0f;
				if (_process.ExitCode != 0)
					_outputWindow.OutputString(analyzerExePath + " has exited with code " + _process.ExitCode.ToString() + "\n");
				else
					_outputWindow.OutputString("Analysis completed in " + timeElapsed.ToString() + " seconds\n");
				_process.Close();

			} catch (System.Exception ex) {
				
			}
		}

		private void analyzerOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
		{
			if (!String.IsNullOrEmpty(outLine.Data))
			{
				String output = outLine.Data;
				_outputWindow.OutputString(output + "\n");
			}
		}

		private OutputWindowPane _outputWindow = null;

		protected int _numCores;

		private static System.Diagnostics.Process _process = new System.Diagnostics.Process();
		private static System.Threading.Thread _thread = null;
	}
}
