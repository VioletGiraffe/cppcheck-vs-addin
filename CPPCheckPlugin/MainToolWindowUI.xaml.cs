using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
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

        private GridViewColumnHeader listViewSortCol = null;
        private SortAdorner listViewSortAdorner = null;

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

        private void problemColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (sender as GridViewColumnHeader);
            string sortBy = column.Tag.ToString();

            ClearSorting();

            ListSortDirection newDir = ListSortDirection.Ascending;
            if (listViewSortCol == column && listViewSortAdorner.Direction == newDir)
            {
                newDir = ListSortDirection.Descending;
            }

            listViewSortCol = column;
            listViewSortAdorner = new SortAdorner(listViewSortCol, newDir);
            AdornerLayer.GetAdornerLayer(listViewSortCol).Add(listViewSortAdorner);

            if (sortBy == "Severity")
            {
                listView.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
                listView.Items.SortDescriptions.Add(new SortDescription("FileName", ListSortDirection.Ascending));
                listView.Items.SortDescriptions.Add(new SortDescription("Line", ListSortDirection.Ascending));
            }
            else if (sortBy == "FileName")
            {
                listView.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
                listView.Items.SortDescriptions.Add(new SortDescription("Line", ListSortDirection.Ascending));
            }
            else if (sortBy == "Message")
            {
                listView.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
                listView.Items.SortDescriptions.Add(new SortDescription("FileName", ListSortDirection.Ascending));
                listView.Items.SortDescriptions.Add(new SortDescription("Line", ListSortDirection.Ascending));
            }
            else
            {
                listView.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
            }
        }

        public void ClearSorting()
        {
            if(listViewSortCol != null)
            {
                AdornerLayer.GetAdornerLayer(listViewSortCol).Remove(listViewSortAdorner);
                listView.Items.SortDescriptions.Clear();
            }
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

            public Problem.SeverityLevel Severity
            {
                get { return _problem.Severity; }
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
        private void ListView_SelectionChanged()
        {
        }
        private void ListView_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
        }
        private void ListView_SelectionChanged_2(object sender, SelectionChangedEventArgs e)
        {
        }
	}

	public class DeleteObjectInvoker
	{
		[System.Runtime.InteropServices.DllImport("gdi32.dll")]
		public static extern bool DeleteObject(IntPtr hObject);
	}

    public class SortAdorner : Adorner
    {
        private static Geometry ascGeometry = Geometry.Parse("M 0 4 L 3.5 0 L 7 4 Z");
        private static Geometry descGeometry = Geometry.Parse("M 0 0 L 3.5 4 L 7 0 Z");

        public ListSortDirection Direction { get; private set; }

        public SortAdorner(UIElement element, ListSortDirection dir)
            : base(element)
        {
            this.Direction = dir;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (AdornedElement.RenderSize.Width < 20)
            {
                return;
            }

            TranslateTransform transform = new TranslateTransform(AdornedElement.RenderSize.Width - 15, (AdornedElement.RenderSize.Height - 5) / 2);
            drawingContext.PushTransform(transform);

            Geometry geometry = ascGeometry;
            if (this.Direction == ListSortDirection.Descending)
            {
                geometry = descGeometry;
            }
            drawingContext.DrawGeometry(System.Windows.Media.Brushes.Black, null, geometry);

            drawingContext.Pop();
        }
    }
}
