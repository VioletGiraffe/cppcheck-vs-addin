using EnvDTE;

namespace VSPackage.CPPCheckPlugin
{
	public static class DTEHelper
	{
		public static Window GetOutputWindow(this DTE dte)
		{
			return dte.Windows.Item(Constants.vsWindowKindOutput);
		}

		public static OutputWindowPane AddOutputWindowPane(this DTE dte, string name)
		{
			var outputWindow = (OutputWindow)dte.GetOutputWindow().Object;
			var newPane = outputWindow.OutputWindowPanes.Add(name);
			return newPane;
		}
	}
}

