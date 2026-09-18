using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class ReferenceColorWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IDialogService _dialogs;
    private BitmapSource? _targetImage;
    private string _targetName = "尚未选择目标图片";
    private string _statusText = "选择目标图片，再选择或导入参考来源。源照片始终只读。";

    public ReferenceColorWorkspaceViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        Editor = new TetherReferenceModeViewModel(
            new ReferenceLookStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals")), dialogs, allowReferenceManagement: true);
        Editor.Enabled = true;
        ChooseTargetCommand = new AsyncRelayCommand(_ => ChooseTargetAsync());
    }

    public TetherReferenceModeViewModel Editor { get; }
    public AsyncRelayCommand ChooseTargetCommand { get; }
    public BitmapSource? TargetImage { get => _targetImage; private set => SetProperty(ref _targetImage, value); }
    public string TargetName { get => _targetName; private set => SetProperty(ref _targetName, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public async Task InitializeAsync(CancellationToken token = default) => await Editor.LoadAsync(token);

    public async Task AcceptContextAsync(Guid? projectId, Guid? assetId, BitmapSource? source, CancellationToken token = default)
    {
        await Editor.SetProjectAsync(projectId, token);
        if (source is null) return;
        TargetImage = source;
        TargetName = "当前联机照片";
        await Editor.SetSourceAsync(assetId, source, token);
        StatusText = "已从联机拍摄带入当前照片和色彩方案；取消或返回不会改写方案。";
    }

    private async Task ChooseTargetAsync()
    {
        var path = _dialogs.ChooseFiles("选择目标图片（源文件只读）", "图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp", false).FirstOrDefault();
        if (path is null) return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze();
            TargetImage = image; TargetName = Path.GetFileName(path);
            await Editor.SetSourceAsync(null, image);
            StatusText = "目标图片已只读加载；可在右侧制作色彩方案或导出 3D LUT。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        { StatusText = "目标图片无法读取；现有色彩方案保持不变。"; }
    }

    public void Dispose() => Editor.Dispose();
}
