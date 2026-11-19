using System.Windows;

namespace MobaSkillBar;

internal static class TileGeometry
{
    public static (Point Position, double Scale) Resize(ScreenRegion source, Rect start, DpiScale dpi, Vector direction, Vector movement)
    {
        // Project motion along the handle's resize axis, so a corner keeps the aspect ratio.
        var axis = new Vector(direction.X * start.Width, direction.Y * start.Height);
        double factor = 1 + Vector.Multiply(movement, axis) / axis.LengthSquared;
        double initialScale = start.Width * dpi.DpiScaleX / source.Width;
        double maxDisplayScale = Tile.GetMaximumDisplayScale(source);
        double scale = Math.Clamp(initialScale * factor, Tile.MinimumScale, Math.Max(Tile.MinimumScale, maxDisplayScale));
        double newDisplayScale = Math.Min(scale, maxDisplayScale);

        // Keep the opposite corner (or opposite edge midpoint) at the same position.
        double pivotX = (1 - direction.X) / 2;
        double pivotY = (1 - direction.Y) / 2;
        double x = start.X + (start.Width - source.Width * newDisplayScale / dpi.DpiScaleX) * pivotX;
        double y = start.Y + (start.Height - source.Height * newDisplayScale / dpi.DpiScaleY) * pivotY;
        return (new Point(Math.Max(0, x), Math.Max(0, y)), scale);
    }
}
