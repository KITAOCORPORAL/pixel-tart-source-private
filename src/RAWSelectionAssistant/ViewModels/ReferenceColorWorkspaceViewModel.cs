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
    private string _targetName = "尚未选择待调色照片";
    private string _statusText = "选择待调色照片，再添加希望借用色彩与影调的参考图片。源照片始终只读。";
    private bool _isLoading;

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
    public BitmapSource? TargetImage { get => _targetImage; private set { if (SetProperty(ref _targetImage, value)) OnPropertyChanged(nameof(HasTarget)); } }
    public bool HasTarget => TargetImage is not null;
    public string TargetName { get => _targetName; private set => SetProperty(ref _targetName, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

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
        var path = _dialogs.ChooseFiles("选择待调色照片（源文件只读）", "图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp", false).FirstOrDefault();
        if (path is null) return;
        await LoadTargetAsync(path);
    }

    public async Task LoadTargetAsync(string path)
    {
        try
        {
            IsLoading = true; StatusText = "正在载入照片…";
            var image = await Task.Run(() =>
            {
                var decoded = new BitmapImage();
                decoded.BeginInit(); decoded.CacheOption = BitmapCacheOption.OnLoad; decoded.UriSource = new Uri(path); decoded.EndInit(); decoded.Freeze();
                return decoded;
            });
            TargetImage = image; TargetName = Path.GetFileName(path);
            await Editor.SetSourceAsync(null, image);
            StatusText = "待调色照片已载入。左侧原片与仿色结果对比，参考图片显示在独立区域。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.IO.FileFormatException)
        { StatusText = "待调色照片无法读取；现有色彩方案保持不变。"; }
        finally { IsLoading = false; }
    }

    public void Dispose() => Editor.Dispose();
}
