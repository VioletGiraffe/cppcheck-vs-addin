using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VSPackage.CPPCheckPlugin
{
	/// <summary>
	/// Interaction logic for MainToolWindowUI.xaml
	/// </summary>
	public partial class MainToolWindowUI : UserControl
	{
		public delegate void suppresssionRequestedHandler(Problem problemToSuppress, ICodeAnalyzer.SuppressionScope scope);
		public delegate void openProblemInEditor(Problem problem);

		public event suppresssionRequestedHandler SuppressionRequested;
		public event openProblemInEditor EditorRequestedForProblem;

		public MainToolWindowUI()
		{
			InitializeComponent();
		}

		private void menuItem_suppressThisMessageGlobally(object sender, RoutedEventArgs e)
		{
			foreach (ProblemsListItem item in listView.SelectedItems)
			{
				if (item != null)
					SuppressionRequested(item.Problem, ICodeAnalyzer.SuppressionScope.suppressThisMessageGlobally);
			}
		}

		private void menuItem_suppressThisMessageProjectOnly(object sender, RoutedEventArgs e)
		{

		}

		private void menuItem_suppressThisMessageFileOnly(object sender, RoutedEventArgs e)
		{

		}

		private void menuItem_suppressThisMessageFileLine(object sender, RoutedEventArgs e)
		{

		}

		private void menuItem_suppressAllMessagesThisFile(object sender, RoutedEventArgs e)
		{

		}

		private void onProblemDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var objectClicked = FindVisualParent<ListViewItem, ListView>(e.OriginalSource as DependencyObject);
			if (objectClicked == null)
				return;

			ProblemsListItem item = listView.ItemContainerGenerator.ItemFromContainer(objectClicked) as ProblemsListItem;
			if (item != null)
				EditorRequestedForProblem(item.Problem);
		}

		public static TParent FindVisualParent<TParent, TLimit>(DependencyObject obj) where TParent : DependencyObject
		{
			while (obj != null && !(obj is TParent))
			{
				if (obj is TLimit)
					return null;
				obj = VisualTreeHelper.GetParent(obj);
			}
			return obj as TParent;
		}

		public class ProblemsListItem
		{
			public ProblemsListItem(Problem problem)
				: base()
			{
				_problem = problem;
				Debug.Assert(problem != null);
			}

			public String Message
			{
				get { return _problem.Message; }
			}

			public ImageSource Icon
			{
				get
				{
					Bitmap bitmap = null;
					switch (_problem.Severity)
					{
						case Problem.SeverityLevel.info:
							bitmap = new System.Drawing.Icon(SystemIcons.Information, SystemIcons.Information.Height, SystemIcons.Information.Width).ToBitmap();
							break;
						case Problem.SeverityLevel.warning:
							bitmap = new System.Drawing.Icon(SystemIcons.Warning, SystemIcons.Warning.Height, SystemIcons.Warning.Width).ToBitmap();
							break;
						case Problem.SeverityLevel.error:
							bitmap = new System.Drawing.Icon(SystemIcons.Error, SystemIcons.Error.Height, SystemIcons.Error.Width).ToBitmap();
							break;
						default:
							bitmap = new System.Drawing.Icon(SystemIcons.Information, SystemIcons.Information.Height, SystemIcons.Information.Width).ToBitmap();
							break;
					}
					ImageSource imgSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bitmap.Width, bitmap.Height));
					return imgSource;
				}
			}

			public Problem Problem
			{
				get { return _problem; }
			}

			Problem _problem;
		}
	}
}
