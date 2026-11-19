using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
