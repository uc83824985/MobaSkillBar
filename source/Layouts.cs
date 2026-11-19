using System.IO;
using System.Windows;
using System.Text.Json;

namespace MobaSkillBar;

internal sealed record ScreenRegion(string DisplayId, int X, int Y, int Width, int Height)
{
    public bool Fits(int width, int height) => X >= 0 && Y >= 0 && Width > 0 && Height > 0 &&
        X <= width && Y <= height && Width <= width - X && Height <= height - Y;
}

internal sealed class Tile
{
    public const double MinimumScale = 0.25;
    public const double MaximumScale = 4;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "技能格";
    public ScreenRegion Source { get; set; } = new("", 0, 0, 1, 1);
    public double X { get; set; }
    public double Y { get; set; }
    public double Scale { get; set; } = 1;
    public double Opacity { get; set; } = 1;
    public double Contrast { get; set; } = 1;
    public double Saturation { get; set; } = 1;
    private double DisplayScale => Math.Min(Scale, GetMaximumDisplayScale(Source));
    public Size DisplaySize(DpiScale dpi) =>
        new(Source.Width * DisplayScale / dpi.DpiScaleX, Source.Height * DisplayScale / dpi.DpiScaleY);

    public static double GetMaximumDisplayScale(ScreenRegion source) =>
        Math.Clamp(1600.0 / Math.Max(source.Width, source.Height), 1, MaximumScale);
}

internal sealed class LayoutPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "默认布局";
    public int? Left { get; set; }
    public int? Top { get; set; }
    public List<Tile> Tiles { get; set; } = [];
}

internal sealed class LayoutCollection
{
    public Guid ActiveId { get; set; }
    public List<LayoutPreset> Presets { get; set; } = [new()];

    public LayoutPreset Active => Presets.FirstOrDefault(preset => preset.Id == ActiveId) ?? Presets[0];
}

internal sealed class LayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true
    };
    private readonly string path = FindConfigPath();

    private static string FindConfigPath()
    {
        // Build outputs live under source/. Standalone copies keep config beside the executable.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (directory.Name == "source" && directory.Parent is { } root &&
                File.Exists(Path.Combine(directory.FullName, "MobaSkillBar.csproj")))
                return Path.Combine(root.FullName, "config", "layouts.json");
        }
        return Path.Combine(AppContext.BaseDirectory, "config", "layouts.json");
    }

    public LayoutCollection Load(out string? warning)
    {
        warning = null;
        try
        {
            bool importLegacy = !File.Exists(path);
            string input = importLegacy ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MobaSkillBar", "layouts.json") : path;
            using var stream = File.OpenRead(input);
            var layouts = JsonSerializer.Deserialize<LayoutCollection>(stream, JsonOptions);
            if (layouts?.Presets is not { Count: > 0 } || layouts.Presets.Any(preset =>
                preset is null || string.IsNullOrWhiteSpace(preset.Name) || preset.Tiles is null ||
                preset.Tiles.Any(tile => tile is null || tile.Source is null || string.IsNullOrWhiteSpace(tile.Name) ||
                    !tile.Source.Fits(int.MaxValue, int.MaxValue) || tile.Scale is < Tile.MinimumScale or > Tile.MaximumScale ||
                    tile.Opacity is < 0 or > 1 || tile.Contrast is < 0.25 or > 3 || tile.Saturation is < 0 or > 2 ||
                    tile.X < 0 || tile.Y < 0)))
            {
                throw new InvalidDataException("布局文件中的设置无效。");
            }
            if (importLegacy)
            {
                try { Save(layouts); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    warning = $"布局已从旧目录读取，迁入 config 失败：{error.Message}";
                }
            }
            return layouts;
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            warning = $"布局读取失败：{error.Message}";
        }
        return new LayoutCollection();
    }

    public void Save(LayoutCollection layouts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, layouts, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
