using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MobaSkillBar;

internal sealed record CaptureTile(TileView View, ScreenRegion Source)
{
    public PixelRect Bounds => new(Source.X, Source.Y, Source.X + Source.Width, Source.Y + Source.Height);
}

internal sealed class CaptureBatch
{
    private const int MaxAreaMultiplier = 2;
    public PixelRect Bounds { get; private set; }
    public List<CaptureTile> Tiles { get; } = [];
    private long requestedPixels;

    private CaptureBatch(CaptureTile tile)
    {
        Bounds = tile.Bounds;
        Add(tile);
    }

    public static List<CaptureBatch> Group(IReadOnlyList<CaptureTile> tiles)
    {
        if (tiles.Count == 0) return [];
        var combined = new CaptureBatch(tiles[0]);
        for (int i = 1; i < tiles.Count; i++) combined.Add(tiles[i]);
        if (Area(combined.Bounds) <= MaxAreaMultiplier * combined.requestedPixels) return [combined];

        var batches = new List<CaptureBatch>();
        foreach (var tile in tiles.OrderBy(tile => tile.Source.Y).ThenBy(tile => tile.Source.X))
        {
            CaptureBatch? best = null;
            long bestGrowth = long.MaxValue;
            var bounds = tile.Bounds;
            foreach (var batch in batches)
            {
                long unionArea = Area(Union(batch.Bounds, bounds));
                long growth = unionArea - Area(batch.Bounds);
                if (unionArea > MaxAreaMultiplier * (batch.requestedPixels + Area(bounds)) || growth >= bestGrowth) continue;
                best = batch;
                bestGrowth = growth;
            }

            if (best is null) batches.Add(new CaptureBatch(tile));
            else best.Add(tile);
        }

        // A completed group can bridge a gap that none of its members could bridge alone.
        while (MergeAdjacent(batches)) { }
        return batches;
    }

    private static bool MergeAdjacent(List<CaptureBatch> batches)
    {
        for (int i = 0; i < batches.Count; i++)
        for (int j = i + 1; j < batches.Count; j++)
        {
            var first = batches[i];
            var second = batches[j];
            if (Area(Union(first.Bounds, second.Bounds)) > MaxAreaMultiplier * (first.requestedPixels + second.requestedPixels)) continue;
            foreach (var tile in second.Tiles) first.Add(tile);
            batches.RemoveAt(j);
            return true;
        }
        return false;
    }

    private void Add(CaptureTile tile)
    {
        Bounds = Union(Bounds, tile.Bounds);
        requestedPixels += Area(tile.Bounds);
        Tiles.Add(tile);
    }

    private static long Area(PixelRect rect) => (long)rect.Width * rect.Height;

    private static PixelRect Union(PixelRect a, PixelRect b) => new(Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top),
        Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom));
}

internal sealed class RegionReadback : IDisposable
{
    private readonly ID3D11Query completed;
    private readonly List<ID3D11Texture2D> staging = [];
    private List<CaptureBatch> batches = [];

    public RegionReadback(ID3D11Device device) => completed = device.CreateQuery(new QueryDescription(QueryType.Event));

    public void Submit(ID3D11Device device, ID3D11DeviceContext context, ID3D11Texture2D frame, List<CaptureBatch> plan)
    {
        while (staging.Count > plan.Count)
        {
            staging[^1].Dispose();
            staging.RemoveAt(staging.Count - 1);
        }

        for (int i = 0; i < plan.Count; i++)
        {
            var bounds = plan[i].Bounds;
            var buffer = GetStaging(device, i, (uint)bounds.Width, (uint)bounds.Height);
            var box = new Box(bounds.Left, bounds.Top, 0, bounds.Right, bounds.Bottom, 1);
            context.CopySubresourceRegion(buffer, 0, 0, 0, 0, frame, 0, box);
        }

        context.End(completed);
        // Submit this frame's copies once. Readiness checks use DoNotFlush.
        context.Flush();
        batches = plan;
    }

    public bool IsReady(ID3D11DeviceContext context)
    {
        var result = context.GetData(completed, IntPtr.Zero, 0, AsyncGetDataFlags.DoNotFlush);
        result.CheckError();
        return result.Code == 0;
    }

    public void Present(ID3D11DeviceContext context, List<TileView> active)
    {
        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var buffer = staging[i];
            context.Map(buffer, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None, out var map).CheckError();
            try
            {
                int rowPitch = checked((int)map.RowPitch);
                foreach (var tile in batch.Tiles)
                {
                    if (!active.Contains(tile.View) || tile.Source != tile.View.Tile.Source) continue;
                    int offset = checked((tile.Source.Y - batch.Bounds.Top) * rowPitch + (tile.Source.X - batch.Bounds.Left) * 4);
                    tile.View.UpdatePixels(map.DataPointer + offset, rowPitch);
                }
            }
            finally
            {
                context.Unmap(buffer, 0);
            }
        }
    }

    private ID3D11Texture2D GetStaging(ID3D11Device device, int index, uint width, uint height)
    {
        bool exists = index < staging.Count;
        if (exists)
        {
            var size = staging[index].Description;
            if (size.Width == width && size.Height == height) return staging[index];
        }

        var replacement = device.CreateTexture2D(new Texture2DDescription(Format.B8G8R8A8_UNorm,
            width, height, 1, 1, BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read));
        if (exists)
        {
            staging[index].Dispose();
            staging[index] = replacement;
        }
        else
        {
            staging.Add(replacement);
        }
        return replacement;
    }

    public void Dispose()
    {
        completed.Dispose();
        foreach (var buffer in staging) buffer.Dispose();
        staging.Clear();
    }
}
