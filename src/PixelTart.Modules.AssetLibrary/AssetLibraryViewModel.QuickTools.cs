using System.IO;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    public Func<string, IReadOnlyList<string>, Task>? SelectionToolHandler { get; set; }
    public Func<IReadOnlyList<AssetItem>, Task>? OpenCanvasHandler { get; set; }
    public AsyncCommand<string> SelectionToolCommand { get; private set; } = null!;
    public string QuickCompressLabel => HasMultipleSelection ? "批量压缩" : "压缩";
    public string QuickExportLabel => HasMultipleSelection ? "批量导出" : "导出";

    private void InitializeQuickTools() => SelectionToolCommand = new(ExecuteSelectionToolAsync, _ => IsReady && HasSelection);

    public IReadOnlyList<AssetItem> CaptureOrderedSelection() => AssetCards.Where(card => SelectedAssetIds.Contains(card.Asset.AssetId)).Select(card => card.Asset).ToArray();

    private async Task ExecuteSelectionToolAsync(string? action)
    {
        var selected = CaptureOrderedSelection();
        if (selected.Count == 0) return;
        try
        {
            if (action == "Canvas")
            {
                if (OpenCanvasHandler is not null) await OpenCanvasHandler(selected);
                return;
            }
            if (action == "View")
            {
                await OpenContextViewerAsync(new(selected[0]) { Owner = this });
                return;
            }
            if (action == "Export")
            {
                var dialog = new OpenFolderDialog { Title = "导出所选照片副本" };
                if (dialog.ShowDialog() != true) return;
                var result = await new AssetSelectionExportService().ExportFilesAsync(selected, dialog.FolderName, false, _lifetimeCancellation.Token);
                Status = $"已导出 {result.ExportedCount} 张；源照片未改变。";
                return;
            }
            var paths = selected.Select(GetDisplaySourcePath).Where(File.Exists).ToArray();
            if (paths.Length == 0) { Status = "所选源照片暂不可访问。"; return; }
            if (action is "RawToJpeg" or "BatchCompress" or "Publishing" or "Collage" && SelectionToolHandler is not null)
                await SelectionToolHandler(action, paths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        { Status = $"无法打开快速工具：{exception.Message}"; }
    }
}
