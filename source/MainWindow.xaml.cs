using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MobaSkillBar;

public partial class MainWindow : Window
{
    private const long PreviewInterval = TimeSpan.TicksPerSecond / 60;
    private const string MixedValueText = "不同值";
    private readonly LayoutStore store = new();
    private readonly LayoutCollection layouts;
    private readonly FloatingBar bar;
    private readonly ScreenCapture capture = new();
    private readonly DispatcherTimer displayRefresh;
    private readonly DispatcherTimer saveTimer;
    private readonly AppearanceSetting[] appearanceSettings;
    private Tile[] selectedTiles = [];
    private DisplayInfo[] displays = [];
    private bool editing;
    private bool barHidden;
    private bool selecting;
    private bool refreshingControls = true;
    private bool previewActive;
    private long nextPresentation;
    private long lastPreviewActivity;
    private LayoutPreset Current => layouts.Active;
    private Tile? Selected => selectedTiles.Length == 1 ? selectedTiles[0] : null;

    public MainWindow()
    {
        InitializeComponent();
        appearanceSettings =
        [
            new(ScaleSlider, ScaleField, tile => tile.Scale, (tile, value) => tile.Scale = value, 1),
            new(OpacitySlider, OpacityField, tile => 1 - tile.Opacity, (tile, value) => tile.Opacity = 1 - value, 0),
            new(ContrastSlider, ContrastField, tile => tile.Contrast, (tile, value) => tile.Contrast = value, 1),
            new(SaturationSlider, SaturationField, tile => tile.Saturation, (tile, value) => tile.Saturation = value, 1)
        ];
        layouts = store.Load(out var warning);
        editing = Current.Tiles.Count == 0;
        bar = new FloatingBar(SelectFromBar, Save);
        displayRefresh = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        displayRefresh.Tick += (_, _) => RefreshDisplays();
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        saveTimer.Tick += (_, _) => Save();
        PreviewKeyDown += NudgeSelectedTiles;
        bar.PreviewKeyDown += NudgeSelectedTiles;
        ShowNotice(warning);
        RefreshPresetSelector();
        LoadLayout();
        Loaded += (_, _) =>
        {
            RefreshDisplays();
            UpdateMode();
            displayRefresh.Start();
        };
        Closed += (_, _) =>
        {
            displayRefresh.Stop();
            SetPreviewActive(false);
            if (saveTimer.IsEnabled) Save();
            bar.Close();
        };
    }

    private void RefreshDisplays()
    {
        var previous = displays;
        displays = Desktop.GetDisplays();
        if (!previous.SequenceEqual(displays) && displays.Length > 0) bar.KeepVisible(displays);
        if (previewActive && Environment.TickCount64 - lastPreviewActivity >= 1000) UpdateCapture();
    }

    private void SetPreviewActive(bool active)
    {
        if (active == previewActive) return;
        previewActive = active;
        if (active)
        {
            nextPresentation = 0;
            CompositionTarget.Rendering += RenderPreview;
        }
        else
        {
            CompositionTarget.Rendering -= RenderPreview;
            capture.Dispose();
        }
    }

    private void RenderPreview(object? sender, EventArgs e)
    {
        long now = ((RenderingEventArgs)e).RenderingTime.Ticks;
        // Allow small compositor jitter while keeping a 60 Hz timeline on higher-refresh displays.
        if (now + TimeSpan.TicksPerMillisecond < nextPresentation) return;
        if (nextPresentation == 0 || now - nextPresentation > PreviewInterval) nextPresentation = now;
        nextPresentation += PreviewInterval;
        UpdateCapture();
    }

    private void UpdateCapture()
    {
        lastPreviewActivity = Environment.TickCount64;
        capture.Update(displays, bar.Views, bar.CaptureObstructions());
        UpdateSourceSummary();
    }

    private void ToggleMode(object sender, RoutedEventArgs e)
    {
        editing = !editing;
        UpdateMode();
    }

    private void UpdateMode()
    {
        ModeButton.Content = editing ? "完成编辑" : "编辑悬浮栏";
        bool canShowBar = editing || bar.Views.Count > 0;
        BarVisibilityButton.IsEnabled = canShowBar;
        BarVisibilityButton.Content = barHidden || !canShowBar ? "显示悬浮栏" : "隐藏悬浮栏";
        ModeHint.Text = barHidden ? "悬浮栏已隐藏。" : editing
            ? "拖动空白处框选，拖动选中格一起移动。WASD 或方向键微调 1 像素，单选时拖动控制点缩放。"
            : "显示模式 · 鼠标可穿透悬浮栏。";
        if (!IsLoaded) return;
        bar.SetMode(editing, selecting || barHidden, displays);
        SetPreviewActive(bar.IsVisible && bar.Views.Count > 0);
    }

    private void NudgeSelectedTiles(object sender, KeyEventArgs e)
    {
        if (!editing || selecting || Keyboard.Modifiers != ModifierKeys.None) return;
        if (sender == this && !TileList.IsKeyboardFocusWithin && e.OriginalSource is not (Button or MainWindow)) return;
        if (!bar.NudgeSelection(Desktop.KeyStep(e.Key))) return;
        e.Handled = true;
        ScheduleSave();
    }

    private void ToggleBarVisibility(object sender, RoutedEventArgs e)
    {
        barHidden = !barHidden;
        UpdateMode();
    }

    private void AddTile(object sender, RoutedEventArgs e) => PickRegion(null);

    private void ReplaceTile(object sender, RoutedEventArgs e)
    {
        if (Selected is { } tile) PickRegion(tile);
    }

    private void PickRegion(Tile? tile)
    {
        selecting = true;
        RefreshDisplays();
        UpdateMode();
        Hide();
        RegionPicker.ShowAll(displays, region =>
        {
            selecting = false;
            if (region is not null)
            {
                if (tile is null)
                {
                    tile = AddRegion(region);
                }
                else
                {
                    tile.Source = region;
                    bar.Views.First(view => view.Tile == tile).SetStatus("正在捕获");
                }
                editing = true;
                bar.Synchronize();
                RefreshTileList(tile);
                Save();
            }
            Show();
            Activate();
            UpdateMode();
        });
    }

    private Tile AddRegion(ScreenRegion region)
    {
        int number = 1;
        while (Current.Tiles.Any(tile => tile.Name == $"技能格 {number}")) number++;
        var tile = new Tile { Name = $"技能格 {number}", Source = region };
        if (Current.Tiles.Count > 0)
        {
            var last = Current.Tiles[^1];
            var dpi = VisualTreeHelper.GetDpi(bar);
            tile.X = last.X + last.DisplaySize(dpi).Width + 6;
            tile.Y = last.Y;
            if (tile.X + tile.DisplaySize(dpi).Width > 460)
            {
                tile.X = 0;
                tile.Y = Current.Tiles.Max(item => item.Y + item.DisplaySize(dpi).Height) + 6;
            }
        }
        Current.Tiles.Add(tile);
        return tile;
    }

    private void RemoveTile(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } tile) return;
        int index = Current.Tiles.IndexOf(tile);
        Current.Tiles.RemoveAt(index);
        bar.Synchronize();
        RefreshTileList(Current.Tiles.ElementAtOrDefault(Math.Min(index, Current.Tiles.Count - 1)));
        UpdateMode();
        Save();
    }

    private void ChangeAppearance(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!refreshingControls && sender is Slider slider) ApplyAppearance(SettingFor(slider), e.NewValue);
    }

    private AppearanceSetting SettingFor(Slider slider) => appearanceSettings.First(setting => setting.Slider == slider);

    private static bool SameValue(double left, double right) => Math.Abs(left - right) < 1e-9;

    private void ApplyAppearance(AppearanceSetting setting, double value)
    {
        if (selectedTiles.Length == 0) return;
        value = Math.Clamp(value, setting.Slider.Minimum, setting.Slider.Maximum);
        bool changed = false;
        foreach (var tile in selectedTiles)
        {
            if (SameValue(setting.Read(tile), value)) continue;
            setting.Write(tile, value);
            changed = true;
        }

        RefreshAppearance(setting);
        if (!changed) return;
        bar.Synchronize();
        ScheduleSave();
    }

    private void RefreshAppearance(AppearanceSetting setting)
    {
        double first = setting.Read(selectedTiles[0]);
        bool mixed = selectedTiles.Any(tile => !SameValue(setting.Read(tile), first));
        setting.DisplayText = mixed ? MixedValueText : PercentageValue.Format(first, setting.Field.Language.GetSpecificCulture());
        if (setting.Field.Text != setting.DisplayText) setting.Field.Text = setting.DisplayText;
        setting.Slider.Opacity = mixed ? 0.65 : 1;
        setting.Slider.IsMoveToPointEnabled = mixed;
        setting.Slider.ToolTip = mixed ? "不同值，拖动滑块可统一修改" : null;

        bool wasRefreshing = refreshingControls;
        refreshingControls = true;
        setting.Slider.Value = first;
        refreshingControls = wasRefreshing;
    }

    private void ScheduleSave()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void ResetSetting(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not Slider slider) return;
        var setting = SettingFor(slider);
        ApplyAppearance(setting, setting.DefaultValue);
    }

    private void FocusMixedPercentage(object sender, MouseButtonEventArgs e)
    {
        var field = (TextBox)sender;
        if (field.IsKeyboardFocusWithin || field.Text != MixedValueText) return;
        field.Focus();
        e.Handled = true;
    }

    private void SelectMixedPercentage(object sender, KeyboardFocusChangedEventArgs e)
    {
        var field = (TextBox)sender;
        if (field.Text == MixedValueText) field.SelectAll();
    }

    private void SelectPercentage(object sender, MouseButtonEventArgs e)
    {
        var field = (TextBox)sender;
        field.Focus();
        field.SelectAll();
        e.Handled = true;
    }

    private void FinishPercentage(TextBox field, bool commit)
    {
        if (field.Tag is not Slider slider) return;
        var setting = SettingFor(slider);
        if (field.Text == setting.DisplayText) return;
        if (commit && PercentageValue.TryParse(field.Text, field.Language.GetSpecificCulture(), out double value))
            ApplyAppearance(setting, value);
        if (field.Text != setting.DisplayText) field.Text = setting.DisplayText;
    }

    private void PercentageKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;
        var field = (TextBox)sender;
        FinishPercentage(field, commit: e.Key == Key.Enter);
        field.SelectAll();
        e.Handled = true;
    }

    private void CommitPercentage(object sender, KeyboardFocusChangedEventArgs e) =>
        FinishPercentage((TextBox)sender, commit: true);

    private void LowerTile(object sender, RoutedEventArgs e) => MoveLayer(-1);
    private void RaiseTile(object sender, RoutedEventArgs e) => MoveLayer(1);

    private void MoveLayer(int step)
    {
        if (Selected is not { } tile) return;
        var tiles = Current.Tiles;
        int index = tiles.IndexOf(tile);
        int target = index + step;
        if (index < 0 || target < 0 || target >= tiles.Count) return;
        (tiles[index], tiles[target]) = (tiles[target], tiles[index]);
        bar.Synchronize();
        RefreshTileList(tile);
        Save();
    }

    private void SelectTile(object sender, SelectionChangedEventArgs e)
    {
        if (!refreshingControls) RefreshSelection();
    }

    private void ClearListSelectionOnBlankClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not Visual hit || ItemsControl.ContainerFromElement(TileList, hit) is not null) return;
        for (DependencyObject? element = hit; element is Visual && element != TileList; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ScrollBar) return;
        }
        TileList.UnselectAll();
        TileList.Focus();
    }

    private void SelectFromBar(IReadOnlyCollection<Tile> tiles)
    {
        refreshingControls = true;
        TileList.UnselectAll();
        foreach (var tile in tiles) TileList.SelectedItems.Add(tile);
        refreshingControls = false;
        RefreshSelection();
    }

    private void RefreshTileList(Tile? tile)
    {
        refreshingControls = true;
        TileList.ItemsSource = Current.Tiles.ToArray();
        TileList.SelectedItem = tile;
        refreshingControls = false;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        // A selection change can arrive before a text field loses keyboard focus.
        if (Keyboard.FocusedElement is TextBox field && field.Tag is Slider) FinishPercentage(field, commit: true);
        selectedTiles = TileList.SelectedItems.Cast<Tile>().ToArray();
        var tile = Selected;
        bar.SelectTiles(selectedTiles);
        Appearance.Visibility = selectedTiles.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        RenameTileButton.IsEnabled = ReplaceButton.IsEnabled = RemoveButton.IsEnabled = tile is not null;
        int index = tile is null ? -1 : Current.Tiles.IndexOf(tile);
        LowerButton.IsEnabled = index > 0;
        RaiseButton.IsEnabled = index >= 0 && index < Current.Tiles.Count - 1;
        if (selectedTiles.Length == 0) return;
        foreach (var setting in appearanceSettings) RefreshAppearance(setting);
        if (tile is null) SourceSummary.Text = $"已选中 {selectedTiles.Length} 个技能格";
        else UpdateSourceSummary();
    }

    private void UpdateSourceSummary()
    {
        if (Selected is not { } tile) return;
        string? status = bar.Views.FirstOrDefault(view => view.Tile == tile)?.Status;
        string summary = $"来源 {tile.Source.Width} × {tile.Source.Height} px";
        if (status is not null) summary += $" · {status}";
        if (SourceSummary.Text != summary) SourceSummary.Text = summary;
    }

    private void CreateLayout(object sender, RoutedEventArgs e)
    {
        if (AskName("新建布局", "布局名称", "", layouts.Presets.Select(preset => preset.Name)) is not { } name) return;
        var preset = new LayoutPreset { Name = name };
        layouts.Presets.Add(preset);
        layouts.ActiveId = preset.Id;
        RefreshPresetSelector();
        LoadLayout();
        Save();
    }

    private void RenameLayout(object sender, RoutedEventArgs e)
    {
        if (AskName("重命名布局", "布局名称", Current.Name,
            layouts.Presets.Where(preset => preset != Current).Select(preset => preset.Name)) is not { } name) return;
        Current.Name = name;
        Save();
    }

    private void DeleteLayout(object sender, RoutedEventArgs e)
    {
        var preset = Current;
        if (MessageBox.Show(this, $"删除「{preset.Name}」及其中的技能格？", "删除布局", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        layouts.Presets.Remove(preset);
        if (layouts.Presets.Count == 0) layouts.Presets.Add(new LayoutPreset());
        layouts.ActiveId = layouts.Presets[0].Id;
        RefreshPresetSelector();
        LoadLayout();
        Save();
    }

    private void SwitchLayout(object sender, SelectionChangedEventArgs e)
    {
        if (refreshingControls || PresetBox.SelectedItem is not LayoutPreset preset) return;
        layouts.ActiveId = preset.Id;
        LoadLayout();
        Save();
    }

    private void LoadLayout()
    {
        bar.Load(Current);
        if (Current.Tiles.Count == 0) editing = true;
        RefreshTileList(Current.Tiles.FirstOrDefault());
        UpdateMode();
    }

    private void RefreshPresetSelector()
    {
        refreshingControls = true;
        PresetBox.ItemsSource = layouts.Presets.ToArray();
        PresetBox.SelectedItem = Current;
        refreshingControls = false;
    }

    private void RenameTile(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } tile || AskName("重命名技能格", "技能格名称", tile.Name) is not { } name) return;
        tile.Name = name;
        RefreshTileList(tile);
        Save();
    }

    private string? AskName(string title, string field, string initial, IEnumerable<string>? takenNames = null)
    {
        var input = new TextBox { Text = initial, MinWidth = 240, MaxLength = 60, Margin = new Thickness(0, 5, 0, 10) };
        var label = new Label { Content = field, Target = input, Padding = new Thickness(0) };
        var confirm = new Button { Content = "保存", MinWidth = 68, IsDefault = true };
        var cancel = new Button { Content = "取消", MinWidth = 68, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);
        var prompt = new StackPanel { Margin = new Thickness(16) };
        prompt.Children.Add(label);
        prompt.Children.Add(input);
        prompt.Children.Add(buttons);
        var dialog = new Window
        {
            Owner = this, Title = title, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Content = prompt
        };
        confirm.Click += (_, _) =>
        {
            string name = input.Text.Trim();
            if (name.Length == 0 || takenNames?.Contains(name, StringComparer.OrdinalIgnoreCase) == true)
            {
                MessageBox.Show(dialog, name.Length == 0 ? $"请输入{field}。" : $"该{field}已被使用。", title, MessageBoxButton.OK, MessageBoxImage.Information);
                input.Focus();
                return;
            }
            dialog.DialogResult = true;
        };
        dialog.Loaded += (_, _) => { Keyboard.Focus(input); input.SelectAll(); };
        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    private void Save()
    {
        saveTimer.Stop();
        try
        {
            store.Save(layouts);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            ShowNotice($"布局保存失败：{error.Message}");
        }
    }

    private void ShowNotice(string? message)
    {
        Notice.Text = message;
        Notice.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private sealed record AppearanceSetting(Slider Slider, TextBox Field, Func<Tile, double> Read,
        Action<Tile, double> Write, double DefaultValue)
    {
        public string DisplayText { get; set; } = "";
    }
}
