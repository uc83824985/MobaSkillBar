using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MobaSkillBar;

internal sealed class RegionPicker : Window
{
    private readonly DisplayInfo display;
    private readonly Canvas canvas = new()
    {
        Background = new SolidColorBrush(Color.FromArgb(75, 0, 0, 0)),
        Focusable = true,
        ClipToBounds = true
    };
    private readonly Rectangle outline = new()
    {
        Visibility = Visibility.Hidden,
        Stroke = Brushes.Cyan,
        StrokeThickness = 2,
        Fill = new SolidColorBrush(Color.FromArgb(35, 0, 255, 255)),
        IsHitTestVisible = false
    };
    private readonly RegionMagnifier? magnifier;
    private readonly Border preview;
    private readonly TextBlock readout = new()
    {
        Foreground = Brushes.White,
        FontSize = 13,
        Padding = new Thickness(5, 3, 5, 3)
    };
    private PixelPoint? start;

    private RegionPicker(DisplayInfo display, BitmapSource? snapshot, Action<ScreenRegion?> finish)
    {
        this.display = display;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = snapshot is null;
        Background = snapshot is null ? Brushes.Transparent : Brushes.Black;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Cursor = Cursors.Cross;
        UseLayoutRounding = true;

        var surface = new Grid();
        if (snapshot is not null)
        {
            var image = new Image { Source = snapshot, Stretch = Stretch.Fill, IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            surface.Children.Add(image);
        }
        surface.Children.Add(canvas);
        canvas.Children.Add(outline);
        var panelBrush = new SolidColorBrush(Color.FromRgb(18, 30, 40));
        var help = new Border
        {
            Padding = new Thickness(14, 8, 14, 8),
            Background = panelBrush,
            Child = new TextBlock
            {
                Text = "左键拖动框选 · WASD 或方向键移动 1 像素 · 右键或 Esc 取消" + (snapshot is null ? " · 放大不可用" : ""),
                Foreground = Brushes.White,
                FontSize = 16
            },
            IsHitTestVisible = false
        };
        Canvas.SetTop(help, 20);
        Canvas.SetLeft(help, 20);
        canvas.Children.Add(help);

        var details = new StackPanel();
        if (snapshot is not null)
        {
            magnifier = new RegionMagnifier(snapshot);
            details.Children.Add(magnifier);
        }
        details.Children.Add(readout);
        preview = new Border
        {
            Background = panelBrush,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Child = details,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        canvas.Children.Add(preview);
        Content = surface;

        SourceInitialized += (_, _) => Desktop.Position(this, display.Bounds, resize: true);
        Loaded += (_, _) =>
        {
            Desktop.Position(this, display.Bounds, resize: true);
            UpdatePreview();
        };
        Activated += (_, _) => canvas.Focus();
        PreviewMouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            finish(null);
        };
        canvas.MouseLeftButtonDown += (_, _) =>
        {
            if (!canvas.CaptureMouse()) return;
            start = LocalCursor(Desktop.Cursor);
            UpdatePreview();
        };
        canvas.SizeChanged += (_, _) => UpdatePreview();
        canvas.MouseEnter += (_, _) => UpdatePreview();
        canvas.MouseMove += (_, _) => UpdatePreview();
        canvas.MouseLeave += (_, _) => UpdatePreview();
        canvas.LostMouseCapture += (_, _) =>
        {
            start = null;
            UpdatePreview();
        };
        canvas.MouseLeftButtonUp += (_, _) =>
        {
            if (start is not { } origin) return;
            var bounds = Selection(origin, LocalCursor(Desktop.Cursor));
            canvas.ReleaseMouseCapture();
            if (bounds.Width >= 2 && bounds.Height >= 2)
            {
                finish(new ScreenRegion(display.Id, bounds.Left, bounds.Top, bounds.Width, bounds.Height));
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

        void HandleKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Finish(null);
                return;
            }

            var (dx, dy) = Desktop.KeyStep(e.Key);
            if (dx == 0 && dy == 0) return;

            e.Handled = true;
            var cursor = Desktop.Cursor;
            var target = windows.FirstOrDefault(window => window.start is not null)
                ?? windows.FirstOrDefault(window => window.Contains(cursor)) ?? (RegionPicker)sender;
            var point = target.LocalCursor(cursor);
            var bounds = target.display.Bounds;
            Desktop.MoveCursor(new PixelPoint(bounds.Left + Math.Clamp(point.X + dx, 0, bounds.Width - 1),
                bounds.Top + Math.Clamp(point.Y + dy, 0, bounds.Height - 1)));
            target.UpdatePreview();
        }

        foreach (var display in displays.OrderByDescending(display => display.Primary))
        {
            var window = new RegionPicker(display, Desktop.Snapshot(display.Bounds), Finish);
            window.PreviewKeyDown += HandleKey;
            window.Closing += (_, e) =>
            {
                if (finished) return;
                e.Cancel = true;
                window.Dispatcher.BeginInvoke(() => Finish(null));
            };
            windows.Add(window);
        }
        foreach (var window in windows) window.Show();
        var current = Desktop.Cursor;
        (windows.FirstOrDefault(window => window.Contains(current)) ?? windows[0]).Activate();
    }

    private bool Contains(PixelPoint point) => point.X >= display.Bounds.Left && point.X < display.Bounds.Right &&
        point.Y >= display.Bounds.Top && point.Y < display.Bounds.Bottom;

    private PixelPoint LocalCursor(PixelPoint point) => new(Math.Clamp(point.X - display.Bounds.Left, 0, display.Bounds.Width - 1),
        Math.Clamp(point.Y - display.Bounds.Top, 0, display.Bounds.Height - 1));

    // Include both cursor pixels while keeping the stored right and bottom edges exclusive.
    private static PixelRect Selection(PixelPoint origin, PixelPoint current) =>
        new(Math.Min(origin.X, current.X), Math.Min(origin.Y, current.Y),
            Math.Max(origin.X, current.X) + 1, Math.Max(origin.Y, current.Y) + 1);

    private void UpdatePreview()
    {
        if (!IsVisible || PresentationSource.FromVisual(canvas) is null) return;
        var cursor = Desktop.Cursor;
        var point = LocalCursor(cursor);
        PixelRect? selection = start is { } origin ? Selection(origin, point) : null;
        if (selection is { } bounds) UpdateOutline(bounds);
        else outline.Visibility = Visibility.Hidden;

        if (selection is null && !Contains(cursor))
        {
            preview.Visibility = Visibility.Collapsed;
            return;
        }

        magnifier?.Update(point, selection);
        readout.Text = selection is { } region
            ? $"X {point.X}  Y {point.Y}\n{region.Width} × {region.Height} px"
            : $"X {point.X}  Y {point.Y}";
        preview.Visibility = Visibility.Visible;
        preview.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var anchor = ToCanvas(point.X, point.Y);
        Canvas.SetLeft(preview, PreviewOffset(anchor.X, preview.DesiredSize.Width, canvas.ActualWidth));
        Canvas.SetTop(preview, PreviewOffset(anchor.Y, preview.DesiredSize.Height, canvas.ActualHeight));
    }

    private void UpdateOutline(PixelRect bounds)
    {
        var topLeft = ToCanvas(bounds.Left, bounds.Top);
        var bottomRight = ToCanvas(bounds.Right, bounds.Bottom);
        Canvas.SetLeft(outline, topLeft.X);
        Canvas.SetTop(outline, topLeft.Y);
        outline.Width = Math.Max(0, bottomRight.X - topLeft.X);
        outline.Height = Math.Max(0, bottomRight.Y - topLeft.Y);
        outline.Visibility = Visibility.Visible;
    }

    private Point ToCanvas(int x, int y) =>
        canvas.PointFromScreen(new Point(display.Bounds.Left + x, display.Bounds.Top + y));

    private static double PreviewOffset(double cursor, double size, double available)
    {
        const double gap = 20;
        double offset = cursor + gap + size <= available ? cursor + gap : cursor - gap - size;
        return Math.Clamp(offset, 0, Math.Max(0, available - size));
    }
}
