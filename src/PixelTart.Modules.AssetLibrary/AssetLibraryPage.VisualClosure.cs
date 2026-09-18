using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetLibraryPage
{
    private VisualAnalysisSurfacePayload? _visualPayload;
    private BitmapSource? _zoneOriginal;
    private BitmapSource[] _zoneHoverFrames = [];
    private int _visualRevision;
    private string _visualSourceKind = "Asset";
    private Guid? _visualContainerId;
    private DispatcherTimer? _visualToastTimer;

    private void CopyPaletteValue(string value)
    {
        try { Clipboard.SetText(value); ShowVisualToast("已复制 " + value); }
        catch (System.Runtime.InteropServices.ExternalException) { ShowVisualToast("剪贴板暂时不可用，请重试。"); }
    }
    private void ShowVisualToast(string message)
    {
        VisualCopyToast.Text = message; VisualCopyToast.Visibility = Visibility.Visible;
        _visualToastTimer?.Stop();
        _visualToastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _visualToastTimer.Tick += (_, _) => { _visualToastTimer.Stop(); VisualCopyToast.Visibility = Visibility.Collapsed; };
        _visualToastTimer.Start();
    }
    public void SelectVisualZone(int? zone) => VisualZoneMapImage.Source = zone is >= 0 and <= 10 && _zoneHoverFrames.Length == 11
        ? _zoneHoverFrames[zone.Value] : _zoneOriginal;

    private void PopulateZoneRows(CombinedVisualAnalysisResult result)
    {
        VisualZoneRows.Items.Clear();
        for (var zone = 0; zone <= 10; zone++)
        {
            var selected = zone;
            var row = new Button { Content = $"Zone {RomanZone(zone)}  {result.Zones[zone]:P1}", Padding = new(7, 4, 7, 4), Margin = new(2) };
            row.SetResourceReference(StyleProperty, "PixelTart.Button.Ghost");
            row.MouseEnter += (_, _) => SelectVisualZone(selected);
            row.MouseLeave += (_, _) => SelectVisualZone(null);
            row.GotKeyboardFocus += (_, _) => SelectVisualZone(selected);
            row.LostKeyboardFocus += (_, _) => SelectVisualZone(null);
            VisualZoneRows.Items.Add(row);
        }
    }
    private async Task LoadVisualProjectsAsync()
    {
        var previous = (VisualProjectPicker.SelectedItem as VisualProjectChoice)?.Id;
        var choices = (await _viewModel.CanvasProjectsAsync()).Where(item => item.Id.HasValue)
            .Select(item => new VisualProjectChoice(item.Id!.Value, item.Name)).ToArray();
        VisualProjectPicker.ItemsSource = choices;
        VisualProjectPicker.SelectedItem = choices.FirstOrDefault(item => item.Id == previous);
    }
    private async void SaveVisualPalette_Click(object sender, RoutedEventArgs e) => await SaveVisualProjectAsync(false);
    private async void SaveVisualTone_Click(object sender, RoutedEventArgs e) => await SaveVisualProjectAsync(true);
    private async Task SaveVisualProjectAsync(bool tone)
    {
        if (_visualPayload is null) return;
        if (VisualProjectPicker.SelectedItem is not VisualProjectChoice project) { ShowVisualToast("请先选择项目。"); return; }
        try
        {
            await _viewModel.SaveProjectVisualAsync(project.Id, _visualPayload, tone, _visualSourceKind, _visualContainerId);
            ShowVisualToast(tone ? "已保存为项目默认影调目标" : "已保存为项目默认配色");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        { ShowVisualToast("项目视觉参考未保存，请检查存储位置后重试。"); }
    }
    private sealed record VisualProjectChoice(Guid Id, string Name);
}
