namespace RAWSelectionAssistant.ViewModels;

public partial class MainViewModel
{
    public Task OpenSelectionToolAsync(string tool, IReadOnlyList<string> paths)
    {
        if (IsBusy) throw new InvalidOperationException("请等待当前任务完成。");
        switch (tool)
        {
            case "RawToJpeg":
                if (RawToJpegPage is null) throw new InvalidOperationException("RAW 转换工具不可用。");
                RawToJpegPage.AddFiles(paths);
                break;
            case "BatchCompress":
                if (BatchCompressionPage is null) throw new InvalidOperationException("压缩工具不可用。");
                BatchCompressionPage.AddFiles(paths);
                break;
            case "Collage": CollagePage.AddPaths(paths); break;
            default: throw new ArgumentException("未知快速工具。", nameof(tool));
        }
        Navigate(tool);
        return Task.CompletedTask;
    }
}
