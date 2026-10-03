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
    public string QuickExportLabel => "复制原文件到…";
    public AssetLibraryWorkspaceSettings InspectorLayoutSettings => _workspaceSettings;

    private void InitializeQuickTools() => SelectionToolCommand = new(ExecuteSelectionToolAsync, CanExecuteSelectionTool);

    public IReadOnlyDictionary<string,string> QuickToolHints => new[] { "View", "Canvas", "Export", "RawToJpeg", "Collage", "BatchCompress", "Publishing" }
        .ToDictionary(action=>action, action=>CanExecuteSelectionTool(action) ? action switch { "Export"=>"复制磁盘原文件，不渲染调整", "Publishing"=>"使用发布配方选择格式、尺寸和输出", _=>"对当前选择执行" } :
            CaptureOrderedSelection().Any(item=>!File.Exists(GetDisplaySourcePath(item))) ? "所选源文件不可访问，请先重新定位" : action=="RawToJpeg" ? "需要选择可访问的 RAW 照片" : "此工具不适用于当前选择或当前工作区");

    public bool CanExecuteSelectionTool(string? action)
    {
        if (!IsReady || !HasSelection) return false;
        var selected = CaptureOrderedSelection();
        if (selected.Count == 0 || selected.Any(item => !File.Exists(GetDisplaySourcePath(item)))) return false;
        return action switch
        {
            "View" => selected.Count == 1,
            "Canvas" => OpenCanvasHandler is not null,
            "Export" => true,
            "RawToJpeg" => SelectionToolHandler is not null && selected.All(item =>
                new[] { ".arw", ".cr2", ".cr3", ".nef", ".nrw", ".raf", ".rw2", ".orf", ".pef", ".dng", ".srw" }.Contains(Path.GetExtension(GetDisplaySourcePath(item)), StringComparer.OrdinalIgnoreCase)),
            "Collage" => SelectionToolHandler is not null && selected.Count > 1,
            "BatchCompress" or "Publishing" => SelectionToolHandler is not null,
            _ => false
        };
    }

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
