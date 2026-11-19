using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MobaSkillBar;

internal sealed class TileView : Border
{
    private readonly Image picture = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly TextBlock status = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Foreground = Brushes.White,
        FontSize = 13,
        IsHitTestVisible = false
    };
    private readonly Rectangle groupOutline = new()
    {
        StrokeThickness = 2, Visibility = Visibility.Hidden, IsHitTestVisible = false
    };
    private WriteableBitmap? bitmap;
    private byte[] pixels = [];
    private byte[] adjusted = [];
    private readonly byte[] contrastMap = new byte[256];
    private double appliedContrast = double.NaN;
    private double appliedSaturation = double.NaN;
    public Tile Tile { get; }
    public string? Status { get; private set; }

    public TileView(Tile tile)
    {
        Tile = tile;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.NearestNeighbor);
        var grid = new Grid();
        grid.Children.Add(picture);
        grid.Children.Add(status);
        grid.Children.Add(groupOutline);
        Child = grid;
        Background = Brushes.Transparent;
        SetStatus("正在捕获");
    }

    public void ShowGroupOutline(bool visible, Brush brush)
    {
        groupOutline.Stroke = brush;
        groupOutline.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
    }

    public void SetStatus(string message)
    {
        if (Status == message) return;
        Status = message;
        status.Text = message;
        status.Visibility = Visibility.Visible;
        picture.Source = null;
        Background = new SolidColorBrush(Color.FromArgb(205, 18, 28, 37));
    }

    public void UpdateAppearance(DpiScale dpi)
    {
        var size = Tile.DisplaySize(dpi);
        Width = size.Width;
        Height = size.Height;
        picture.Opacity = Tile.Opacity;
        var position = Tile.UpdatePixelPosition(dpi);
        Canvas.SetLeft(this, position.X / dpi.DpiScaleX);
        Canvas.SetTop(this, position.Y / dpi.DpiScaleY);
        if (Status is null && (appliedContrast != Tile.Contrast || appliedSaturation != Tile.Saturation)) RenderPixels();
    }

    public void UpdatePixels(IntPtr data, int rowPitch)
    {
        int width = Tile.Source.Width;
        int height = Tile.Source.Height;
        if (bitmap is null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
        {
            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            pixels = new byte[checked(width * height * 4)];
            adjusted = [];
        }

        int stride = width * 4;
        if (stride == rowPitch)
        {
            Marshal.Copy(data, pixels, 0, pixels.Length);
        }
        else
        {
            for (int row = 0; row < height; row++)
                Marshal.Copy(data + row * rowPitch, pixels, row * stride, stride);
        }
        RenderPixels();
        if (picture.Source != bitmap) picture.Source = bitmap;
        if (Status is not null)
        {
            Status = null;
            status.Visibility = Visibility.Collapsed;
            Background = Brushes.Transparent;
        }
    }

    private void RenderPixels()
    {
        if (bitmap is null) return;
        double contrast = Tile.Contrast;
        byte[] output = pixels;
        double saturation = Tile.Saturation;
        if (appliedContrast != contrast)
        {
            for (int value = 0; value < contrastMap.Length; value++)
                contrastMap[value] = (byte)Math.Clamp(Math.Round(128 + (value - 128) * contrast), 0, 255);
        }
        if (contrast != 1 || saturation != 1)
        {
            if (adjusted.Length != pixels.Length) adjusted = new byte[pixels.Length];
            if (saturation == 1)
            {
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    adjusted[i] = contrastMap[pixels[i]];
                    adjusted[i + 1] = contrastMap[pixels[i + 1]];
                    adjusted[i + 2] = contrastMap[pixels[i + 2]];
                }
            }
            else
            {
                int amount = (int)Math.Round(saturation * 65536);
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                    // Rec. 709 luma, using fixed-point weights that sum to one.
                    int gray = (4732 * b + 46871 * g + 13933 * r + 32768) >> 16;
                    adjusted[i] = AdjustChannel(b, gray, amount);
                    adjusted[i + 1] = AdjustChannel(g, gray, amount);
                    adjusted[i + 2] = AdjustChannel(r, gray, amount);
                }
            }
            output = adjusted;
        }
        appliedContrast = contrast;
        appliedSaturation = saturation;
        bitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), output, bitmap.PixelWidth * 4, 0);
    }

    private byte AdjustChannel(int channel, int gray, int amount) =>
        contrastMap[Math.Clamp(gray + (((channel - gray) * amount + 32768) >> 16), 0, 255)];
}

internal sealed class FloatingBar : Window
{
    private const double GripHeight = 26;
    private const double HandleSize = 9;
    private static readonly Brush EditorBackground = new SolidColorBrush(Color.FromArgb(142, 14, 23, 31));
    private static readonly Brush SelectionBrush = new SolidColorBrush(Color.FromRgb(90, 221, 251));
    private readonly Canvas canvas = new() { Focusable = true, FocusVisualStyle = null };
    private readonly Canvas chrome = new() { Visibility = Visibility.Hidden };
    private readonly Rectangle outline = new() { Stroke = SelectionBrush, StrokeThickness = 1.5, IsHitTestVisible = false };
    private readonly List<(Thumb Thumb, Vector Direction)> handles = [];
    private readonly TextBlock gripText = new()
    {
        Foreground = Brushes.White, Margin = new Thickness(9, 0, 9, 0),
        VerticalAlignment = VerticalAlignment.Center, FontSize = 13
    };
    private readonly Border grip;
    private readonly Dictionary<Guid, TileView> views = [];
    private readonly Rectangle marquee = new()
    {
        Stroke = SelectionBrush, StrokeThickness = 1, Fill = new SolidColorBrush(Color.FromArgb(48, 90, 221, 251)),
        Visibility = Visibility.Hidden, IsHitTestVisible = false
    };
    private readonly DispatcherTimer hold = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly HashSet<Tile> selected = [];
    private readonly Action<IReadOnlyCollection<Tile>> select;
    private readonly Action save;
    private LayoutPreset? layout;
    private readonly List<(TileView View, Point Origin)> dragMembers = [];
    private Tile? pressedTile;
    private Point pointerStart;
    private bool moving;
    private ResizeGesture? resizing;
    private bool editing;
    private bool excluded;
    private bool positionPending;

    public FloatingBar(Action<IReadOnlyCollection<Tile>> select, Action save)
    {
        this.select = select;
        this.save = save;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.Manual;

        grip = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(28, 48, 61)),
            Child = gripText,
            Cursor = Cursors.SizeAll
        };
        grip.MouseLeftButtonDown += (_, e) =>
        {
            if (!editing) return;
            e.Handled = true;
            DragMove();
            SavePosition();
        };
        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(GripHeight) });
        panel.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(canvas, 1);
        Grid.SetRow(chrome, 1);
        panel.Children.Add(grip);
        panel.Children.Add(canvas);
        panel.Children.Add(chrome);
        Content = panel;
        chrome.Children.Add(outline);
        CreateResizeHandles();
        UpdateSize();
        Loaded += (_, _) => Synchronize();
        DpiChanged += (_, _) => Dispatcher.InvokeAsync(Synchronize);
        SourceInitialized += (_, _) => ApplyMode();
        Panel.SetZIndex(marquee, int.MaxValue);
        hold.Tick += (_, _) =>
        {
            hold.Stop();
            if (canvas.IsMouseCaptured) BeginMarquee();
        };
        canvas.MouseLeftButtonDown += (_, e) =>
        {
            if (!editing || e.Source != canvas || !canvas.CaptureMouse()) return;
            canvas.Focus();
            pointerStart = e.GetPosition(canvas);
            BeginMarquee();
            e.Handled = true;
        };
        canvas.MouseMove += (_, e) =>
        {
            if (canvas.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) UpdateGesture(e.GetPosition(canvas));
        };
        canvas.MouseLeftButtonUp += (_, e) =>
        {
            if (!canvas.IsMouseCaptured) return;
            UpdateGesture(e.GetPosition(canvas));
            if (pressedTile is { } tile && !moving && selected.Count > 1) select([tile]);
            canvas.ReleaseMouseCapture();
            e.Handled = true;
        };
        canvas.LostMouseCapture += (_, _) => EndGesture();
    }

    public IReadOnlyCollection<TileView> Views => views.Values;

    public void Load(LayoutPreset preset)
    {
        if (canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
        layout = preset;
        selected.Clear();
        positionPending = true;
        canvas.Children.Clear();
        canvas.Children.Add(marquee);
        views.Clear();
        Synchronize();
    }

    public void Synchronize()
    {
        if (layout is null) return;
        var active = layout.Tiles.Select(tile => tile.Id).ToHashSet();
        foreach (var (id, view) in views.ToArray())
        {
            if (active.Contains(id)) continue;
            canvas.Children.Remove(view);
            views.Remove(id);
        }

        for (int index = 0; index < layout.Tiles.Count; index++)
        {
            var tile = layout.Tiles[index];
            if (!views.TryGetValue(tile.Id, out var view))
            {
                view = new TileView(tile);
                views.Add(tile.Id, view);
                canvas.Children.Add(view);
                view.MouseLeftButtonDown += (_, e) => BeginTileDrag(view, e);
            }
            Panel.SetZIndex(view, index);
            view.UpdateAppearance(VisualTreeHelper.GetDpi(this));
        }
        UpdateSize();
        UpdateSelection();
    }

    public void SelectTiles(IEnumerable<Tile> tiles)
    {
        selected.Clear();
        selected.UnionWith(tiles);
        UpdateSelection();
    }

    public void SetMode(bool edit, bool suspended, DisplayInfo[] displays)
    {
        if ((!edit || suspended) && canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
        editing = edit;
        if (suspended || (!editing && views.Count == 0))
        {
            Hide();
            return;
        }

        ApplyMode();
        if (!IsVisible)
        {
            Show();
            excluded = Desktop.ExcludeFromCapture(this);
        }
        if (positionPending) RestorePosition(displays);
    }

    public PixelRect[] CaptureObstructions()
    {
        if (!IsVisible || excluded) return [];
        if (editing) return [Desktop.WindowBounds(this)];
        return views.Values.Where(view => view.IsLoaded && (view.Status is not null || view.Tile.Opacity > 0))
            .Select(view =>
            {
                var start = view.PointToScreen(new Point(0, 0));
                var end = view.PointToScreen(new Point(view.ActualWidth, view.ActualHeight));
                return new PixelRect((int)Math.Floor(start.X), (int)Math.Floor(start.Y),
                    (int)Math.Ceiling(end.X), (int)Math.Ceiling(end.Y));
            }).ToArray();
    }

    private void ResetPosition(DisplayInfo[] displays)
    {
        if (layout is null) return;
        layout.Left = null;
        layout.Top = null;
        positionPending = true;
        if (IsVisible) RestorePosition(displays);
        save();
    }

    public void KeepVisible(DisplayInfo[] displays)
    {
        if (!IsVisible) positionPending = true;
        else if (!HasVisibleGrip(displays)) ResetPosition(displays);
    }

    private void ApplyMode()
    {
        grip.Visibility = editing ? Visibility.Visible : Visibility.Hidden;
        canvas.Background = editing ? EditorBackground : Brushes.Transparent;
        UpdateSelection();
        Desktop.SetClickThrough(this, !editing);
    }

    private void UpdateSize()
    {
        double width = views.Values.Select(view => view.Tile.X + view.Width).DefaultIfEmpty().Max();
        double height = views.Values.Select(view => view.Tile.Y + view.Height).DefaultIfEmpty().Max();
        Width = Math.Max(300, width + HandleSize);
        Height = GripHeight + Math.Max(130, height + HandleSize);
    }

    private void BeginTileDrag(TileView view, MouseButtonEventArgs e)
    {
        if (!editing) return;
        bool wasSelected = selected.Contains(view.Tile);
        if (!wasSelected) select([view.Tile]);
        canvas.Focus();
        if (!canvas.CaptureMouse()) return;
        pointerStart = e.GetPosition(canvas);
        pressedTile = view.Tile;
        dragMembers.Clear();
        foreach (var tile in selected)
            dragMembers.Add((views[tile.Id], new Point(tile.X, tile.Y)));
        if (!wasSelected) hold.Start();
        e.Handled = true;
    }

    private void BeginMarquee()
    {
        pressedTile = null;
        dragMembers.Clear();
        marquee.Visibility = Visibility.Visible;
        UpdateMarquee(pointerStart);
    }

    private void UpdateMarquee(Point point)
    {
        var area = new Rect(pointerStart, point);
        Canvas.SetLeft(marquee, area.Left);
        Canvas.SetTop(marquee, area.Top);
        marquee.Width = area.Width;
        marquee.Height = area.Height;

        var hits = views.Values.Where(view => area.Width > 0 && area.Height > 0 &&
            area.IntersectsWith(new Rect(view.Tile.X, view.Tile.Y, view.Width, view.Height)))
            .Select(view => view.Tile).ToArray();
        if (!selected.SetEquals(hits)) select(hits);
    }

    private void UpdateGesture(Point point)
    {
        if (marquee.Visibility == Visibility.Visible)
        {
            UpdateMarquee(point);
            return;
        }
        if (dragMembers.Count == 0) return;
        var delta = point - pointerStart;
        if (!moving && Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        hold.Stop();
        moving = true;
        MoveTiles(dragMembers, delta, VisualTreeHelper.GetDpi(this));
    }

    public bool NudgeSelection(PixelPoint step)
    {
        if (step == default || !editing || !IsVisible || selected.Count == 0 ||
            canvas.IsMouseCaptured || resizing is not null) return false;

        var dpi = VisualTreeHelper.GetDpi(this);
        var delta = new Vector(step.X / dpi.DpiScaleX, step.Y / dpi.DpiScaleY);
        // Start each step at the displayed pixel so fractional drag positions cannot skip a pixel.
        var members = selected.Select(tile =>
        {
            var view = views[tile.Id];
            var origin = new Point(step.X == 0 ? tile.X : Canvas.GetLeft(view),
                step.Y == 0 ? tile.Y : Canvas.GetTop(view));
            return (view, origin);
        }).ToArray();
        MoveTiles(members, delta, dpi);
        return true;
    }

    private void MoveTiles(IReadOnlyList<(TileView View, Point Origin)> members, Vector delta, DpiScale dpi)
    {
        // Clamp the translation once so members retain their spacing at the canvas edges.
        delta.X = Math.Max(delta.X, -members.Min(member => member.Origin.X));
        delta.Y = Math.Max(delta.Y, -members.Min(member => member.Origin.Y));
        foreach (var (member, origin) in members)
        {
            member.Tile.X = origin.X + delta.X;
            member.Tile.Y = origin.Y + delta.Y;
            member.UpdateAppearance(dpi);
        }
        UpdateSize();
        UpdateSelection();
    }

    private void EndGesture()
    {
        hold.Stop();
        marquee.Visibility = Visibility.Hidden;
        pressedTile = null;
        dragMembers.Clear();
        if (moving) save();
        moving = false;
    }

    private void CreateResizeHandles()
    {
        var frame = new FrameworkElementFactory(typeof(Border));
        frame.SetValue(Border.BackgroundProperty, Brushes.White);
        frame.SetValue(Border.BorderBrushProperty, SelectionBrush);
        frame.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var template = new ControlTemplate(typeof(Thumb)) { VisualTree = frame };

        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            if (x == 0 && y == 0) continue;
            var direction = new Vector(x, y);
            var thumb = new Thumb
            {
                Width = HandleSize, Height = HandleSize, Template = template, Focusable = false,
                Cursor = x == 0 ? Cursors.SizeNS : y == 0 ? Cursors.SizeWE : x == y ? Cursors.SizeNWSE : Cursors.SizeNESW
            };
            thumb.DragStarted += (_, _) => BeginResize(direction);
            thumb.DragDelta += (_, _) => ResizeSelected();
            thumb.DragCompleted += (_, _) => EndResize();
            chrome.Children.Add(thumb);
            handles.Add((thumb, direction));
        }
    }

    private void UpdateSelection()
    {
        foreach (var member in views.Values)
            member.ShowGroupOutline(editing && selected.Count > 1 && selected.Contains(member.Tile), SelectionBrush);

        var tile = selected.Count == 1 ? selected.First() : null;
        gripText.Text = tile is null ? "拖动此处移动技能栏" : $"拖动此处移动技能栏    {tile.PositionText}";
        if (!editing || tile is null || !views.TryGetValue(tile.Id, out var view))
        {
            chrome.Visibility = Visibility.Hidden;
            return;
        }

        chrome.Visibility = Visibility.Visible;
        // Leave enough room to distinguish the handles even for tiny captures.
        double width = Math.Max(view.Width, 2 * HandleSize);
        double height = Math.Max(view.Height, 2 * HandleSize);
        outline.Width = width;
        outline.Height = height;
        double left = Canvas.GetLeft(view);
        double top = Canvas.GetTop(view);
        Canvas.SetLeft(outline, left);
        Canvas.SetTop(outline, top);
        foreach (var (thumb, direction) in handles)
        {
            Canvas.SetLeft(thumb, Math.Max(0, left + width * (direction.X + 1) / 2 - HandleSize / 2));
            Canvas.SetTop(thumb, Math.Max(0, top + height * (direction.Y + 1) / 2 - HandleSize / 2));
        }
    }

    private void BeginResize(Vector direction)
    {
        if (editing && selected.Count == 1 && views.TryGetValue(selected.First().Id, out var view))
        {
            resizing = new ResizeGesture(view, direction, Mouse.GetPosition(canvas),
                new Rect(view.Tile.X, view.Tile.Y, view.Width, view.Height));
        }
    }

    private void ResizeSelected()
    {
        if (resizing is not { } gesture) return;
        var view = gesture.View;
        var tile = view.Tile;
        var (position, scale) = TileGeometry.Resize(tile.Source, gesture.InitialBounds, VisualTreeHelper.GetDpi(this), gesture.Direction,
            Mouse.GetPosition(canvas) - gesture.Start);
        tile.X = position.X;
        tile.Y = position.Y;
        tile.Scale = scale;
        view.UpdateAppearance(VisualTreeHelper.GetDpi(this));
        UpdateSize();
        UpdateSelection();
    }

    private void EndResize()
    {
        if (resizing is not { } gesture) return;
        resizing = null;
        select([gesture.View.Tile]);
        save();
    }

    private sealed record ResizeGesture(TileView View, Vector Direction, Point Start, Rect InitialBounds);

    private void RestorePosition(DisplayInfo[] displays)
    {
        if (layout is null || displays.Length == 0) return;
        positionPending = false;
        if (layout.Left is int left && layout.Top is int top)
        {
            Desktop.Position(this, new PixelRect(left, top, left, top), resize: false);
            if (HasVisibleGrip(displays)) return;
        }

        var work = (displays.FirstOrDefault(display => display.Primary) ?? displays[0]).WorkArea;
        int x = work.Left + work.Width * 3 / 8;
        int y = work.Top + work.Height * 5 / 8;
        Desktop.Position(this, new PixelRect(x, y, x, y), resize: false);
        RecordPosition();
    }

    private bool HasVisibleGrip(DisplayInfo[] displays)
    {
        var bounds = Desktop.WindowBounds(this);
        var handle = new PixelRect(bounds.Left, bounds.Top, bounds.Left + 48, bounds.Top + 24);
        return displays.Any(display => handle.Left >= display.WorkArea.Left && handle.Top >= display.WorkArea.Top &&
            handle.Right <= display.WorkArea.Right && handle.Bottom <= display.WorkArea.Bottom);
    }

    private void SavePosition()
    {
        RecordPosition();
        save();
    }

    private void RecordPosition()
    {
        if (layout is null) return;
        var bounds = Desktop.WindowBounds(this);
        layout.Left = bounds.Left;
        layout.Top = bounds.Top;
    }
}
