using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Claudestrap.Enums;
using Claudestrap.UI.Elements.Base;

namespace Claudestrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Dialog for previewing cursor types before applying them
    /// </summary>
    public partial class CursorPreviewDialog : WpfUiWindow
    {
        public Claudestrap.Enums.CursorType? SelectedCursor { get; private set; }

        public CursorPreviewDialog()
        {
            InitializeComponent();
            LoadCursorPreviews();
        }

        private void LoadCursorPreviews()
        {
            var cursors = new[]
            {
                Claudestrap.Enums.CursorType.Default,
                Claudestrap.Enums.CursorType.FPSCursor,
                Claudestrap.Enums.CursorType.CleanCursor,
                Claudestrap.Enums.CursorType.DotCursor,
                Claudestrap.Enums.CursorType.StoofsCursor,
                Claudestrap.Enums.CursorType.From2006,
                Claudestrap.Enums.CursorType.From2013,
                Claudestrap.Enums.CursorType.WhiteDotCursor,
                Claudestrap.Enums.CursorType.VerySmallWhiteDot
            };

            foreach (var cursor in cursors)
            {
                var previewItem = CreateCursorPreviewItem(cursor);
                CursorStackPanel.Children.Add(previewItem);
            }
        }

        private FrameworkElement CreateCursorPreviewItem(Claudestrap.Enums.CursorType cursor)
        {
            var border = new System.Windows.Controls.Border
            {
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Colors.Transparent),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };

            // Load cursor image for preview
            var image = new System.Windows.Controls.Image
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0)
            };

            try
            {
                var imagePath = GetCursorImagePath(cursor);
                if (!string.IsNullOrEmpty(imagePath))
                {
                    var uri = new Uri($"pack://application:,,,/Resources/Mods/{imagePath}");
                    image.Source = new BitmapImage(uri);
                }
            }
            catch
            {
                // Use default image if cursor image can't be loaded
                image.Source = null;
            }

            var nameLabel = new System.Windows.Controls.TextBlock
            {
                Text = GetCursorDisplayName(cursor),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };

            stackPanel.Children.Add(image);
            stackPanel.Children.Add(nameLabel);
            border.Child = stackPanel;

            border.MouseLeftButtonUp += (s, e) =>
            {
                SelectedCursor = cursor;
                DialogResult = true;
                Close();
            };

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(50, 100, 149, 237));
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Colors.Transparent);
            };

            return border;
        }

        private string GetCursorImagePath(Claudestrap.Enums.CursorType cursor)
        {
            return cursor switch
            {
                Claudestrap.Enums.CursorType.FPSCursor => "Cursor/FPSCursor/ArrowCursor.png",
                Claudestrap.Enums.CursorType.CleanCursor => "Cursor/CleanCursor/ArrowCursor.png",
                Claudestrap.Enums.CursorType.DotCursor => "Cursor/DotCursor/ArrowCursor.png",
                Claudestrap.Enums.CursorType.StoofsCursor => "Cursor/StoofsCursor/ArrowCursor.png",
                Claudestrap.Enums.CursorType.From2006 => "Cursor/From2006/ArrowCursor.png",
                Claudestrap.Enums.CursorType.From2013 => "Cursor/From2013/ArrowCursor.png",
                Claudestrap.Enums.CursorType.WhiteDotCursor => "Cursor/WhiteDotCursor/ArrowCursor.png",
                Claudestrap.Enums.CursorType.VerySmallWhiteDot => "Cursor/VerySmallWhiteDot/ArrowCursor.png",
                _ => string.Empty
            };
        }

        private string GetCursorDisplayName(Claudestrap.Enums.CursorType cursor)
        {
            return cursor switch
            {
                Claudestrap.Enums.CursorType.Default => "Default",
                Claudestrap.Enums.CursorType.FPSCursor => "FPS Cursor (V1)",
                Claudestrap.Enums.CursorType.CleanCursor => "Clean Cursor",
                Claudestrap.Enums.CursorType.DotCursor => "Dot Cursor",
                Claudestrap.Enums.CursorType.StoofsCursor => "Stoofs Cursor",
                Claudestrap.Enums.CursorType.From2006 => "2006 Legacy Cursor",
                Claudestrap.Enums.CursorType.From2013 => "2013 Legacy Cursor",
                Claudestrap.Enums.CursorType.WhiteDotCursor => "White Dot Cursor",
                Claudestrap.Enums.CursorType.VerySmallWhiteDot => "Very Small White Dot",
                _ => cursor.ToString()
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}