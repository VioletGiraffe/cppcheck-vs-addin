using System.Collections.Generic;
using System;
using EnvDTE;
using System.Diagnostics;

namespace VSPackage.CPPCheckPlugin
{
	abstract class ICodeAnalyzer : IDisposable
	{
		protected ICodeAnalyzer()
		{
			_numCores = Environment.ProcessorCount;
		}

		~ICodeAnalyzer()
		{
			Dispose(false);
		}

		public abstract void analyze(List<SourceFile> filesToAnalyze, OutputWindowPane outputPane, bool is64bitConfiguration,
			bool isDebugConfiguration, bool bringOutputToFrontAfterAnalysis);

		protected abstract HashSet<string> readSuppressions(string projectBasePath);

		protected void run(string analyzerExePath, string arguments, OutputWindowPane outputPane, bool bringOutputToFrontAfterAnalysis)
		{
			_outputPane = outputPane;
			if (_thread != null)
			{

				try
				{
					_thread.Abort();
				}
				catch (System.Exception /*ex*/) { }
				_thread = null;
			}

			_thread = new System.Threading.Thread(() => analyzerThreadFunc(analyzerExePath, arguments, bringOutputToFrontAfterAnalysis));
			_thread.Name = "cppcheck";
			_thread.Start();
		}

		private void analyzerThreadFunc(string analyzerExePath, string arguments, bool bringOutputToFrontAfterAnalysis)
		{
			System.Diagnostics.Process process = null;
			try
			{
				Debug.Assert(!String.IsNullOrEmpty(analyzerExePath));
				Debug.Assert(!String.IsNullOrEmpty(arguments));
				Debug.Assert(_outputPane != null);
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
				process.OutputDataReceived += new DataReceivedEventHandler(this.analyzerOutputHandler);
				process.ErrorDataReceived += new DataReceivedEventHandler(this.analyzerOutputHandler);

				_outputPane.OutputString("Starting analyzer with arguments: " + arguments + "\n");

				var timer = Stopwatch.StartNew();
				// Start the process.
				process.Start();

				// Start the asynchronous read of the sort output stream.
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();
				// Wait for analysis completion
				process.WaitForExit();
				timer.Stop();
				float timeElapsed = timer.ElapsedMilliseconds / 1000.0f;
				if (process.ExitCode != 0)
					_outputPane.OutputString(analyzerExePath + " has exited with code " + process.ExitCode.ToString() + "\n");
				else
					_outputPane.OutputString("Analysis completed in " + timeElapsed.ToString() + " seconds\n");
				process.Close();
				process = null;
				if (bringOutputToFrontAfterAnalysis)
				{
					Window outputWindow = _outputPane.DTE.GetOutputWindow();
					outputWindow.Visible = true;
					_outputPane.Activate();
				}
			}
			catch (System.Exception /*ex*/) { }
			finally
			{
				if (process != null)
				{
					try
					{
						process.Kill();
					}
					catch (Exception) { }
					process.Dispose();
				}
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

		public void Dispose()
		{
			Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_thread != null)
			{
				_thread.Abort();
				_thread = null;
			}
		}

		private OutputWindowPane _outputPane = null;
		protected int _numCores;

		private System.Threading.Thread _thread = null;
	}
}
