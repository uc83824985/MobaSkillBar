using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MobaSkillBar;

internal sealed class RegionMagnifier : FrameworkElement
{
    private const int Radius = 8;
    private const int PixelSize = 10;
    private const int Size = (2 * Radius + 1) * PixelSize;
    private static readonly Pen GridLine = MakePen(Color.FromArgb(75, 0, 0, 0), 1);
    private static readonly Pen SelectionLine = MakePen(Colors.Cyan, 2);
    private static readonly Pen CursorShadow = MakePen(Colors.Black, 3);
    private static readonly Pen CursorLine = MakePen(Colors.White, 1);
    private readonly BitmapSource snapshot;
    private PixelPoint center;
    private PixelRect? selection;

    public RegionMagnifier(BitmapSource snapshot)
    {
        this.snapshot = snapshot;
        Width = Height = Size;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public void Update(PixelPoint point, PixelRect? bounds)
    {
        if (center == point && selection == bounds) return;
        center = point;
        selection = bounds;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        int left = center.X - Radius;
        int top = center.Y - Radius;
        int x = Math.Max(0, left);
        int y = Math.Max(0, top);
        int right = Math.Min(snapshot.PixelWidth, center.X + Radius + 1);
        int bottom = Math.Min(snapshot.PixelHeight, center.Y + Radius + 1);
        var crop = new CroppedBitmap(snapshot, new Int32Rect(x, y, right - x, bottom - y));
        drawing.DrawRectangle(Brushes.Black, null, new Rect(0, 0, Size, Size));
        drawing.DrawImage(crop, new Rect((x - left) * PixelSize, (y - top) * PixelSize,
            crop.PixelWidth * PixelSize, crop.PixelHeight * PixelSize));

        for (int offset = PixelSize; offset < Size; offset += PixelSize)
        {
            drawing.DrawLine(GridLine, new Point(offset, 0), new Point(offset, Size));
            drawing.DrawLine(GridLine, new Point(0, offset), new Point(Size, offset));
        }

        if (selection is { } bounds)
        {
            drawing.DrawRectangle(null, SelectionLine, new Rect((bounds.Left - left) * PixelSize,
                (bounds.Top - top) * PixelSize, bounds.Width * PixelSize, bounds.Height * PixelSize));
        }

        var cursor = new Rect(Radius * PixelSize + 2, Radius * PixelSize + 2, PixelSize - 4, PixelSize - 4);
        drawing.DrawRectangle(null, CursorShadow, cursor);
        drawing.DrawRectangle(null, CursorLine, cursor);
    }

    private static Pen MakePen(Color color, double width)
    {
        var pen = new Pen(new SolidColorBrush(color), width);
        pen.Freeze();
        return pen;
    }
}
