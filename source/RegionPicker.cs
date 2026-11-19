using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MobaSkillBar;

internal sealed class RegionPicker : Window
{
    private readonly DisplayInfo display;
    private readonly Canvas canvas = new() { Background = new SolidColorBrush(Color.FromArgb(75, 0, 0, 0)) };
    private readonly Rectangle outline = new()
    {
        Visibility = Visibility.Hidden,
        Stroke = Brushes.Cyan,
        StrokeThickness = 2,
        Fill = new SolidColorBrush(Color.FromArgb(35, 0, 255, 255)),
        IsHitTestVisible = false
    };
    private PixelPoint? start;

    private RegionPicker(DisplayInfo display, Action<ScreenRegion?> finish)
    {
        this.display = display;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Cursor = Cursors.Cross;
        canvas.Children.Add(outline);
        var help = new Border
        {
            Padding = new Thickness(14, 8, 14, 8),
            Background = new SolidColorBrush(Color.FromRgb(18, 30, 40)),
            Child = new TextBlock { Text = "拖动框选一个技能格，Esc 取消", Foreground = Brushes.White, FontSize = 16 },
            IsHitTestVisible = false
        };
        Canvas.SetTop(help, 20);
        Canvas.SetLeft(help, 20);
        canvas.Children.Add(help);
        Content = canvas;
        SourceInitialized += (_, _) => Desktop.Position(this, display.Bounds, resize: true);
        Loaded += (_, _) => Desktop.Position(this, display.Bounds, resize: true);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                finish(null);
            }
        };
        canvas.MouseLeftButtonDown += (_, _) =>
        {
            start = Clamp(Desktop.Cursor);
            canvas.CaptureMouse();
            UpdateOutline();
        };
        canvas.MouseMove += (_, _) => UpdateOutline();
        canvas.LostMouseCapture += (_, _) => ClearSelection();
        canvas.MouseLeftButtonUp += (_, _) =>
        {
            if (start is not { } origin) return;
            var bounds = Selection(origin);
            canvas.ReleaseMouseCapture();
            if (bounds.Width >= 2 && bounds.Height >= 2)
            {
                finish(new ScreenRegion(display.Id, bounds.Left - display.Bounds.Left,
                    bounds.Top - display.Bounds.Top, bounds.Width, bounds.Height));
            }
        };
    }

    public static void ShowAll(DisplayInfo[] displays, Action<ScreenRegion?> completed)
    {
        if (displays.Length == 0)
        {
            completed(null);
            return;
        }
        bool finished = false;
        var windows = new List<RegionPicker>();
        void Finish(ScreenRegion? region)
        {
            if (finished) return;
            finished = true;
            foreach (var window in windows) window.Close();
            completed(region);
        }

        foreach (var display in displays.OrderBy(display => display.Primary))
        {
            var window = new RegionPicker(display, Finish);
            window.Closing += (_, e) =>
            {
                if (finished) return;
                e.Cancel = true;
                window.Dispatcher.BeginInvoke(() => Finish(null));
            };
            windows.Add(window);
            window.Show();
        }
    }

    private PixelPoint Clamp(PixelPoint point) => new(Math.Clamp(point.X, display.Bounds.Left, display.Bounds.Right),
        Math.Clamp(point.Y, display.Bounds.Top, display.Bounds.Bottom));

    private PixelRect Selection(PixelPoint origin)
    {
        var current = Clamp(Desktop.Cursor);
        return new PixelRect(Math.Min(origin.X, current.X), Math.Min(origin.Y, current.Y),
            Math.Max(origin.X, current.X), Math.Max(origin.Y, current.Y));
    }

    private void UpdateOutline()
    {
        if (start is not { } origin) return;
        var bounds = Selection(origin);
        var topLeft = canvas.PointFromScreen(new Point(bounds.Left, bounds.Top));
        var bottomRight = canvas.PointFromScreen(new Point(bounds.Right, bounds.Bottom));
        Canvas.SetLeft(outline, topLeft.X);
        Canvas.SetTop(outline, topLeft.Y);
        outline.Width = Math.Max(0, bottomRight.X - topLeft.X);
        outline.Height = Math.Max(0, bottomRight.Y - topLeft.Y);
        outline.Visibility = Visibility.Visible;
    }

    private void ClearSelection()
    {
        start = null;
        outline.Visibility = Visibility.Hidden;
    }
}
