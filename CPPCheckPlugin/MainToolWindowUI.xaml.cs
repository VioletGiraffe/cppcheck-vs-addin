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
using System.IO;

namespace VSPackage.CPPCheckPlugin
{
	/// <summary>
	/// Interaction logic for MainToolWindowUI.xaml
	/// </summary>
	public partial class MainToolWindowUI : UserControl
	{
		public class SuppresssionRequestedEventArgs : EventArgs
		{
			public SuppresssionRequestedEventArgs(Problem p, ICodeAnalyzer.SuppressionScope scope) { Problem = p; Scope = scope; }
			public Problem Problem { get; set; }
			public ICodeAnalyzer.SuppressionScope Scope { get; set; }
		}

		public class OpenProblemInEditorEventArgs : EventArgs
		{
			public OpenProblemInEditorEventArgs(Problem p) { Problem = p; }
			public Problem Problem { get; set; }
		}

		public delegate void suppresssionRequestedHandler(object sender, SuppresssionRequestedEventArgs e);
		public delegate void openProblemInEditor(object sender, OpenProblemInEditorEventArgs e);

		public event suppresssionRequestedHandler SuppressionRequested;
		public event openProblemInEditor EditorRequestedForProblem;

		private static int iconSize = 20;

		public MainToolWindowUI()
		{
			InitializeComponent();
		}

		private void menuItem_suppressThisMessageGlobally(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageGlobally);
		}

		private void menuItem_suppressThisMessageProjectOnly(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageProjectOnly);
		}

		private void menuItem_suppressThisMessageFileOnly(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageFileOnly);
		}

		private void menuItem_suppressThisMessageFileLine(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageFileLine);
		}

		private void menuItem_suppressAllMessagesThisFile(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressAllMessagesThisFile);
		}

		private void menuItem_suppressThisMessageSolutionWide(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageSolutionWide);
		}

		private void menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope scope)
		{
			var selectedItems = listView.SelectedItems;
			foreach (ProblemsListItem item in selectedItems)
			{
				if (item != null)
					SuppressionRequested(this, new SuppresssionRequestedEventArgs(item.Problem, scope));
			}
		}

		private void onProblemDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var objectClicked = FindVisualParent<ListViewItem, ListView>(e.OriginalSource as DependencyObject);
			if (objectClicked == null)
				return;

			ProblemsListItem item = listView.ItemContainerGenerator.ItemFromContainer(objectClicked) as ProblemsListItem;
			if (item != null)
				EditorRequestedForProblem(this, new OpenProblemInEditorEventArgs(item.Problem));
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

			public String FileName
			{
				get { return System.IO.Path.GetFileName(_problem.FilePath); }
			}

			public int Line
			{
				get { return _problem.Line; }
			}

			public ImageSource Icon
			{
				get
				{
					Bitmap bitmap = null;
					switch (_problem.Severity)
					{
						case Problem.SeverityLevel.info:
							bitmap = resizeBitmap(new System.Drawing.Icon(SystemIcons.Information, SystemIcons.Information.Height, SystemIcons.Information.Width).ToBitmap(), iconSize, iconSize);
							break;
						case Problem.SeverityLevel.warning:
							bitmap = resizeBitmap(new System.Drawing.Icon(SystemIcons.Warning, SystemIcons.Warning.Height, SystemIcons.Warning.Width).ToBitmap(), iconSize, iconSize);
							break;
						case Problem.SeverityLevel.error:
							bitmap = resizeBitmap(new System.Drawing.Icon(SystemIcons.Error, SystemIcons.Error.Height, SystemIcons.Error.Width).ToBitmap(), iconSize, iconSize);
							break;
						default:
							throw new InvalidOperationException("Unsupported value: " + _problem.Severity.ToString());
					}
					ImageSource imgSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bitmap.Width, bitmap.Height));
					return imgSource;
				}
			}

			public Problem Problem
			{
				get { return _problem; }
			}

			private static Bitmap resizeBitmap(Bitmap src, int destWidth, int destHeight)
			{
				Bitmap dest = new Bitmap(destWidth, destHeight);
				using (Graphics g = Graphics.FromImage((System.Drawing.Image)dest))
				{
					g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					g.DrawImage(src, 0, 0, destWidth, destHeight);
				}
				return dest;
			}

			Problem _problem;
		}
	}
}
