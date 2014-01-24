using EnvDTE;

namespace VSPackage.CPPCheckPlugin
{
	public static class DTEHelper
	{
		public static Window GetOutputWindow(this DTE dte)
		{
			return dte.Windows.Item(Constants.vsWindowKindOutput);
		}
	}
}

