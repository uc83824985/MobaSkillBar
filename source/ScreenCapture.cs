using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace MobaSkillBar;

internal sealed class ScreenCapture : IDisposable
{
    private const DirectXPixelFormat CaptureFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
    private readonly Dictionary<string, MonitorCapture> monitors = [];
    private readonly Dictionary<string, long> retryAfter = [];
    private ID3D11Device? device;
    private ID3D11DeviceContext? context;
    private IDirect3DDevice? winrtDevice;

    public void Update(DisplayInfo[] displays, IReadOnlyCollection<TileView> tiles, PixelRect[] coveredAreas)
    {
        var active = tiles.Select(tile => tile.Tile.Source.DisplayId).ToHashSet();
        var present = displays.ToLookup(display => display.Id);
        foreach (var pair in monitors.ToArray())
        {
            if (active.Contains(pair.Key) && present[pair.Key].Any(display => pair.Value.Matches(display))) continue;
            pair.Value.Dispose();
            monitors.Remove(pair.Key);
            retryAfter.Remove(pair.Key);
        }
        foreach (string id in retryAfter.Keys.ToArray())
        {
            if (!active.Contains(id) || !present.Contains(id)) retryAfter.Remove(id);
        }

        foreach (var group in tiles.GroupBy(view => view.Tile.Source.DisplayId))
        {
            var display = present[group.Key].FirstOrDefault();
            var candidates = new List<TileView>();
            foreach (var view in group)
            {
                var source = view.Tile.Source;
                if (display is null || !source.Fits(display.Bounds.Width, display.Bounds.Height))
                {
                    view.SetStatus("来源不可用");
                    continue;
                }
                var bounds = display.Bounds;
                var region = new PixelRect(bounds.Left + source.X, bounds.Top + source.Y,
                    bounds.Left + source.X + source.Width, bounds.Top + source.Y + source.Height);
                if (coveredAreas.Any(region.Intersects))
                {
                    view.SetStatus("请移开悬浮栏");
                    continue;
                }
                candidates.Add(view);
            }
            if (display is not null && candidates.Count > 0) UpdateMonitor(display, candidates);
        }
    }

    private void UpdateMonitor(DisplayInfo display, List<TileView> views)
    {
        long now = Environment.TickCount64;
        if (retryAfter.TryGetValue(display.Id, out long retry) && now < retry)
        {
            foreach (var view in views) view.SetStatus("捕获不可用");
            return;
        }

        try
        {
            EnsureDevice();
            if (!monitors.TryGetValue(display.Id, out var capture))
            {
                capture = new MonitorCapture(display, winrtDevice!);
                monitors.Add(display.Id, capture);
                retryAfter.Remove(display.Id);
            }

            var frame = capture.NextFrame();
            if (frame is null)
            {
                capture.PresentReady(context!, views);
                return;
            }
            SizeInt32 size;
            using (frame)
            {
                size = frame.ContentSize;
                if (size.Width == capture.Size.Width && size.Height == capture.Size.Height)
                {
                    capture.PresentReady(context!, views);
                    if (!capture.HasReadbackSlot) return;

                    var plan = capture.PlanReadback(views);
                    if (plan.Count == 0) return;

                    using var texture = GetTexture(frame.Surface);
                    capture.Submit(device!, context!, texture, plan);
                    return;
                }
            }

            if (size.Width > 0 && size.Height > 0) capture.Resize(winrtDevice!, size);
        }
        catch (Exception error) when (error is COMException or SharpGenException or NotSupportedException or InvalidCastException
            or UnauthorizedAccessException or ArgumentException or ObjectDisposedException)
        {
            if (monitors.Remove(display.Id, out var failed)) failed.Dispose();
            if (device?.DeviceRemovedReason.Failure == true) Dispose();
            retryAfter[display.Id] = now + 1500;
            System.Diagnostics.Trace.WriteLine(error);
            foreach (var view in views) view.SetStatus("捕获不可用");
        }
    }

    private void EnsureDevice()
    {
        if (winrtDevice is not null) return;
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows Graphics Capture is unavailable.");

        var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            null!, out device, out _, out context);
        if (result.Failure)
        {
            D3D11.D3D11CreateDevice(null, DriverType.Warp, DeviceCreationFlags.BgraSupport,
                null!, out device, out _, out context).CheckError();
        }

        try
        {
            using var dxgi = device!.QueryInterface<IDXGIDevice>();
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var pointer));
            try { winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(pointer); }
            finally { Marshal.Release(pointer); }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static GraphicsCaptureItem CreateItem(IntPtr monitor)
    {
        var factory = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        Guid id = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        Marshal.ThrowExceptionForHR(factory.CreateForMonitor(monitor, ref id, out var item));
        try { return MarshalInterface<GraphicsCaptureItem>.FromAbi(item); }
        finally { Marshal.Release(item); }
    }

    private static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var access = WinRT.CastExtensions.As<IDirect3DDxgiInterfaceAccess>(surface);
        Guid id = typeof(ID3D11Texture2D).GUID;
        Marshal.ThrowExceptionForHR(access.GetInterface(ref id, out var texture));
        return new ID3D11Texture2D(texture);
    }

    public void Dispose()
    {
        foreach (var capture in monitors.Values) capture.Dispose();
        monitors.Clear();
        retryAfter.Clear();
        winrtDevice?.Dispose();
        context?.Dispose();
        device?.Dispose();
        winrtDevice = null;
        context = null;
        device = null;
    }

    private sealed class MonitorCapture : IDisposable
    {
        private readonly DisplayInfo display;
        private readonly GraphicsCaptureItem item;
        private readonly Direct3D11CaptureFramePool frames;
        private readonly GraphicsCaptureSession session;
        private readonly RegionReadback?[] readbacks = new RegionReadback?[2];
        private readonly Queue<RegionReadback> pending = new(2);
        private int nextReadback;
        private List<CaptureTile> plannedTiles = [];
        private List<CaptureBatch> readbackPlan = [];
        private volatile bool closed;
        public SizeInt32 Size { get; private set; }

        public MonitorCapture(DisplayInfo display, IDirect3DDevice device)
        {
            this.display = display;
            item = CreateItem(display.Handle);
            Size = item.Size;
            frames = Direct3D11CaptureFramePool.CreateFreeThreaded(device, CaptureFormat, 2, Size);
            GraphicsCaptureSession? started = null;
            try
            {
                started = frames.CreateCaptureSession(item);
                started.IsCursorCaptureEnabled = false;
                item.Closed += OnClosed;
                started.StartCapture();
                session = started;
            }
            catch
            {
                item.Closed -= OnClosed;
                started?.Dispose();
                frames.Dispose();
                throw;
            }
        }

        public bool Matches(DisplayInfo current) => !closed && display.Handle == current.Handle && display.Bounds == current.Bounds;

        public Direct3D11CaptureFrame? NextFrame()
        {
            var first = frames.TryGetNextFrame();
            if (first is null) return null;
            try
            {
                var newer = frames.TryGetNextFrame();
                if (newer is null) return first;
                first.Dispose();
                return newer;
            }
            catch
            {
                first.Dispose();
                throw;
            }
        }

        public void Resize(IDirect3DDevice device, SizeInt32 size)
        {
            ResetReadbacks();
            frames.Recreate(device, CaptureFormat, 2, size);
            Size = size;
        }

        public bool HasReadbackSlot => pending.Count < readbacks.Length;

        public List<CaptureBatch> PlanReadback(List<TileView> views)
        {
            if (PlanMatches(views)) return readbackPlan;

            plannedTiles = new List<CaptureTile>(views.Count);
            foreach (var view in views)
            {
                var region = view.Tile.Source;
                if (region.Fits(Size.Width, Size.Height)) plannedTiles.Add(new CaptureTile(view, region));
                else view.SetStatus("来源不可用");
            }
            readbackPlan = CaptureBatch.Group(plannedTiles);
            return readbackPlan;
        }

        private bool PlanMatches(List<TileView> views)
        {
            if (views.Count != plannedTiles.Count) return false;
            for (int i = 0; i < views.Count; i++)
            {
                var tile = plannedTiles[i];
                if (tile.View != views[i] || tile.Source != views[i].Tile.Source) return false;
            }
            return true;
        }

        public void Submit(ID3D11Device device, ID3D11DeviceContext context, ID3D11Texture2D texture, List<CaptureBatch> plan)
        {
            var slot = readbacks[nextReadback] ??= new RegionReadback(device);
            slot.Submit(device, context, texture, plan);
            pending.Enqueue(slot);
            nextReadback = (nextReadback + 1) % readbacks.Length;
        }

        public void PresentReady(ID3D11DeviceContext context, List<TileView> views)
        {
            RegionReadback? ready = null;
            while (pending.TryPeek(out var slot) && slot.IsReady(context)) ready = pending.Dequeue();
            ready?.Present(context, views);
        }

        private void ResetReadbacks()
        {
            foreach (var slot in readbacks) slot?.Dispose();
            Array.Clear(readbacks);
            pending.Clear();
            nextReadback = 0;
            plannedTiles = [];
            readbackPlan = [];
        }

        private void OnClosed(GraphicsCaptureItem sender, object args) => closed = true;

        public void Dispose()
        {
            item.Closed -= OnClosed;
            session.Dispose();
            frames.Dispose();
            ResetReadbacks();
        }
    }

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);
        [PreserveSig] int CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
    }

    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig] int GetInterface(ref Guid iid, out IntPtr result);
    }

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr device);
}
