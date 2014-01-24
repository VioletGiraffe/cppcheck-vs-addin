using System.Collections.Generic;
using System;
using EnvDTE;
using System.Diagnostics;

namespace VSPackage.CPPCheckPlugin
{
	abstract class ICodeAnalyzer
	{
		protected ICodeAnalyzer()
		{
			_numCores = Environment.ProcessorCount;
		}

		public abstract void analyze(List<SourceFile> filesToAnalyze, OutputWindowPane outputPane, bool is64bitConfiguration,
			bool isDebugConfiguration, bool bringOutputToFrontAfterAnalysis);

		protected abstract HashSet<string> readSuppressions(string projectBasePath);

		protected void run(string analyzerExePath, string arguments, OutputWindowPane outputPane, bool bringOutputToFrontAfterAnalysis)
		{
			_outputPane = outputPane;
			try
			{
				_process.Kill();
				_thread.Abort();
			}
			catch (System.Exception /*ex*/) {}

			_process = new System.Diagnostics.Process(); // Reusing the same process instance seems to not be possible because of BeginOutputReadLine and BeginErrorReadLine
			_thread = new System.Threading.Thread(() => analyzerThreadFunc(analyzerExePath, arguments, bringOutputToFrontAfterAnalysis));
			_thread.Name = "cppcheck";
			_thread.Start();
		}

		private void analyzerThreadFunc(string analyzerExePath, string arguments, bool bringOutputToFrontAfterAnalysis)
		{
			try
			{
				Debug.Assert(!String.IsNullOrEmpty(analyzerExePath));
				Debug.Assert(!String.IsNullOrEmpty(arguments));
				Debug.Assert(_outputPane != null);
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
					_outputPane.OutputString(analyzerExePath + " has exited with code " + _process.ExitCode.ToString() + "\n");
				else
					_outputPane.OutputString("Analysis completed in " + timeElapsed.ToString() + " seconds\n");
				_process.Close();
				if (bringOutputToFrontAfterAnalysis)
				{
					Window outputWindow = _outputPane.DTE.GetOutputWindow();
					outputWindow.Visible = true;
					_outputPane.Activate();
				}
			} catch (System.Exception /*ex*/) {
				
			}
		}

		private void analyzerOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
		{
			String output = outLine.Data;
			if (!String.IsNullOrEmpty(output))
			{
				_outputPane.OutputString(output + "\n");
			}
		}

		private OutputWindowPane _outputPane = null;
		protected int _numCores;

		private static System.Diagnostics.Process _process = new System.Diagnostics.Process();
		private static System.Threading.Thread _thread = null;
	}
}
