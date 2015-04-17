using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VSPackage.CPPCheckPlugin.SuppressionsSettingsUI
{
	/// <summary>
	/// Interaction logic for ItemsListEditor.xaml
	/// </summary>
	public partial class ItemsListEditor : UserControl
	{
		public ItemsListEditor()
		{
			InitializeComponent();
		}

		public string Label
		{
			set { HeaderLabel.Content = value; }
		}

		public HashSet<string> Items
		{
			set { ItemsListText.Text = string.Join("\n", value); }
			get {
				var text = ItemsListText.Text;
				var splitted = text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
				return new HashSet<string>(splitted);
			}
		}
	}
}
