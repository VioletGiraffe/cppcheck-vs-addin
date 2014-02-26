using System;
using System.Windows;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.IO;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSPackage.CPPCheckPlugin
{
	[Guid("98C5C8D0-34D9-406F-AA2E-C85B47C9F268")]
	public class MainToolWindow : ToolWindowPane
	{
		public MainToolWindow() : base(null)
		{
			Debug.Assert(_instance == null); // Only 1 instance of this tool window makes sense
			_instance = this;

			_listView = _ui.listView;
			_ui.EditorRequestedForProblem += openProblemInEditor;
			_ui.SuppressionRequested += suppressProblem;

			Caption = "Cppcheck analysis results";
			Content = _ui;
		}

		public static MainToolWindow Instance
		{
			get{return _instance;}
		}

		public void show()
		{
			IVsWindowFrame frame = Frame as IVsWindowFrame;
			if (frame != null)
				frame.Show();
		}

		public void clear()
		{
			_listView.Items.Clear();
		}

		public bool isEmpty()
		{
			return _listView.Items.Count == 0;
		}

		public void displayProblem(Problem problem)
		{
			Application.Current.Dispatcher.BeginInvoke(new Action(()=>_listView.Items.Add(new MainToolWindowUI.ProblemsListItem(problem))));
		}

		public ICodeAnalyzer.AnalysisType ContentsType
		{
			get;
			set;
		}

		private void openProblemInEditor(object sender, Problem problem)
		{
			IVsUIShellOpenDocument shellOpenDocument = (IVsUIShellOpenDocument)GetService(typeof(IVsUIShellOpenDocument));
			Debug.Assert(shellOpenDocument != null);
			Guid guidCodeView = VSConstants.LOGVIEWID.Code_guid;
			Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp = null;
			IVsUIHierarchy hierarchy = null;
			uint itemId = 0;
			IVsWindowFrame windowFrame = null;
			if (shellOpenDocument.OpenDocumentViaProject(problem.FilePath, ref guidCodeView, out sp, out hierarchy, out itemId, out windowFrame) != VSConstants.S_OK)
			{
				Debug.WriteLine("Error opening file " + problem.FilePath);
				return;
			}

			if (windowFrame != null)
				windowFrame.Show();

			EnvDTE.DTE dte = (EnvDTE.DTE)GetService(typeof(SDTE));
			Debug.Assert(dte != null);
			Debug.Assert(dte.ActiveDocument != null);
			EnvDTE.TextSelection selection = dte.ActiveDocument.Selection as EnvDTE.TextSelection;
			Debug.Assert(selection != null);
			selection.GotoLine(problem.Line);
		}

		private void suppressProblem(object sender, Problem problem, ICodeAnalyzer.SuppressionScope scope)
		{
			
		}

		private MainToolWindowUI _ui = new MainToolWindowUI();
		private ListView _listView = null;
		private static MainToolWindow _instance = null;
	}
}
