using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Windows.Controls;

namespace VSPackage.CPPCheckPlugin
{
	[Guid("98C5C8D0-34D9-406F-AA2E-C85B47C9F268")]
	public class MainToolWindow : ToolWindowPane
	{
		public MainToolWindow() : base(null)
		{
			_listView = _ui.listView;

			Caption = "Cppcheck analysis results";
			Content = _ui;

		}

		private void itemActivated(object sender, EventArgs e)
		{
			// Determine which item was clicked/activated
			var item = _listView.SelectedItems[0];
		}

		private MainToolWindowUI _ui = new MainToolWindowUI();
		private ListView _listView = null;
	}
}
