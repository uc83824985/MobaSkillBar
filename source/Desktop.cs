using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Screen = System.Windows.Forms.Screen;

namespace MobaSkillBar;

internal sealed record DisplayInfo(string Id, IntPtr Handle, PixelRect Bounds, PixelRect WorkArea, bool Primary);

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PixelPoint(int X, int Y);

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool Intersects(PixelRect other) => Left < other.Right && Right > other.Left &&
        Top < other.Bottom && Bottom > other.Top;
}

internal static class Desktop
{
    private const int ExtendedStyle = -20;
    private const int Transparent = 0x20;
    private const int NoActivate = 0x08000000;

    public static DisplayInfo[] GetDisplays() => Screen.AllScreens.Select(screen =>
    {
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;
        var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
        string id = EnumDisplayDevices(screen.DeviceName, 0, ref device, 1) && !string.IsNullOrEmpty(device.Id)
            ? device.Id : screen.DeviceName;
        return new DisplayInfo(id, MonitorFromPoint(new PixelPoint(bounds.Left, bounds.Top), 0),
            new PixelRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom),
            new PixelRect(work.Left, work.Top, work.Right, work.Bottom), screen.Primary);
    }).ToArray();

    public static PixelPoint Cursor => GetCursorPos(out var point) ? point : default;

    public static void MoveCursor(PixelPoint point) => SetCursorPos(point.X, point.Y);

    public static BitmapSource? Snapshot(PixelRect bounds)
    {
        try
        {
            using var pixels = new Drawing.Bitmap(bounds.Width, bounds.Height, Drawing.Imaging.PixelFormat.Format32bppRgb);
            CopyDesktopPixels(pixels, bounds);

            var area = new Drawing.Rectangle(0, 0, pixels.Width, pixels.Height);
            var data = pixels.LockBits(area, Drawing.Imaging.ImageLockMode.ReadOnly, pixels.PixelFormat);
            try
            {
                var snapshot = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Bgr32,
                    null, data.Scan0, checked(data.Stride * pixels.Height), data.Stride);
                snapshot.Freeze();
                return snapshot;
            }
            finally
            {
                pixels.UnlockBits(data);
            }
        }
        catch (ExternalException error)
        {
            System.Diagnostics.Trace.WriteLine(error);
            return null;
        }
    }

    private static void CopyDesktopPixels(Drawing.Bitmap pixels, PixelRect bounds)
    {
        const uint sourceCopy = 0x00CC0020;
        const uint captureLayered = 0x40000000;
        using var graphics = Drawing.Graphics.FromImage(pixels);
        IntPtr destination = graphics.GetHdc();
        IntPtr screen = GetDC(IntPtr.Zero);
        try
        {
            if (screen == IntPtr.Zero || !BitBlt(destination, 0, 0, bounds.Width, bounds.Height,
                screen, bounds.Left, bounds.Top, sourceCopy | captureLayered))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
            graphics.ReleaseHdc(destination);
        }
    }

    public static PixelRect WindowBounds(Window window) =>
        GetWindowRect(new WindowInteropHelper(window).Handle, out var bounds) ? bounds : default;

    public static void Position(Window window, PixelRect bounds, bool resize)
    {
        const uint noActivate = 0x10;
        const uint noZOrder = 0x04;
        const uint noSize = 0x01;
        SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, bounds.Left, bounds.Top,
            bounds.Width, bounds.Height, noActivate | noZOrder | (resize ? 0 : noSize));
    }

    public static bool ExcludeFromCapture(Window window) =>
        SetWindowDisplayAffinity(new WindowInteropHelper(window).EnsureHandle(), 0x11);

    public static void SetClickThrough(Window window, bool enabled)
    {
        IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
        int style = GetWindowLong(hwnd, ExtendedStyle);
        int flags = Transparent | NoActivate;
        SetWindowLong(hwnd, ExtendedStyle, enabled ? style | flags : style & ~flags);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public int State;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string device, uint number, ref DisplayDevice info, uint flags);

    [DllImport("user32")]
    private static extern IntPtr MonitorFromPoint(PixelPoint point, uint flags);

    [DllImport("user32")]
    private static extern bool GetCursorPos(out PixelPoint point);

    [DllImport("user32")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32", SetLastError = true)]
    private static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height,
        IntPtr source, int sourceX, int sourceY, uint operation);

    [DllImport("user32")]
    private static extern bool GetWindowRect(IntPtr window, out PixelRect rect);

    [DllImport("user32")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32")]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);

    [DllImport("user32", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);
}
