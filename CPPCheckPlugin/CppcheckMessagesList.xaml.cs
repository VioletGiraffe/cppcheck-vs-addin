using System.Windows;

namespace VSPackage.CPPCheckPlugin
{
	/// <summary>
	/// Interaction logic for CppcheckMessagesList.xaml
	/// </summary>
	public partial class CppcheckMessagesList : Window
	{
		public CppcheckMessagesList()
		{
			InitializeComponent();

			var panel = new ChecksPanel(Checks_Panel);
			panel.LoadSettings();
		}
	}
}
