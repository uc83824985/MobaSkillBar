using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
    private WriteableBitmap? bitmap;
    private byte[] pixels = [];
    private byte[] adjusted = [];
    private readonly byte[] contrastMap = new byte[256];
    private double appliedContrast = double.NaN;
    public Tile Tile { get; }
    public string? Status { get; private set; }

    public TileView(Tile tile)
    {
        Tile = tile;
        ClipToBounds = true;
        var grid = new Grid();
        grid.Children.Add(picture);
        grid.Children.Add(status);
        Child = grid;
        Background = Brushes.Transparent;
        SetStatus("正在捕获");
        UpdateAppearance();
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

    public void UpdateAppearance()
    {
        Width = Tile.Width;
        Height = Tile.Height;
        picture.Opacity = Tile.Opacity;
        Canvas.SetLeft(this, Tile.X);
        Canvas.SetTop(this, Tile.Y);
        if (Status is null && appliedContrast != Tile.Contrast) RenderPixels();
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
        if (Status is not null)
        {
            Status = null;
            picture.Source = bitmap;
            status.Visibility = Visibility.Collapsed;
            Background = Brushes.Transparent;
        }
    }

    private void RenderPixels()
    {
        if (bitmap is null) return;
        double contrast = Tile.Contrast;
        byte[] output = pixels;
        if (contrast != 1)
        {
            if (adjusted.Length != pixels.Length) adjusted = new byte[pixels.Length];
            if (appliedContrast != contrast)
            {
                for (int value = 0; value < contrastMap.Length; value++)
                    contrastMap[value] = (byte)Math.Clamp(Math.Round(128 + (value - 128) * contrast), 0, 255);
            }
            for (int i = 0; i < pixels.Length; i += 4)
            {
                adjusted[i] = contrastMap[pixels[i]];
                adjusted[i + 1] = contrastMap[pixels[i + 1]];
                adjusted[i + 2] = contrastMap[pixels[i + 2]];
            }
            output = adjusted;
        }
        appliedContrast = contrast;
        bitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), output, bitmap.PixelWidth * 4, 0);
    }
}

internal sealed class FloatingBar : Window
{
    private const double GripHeight = 26;
    private const double HandleSize = 9;
    private static readonly Brush EditorBackground = new SolidColorBrush(Color.FromArgb(142, 14, 23, 31));
    private static readonly Brush SelectionBrush = new SolidColorBrush(Color.FromRgb(90, 221, 251));
    private readonly Canvas canvas = new();
    private readonly Canvas chrome = new() { Visibility = Visibility.Hidden };
    private readonly Rectangle outline = new() { Stroke = SelectionBrush, StrokeThickness = 1.5, IsHitTestVisible = false };
    private readonly List<(Thumb Thumb, Vector Direction)> handles = [];
    private readonly Border grip;
    private readonly Dictionary<Guid, TileView> views = [];
    private readonly Action<Tile?> select;
    private readonly Action save;
    private LayoutPreset? layout;
    private Tile? selected;
    private TileView? dragging;
    private Point dragOrigin;
    private ResizeGesture? resizing;
    private bool editing;
    private bool excluded;
    private bool positionPending;

    public FloatingBar(Action<Tile?> select, Action save)
    {
        this.select = select;
        this.save = save;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.Manual;

        grip = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(28, 48, 61)),
            Child = new TextBlock
            {
                Text = "拖动此处移动技能栏",
                Foreground = Brushes.White,
                Margin = new Thickness(9, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            },
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
        SourceInitialized += (_, _) => ApplyMode();
        canvas.MouseLeftButtonDown += (_, e) =>
        {
            if (editing && e.Source == canvas) select(null);
        };
        canvas.MouseMove += MoveTile;
        canvas.MouseLeftButtonUp += (_, _) => { if (dragging is not null) canvas.ReleaseMouseCapture(); };
        canvas.LostMouseCapture += (_, _) =>
        {
            if (dragging is null) return;
            dragging = null;
            save();
        };
    }

    public IReadOnlyCollection<TileView> Views => views.Values;

    public void Load(LayoutPreset preset)
    {
        layout = preset;
        selected = null;
        positionPending = true;
        canvas.Children.Clear();
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
            view.UpdateAppearance();
        }
        UpdateSize();
        UpdateSelection();
    }

    public void SelectTile(Tile? tile)
    {
        selected = tile;
        UpdateSelection();
    }

    public void SetMode(bool edit, bool suspended, DisplayInfo[] displays)
    {
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

    public void ResetPosition(DisplayInfo[] displays)
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
        if (IsVisible && !HasVisibleGrip(displays)) ResetPosition(displays);
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
        double width = layout?.Tiles.Select(tile => tile.X + tile.Width).DefaultIfEmpty().Max() ?? 0;
        double height = layout?.Tiles.Select(tile => tile.Y + tile.Height).DefaultIfEmpty().Max() ?? 0;
        Width = Math.Max(300, width + HandleSize);
        Height = GripHeight + Math.Max(130, height + HandleSize);
    }

    private void BeginTileDrag(TileView view, MouseButtonEventArgs e)
    {
        if (!editing) return;
        select(view.Tile);
        dragOrigin = e.GetPosition(view);
        if (canvas.CaptureMouse()) dragging = view;
        e.Handled = true;
    }

    private void MoveTile(object sender, MouseEventArgs e)
    {
        if (dragging is not { } view || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(canvas);
        view.Tile.X = Math.Max(0, point.X - dragOrigin.X);
        view.Tile.Y = Math.Max(0, point.Y - dragOrigin.Y);
        view.UpdateAppearance();
        UpdateSize();
        UpdateSelection();
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
        if (!editing || selected is not { } tile || !views.ContainsKey(tile.Id))
        {
            chrome.Visibility = Visibility.Hidden;
            return;
        }

        chrome.Visibility = Visibility.Visible;
        // Leave enough room to distinguish the handles even for tiny captures.
        double width = Math.Max(tile.Width, 2 * HandleSize);
        double height = Math.Max(tile.Height, 2 * HandleSize);
        outline.Width = width;
        outline.Height = height;
        Canvas.SetLeft(outline, tile.X);
        Canvas.SetTop(outline, tile.Y);
        foreach (var (thumb, direction) in handles)
        {
            Canvas.SetLeft(thumb, Math.Max(0, tile.X + width * (direction.X + 1) / 2 - HandleSize / 2));
            Canvas.SetTop(thumb, Math.Max(0, tile.Y + height * (direction.Y + 1) / 2 - HandleSize / 2));
        }
    }

    private void BeginResize(Vector direction)
    {
        if (editing && selected is { } tile && views.TryGetValue(tile.Id, out var view))
        {
            resizing = new ResizeGesture(view, direction, Mouse.GetPosition(canvas),
                new Rect(tile.X, tile.Y, tile.Width, tile.Height));
        }
    }

    private void ResizeSelected()
    {
        if (resizing is not { } gesture) return;
        var view = gesture.View;
        var tile = view.Tile;
        var (position, scale) = TileGeometry.Resize(tile.Source, gesture.InitialBounds, gesture.Direction,
            Mouse.GetPosition(canvas) - gesture.Start);
        tile.X = position.X;
        tile.Y = position.Y;
        tile.Scale = scale;
        view.UpdateAppearance();
        UpdateSize();
        UpdateSelection();
    }

    private void EndResize()
    {
        if (resizing is not { } gesture) return;
        resizing = null;
        select(gesture.View.Tile);
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
