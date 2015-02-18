using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;

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

		private void menuItem_suppressThisMessageProjectWide(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessage);
		}

		private void menuItem_suppressThisMessageSolutionWide(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageSolutionWide);
		}

		private void menuItem_suppressThisMessageGlobally(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageGlobally);
		}

		private void menuItem_suppressThisTypeOfMessageGlobally(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisTypeOfMessagesGlobally);
		}
		private void menuItem_suppressThisTypeOfMessageFileWide(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisTypeOfMessageFileWide);
		}

		private void menuItem_suppressThisTypeOfMessageProjectWide(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisTypeOfMessageProjectWide);
		}

		private void menuItem_suppressThisTypeOfMessageSolutionWide(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisTypeOfMessagesSolutionWide);
		}

		private void menuItem_suppressAllMessagesThisFileProjectWide(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressAllMessagesThisFileProjectWide);
		}

		private void menuItem_suppressAllMessagesThisFileSolutionWide(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressAllMessagesThisFileSolutionWide);
		}

		private void menuItem_suppressAllMessagesThisFileGlobally(object sender, RoutedEventArgs e)
		{
			menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressAllMessagesThisFileGlobally);
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
				get { return _problem.FileName; }
			}

			public int Line
			{
				get { return _problem.Line; }
			}

			public ImageSource Icon
			{
				get
				{
					Icon fromIcon = null;
					switch (_problem.Severity)
					{
						case Problem.SeverityLevel.info:
							fromIcon = SystemIcons.Information;
							break;
						case Problem.SeverityLevel.warning:
							fromIcon = SystemIcons.Warning;
							break;
						case Problem.SeverityLevel.error:
							fromIcon = SystemIcons.Error;
							break;
						default:
							throw new InvalidOperationException("Unsupported value: " + _problem.Severity.ToString());
					}

					int destWidth = iconSize;
					int destHeight = iconSize;
					using (Bitmap bitmap = new Bitmap(destWidth, destHeight))
					{
						using (Graphics graphicsSurface = Graphics.FromImage(bitmap))
						{
							graphicsSurface.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
							using (var iconBitmap = fromIcon.ToBitmap())
							{
								graphicsSurface.DrawImage(iconBitmap, 0, 0, destWidth, destHeight);
							}
						}
						// This obtains an unmanaged resource that must be released explicitly
						IntPtr hBitmap = bitmap.GetHbitmap();
						try
						{
							var sizeOptions = BitmapSizeOptions.FromWidthAndHeight(bitmap.Width, bitmap.Height);
							ImageSource imgSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, sizeOptions);
							return imgSource;
						}
						finally
						{
							DeleteObjectInvoker.DeleteObject(hBitmap);
						}
					}
				}
			}

			public Problem Problem
			{
				get { return _problem; }
			}

			Problem _problem;
		}
	}

	public class DeleteObjectInvoker
	{
		[System.Runtime.InteropServices.DllImport("gdi32.dll")]
		public static extern bool DeleteObject(IntPtr hObject);
	}
}
