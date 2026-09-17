using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Publishing;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class PublishingWatermarkLayerViewModel : ObservableObject
{
    private bool _enabled = true; private string _name = "文字水印"; private string _text = "Pixel Tart"; private string _imagePath = ""; private double _opacity = .72; private double _size = 12; private double _hue; private double _saturation; private double _lightness; private bool _invert; private WatermarkPosition _position = WatermarkPosition.BottomRight;
    public Guid Id { get; } = Guid.NewGuid(); public WatermarkLayerType Type { get; init; }
    public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Text { get => _text; set => SetProperty(ref _text, value ?? ""); }
    public string ImagePath { get => _imagePath; set => SetProperty(ref _imagePath, value ?? ""); }
    public double Opacity { get => _opacity; set => SetProperty(ref _opacity, value); }
    public double Size { get => _size; set => SetProperty(ref _size, value); }
    public double Hue { get => _hue; set => SetProperty(ref _hue, value); }
    public double Saturation { get => _saturation; set => SetProperty(ref _saturation, value); }
    public double Lightness { get => _lightness; set => SetProperty(ref _lightness, value); }
    public bool Invert { get => _invert; set => SetProperty(ref _invert, value); }
    public WatermarkPosition Position { get => _position; set => SetProperty(ref _position, value); }
    public bool IsText => Type == WatermarkLayerType.Text; public bool IsImage => Type == WatermarkLayerType.Image;
    public WatermarkLayer ToModel() => new(Id, Type, Enabled, string.IsNullOrWhiteSpace(ImagePath) ? null : ImagePath, Text, FontSize: 34, Opacity: Opacity, WidthPercent: Size, Position: Position, ColorAdjustments: new(Hue, Saturation, Lightness, Invert));
}

public sealed class PublishingExportViewModel : ObservableObject
{
    private readonly IPublishingTaskCoordinator _coordinator; private readonly IPublishingRenderer _renderer; private readonly IDialogService _dialogs;
    private string _destinationDirectory = ""; private bool _dimensionsEnabled = true; private PublishingSizeMode _sizeMode = PublishingSizeMode.LongestEdge; private int _longestEdge = 2400; private int _jpegQuality = 88; private bool _preserveMetadata = true; private bool _watermarksEnabled = true; private string _suffix = PublishingDefaults.DefaultSuffix; private int _previewIndex; private BitmapImage? _previewImage; private bool _isPreviewing; private bool _isBusy; private string _statusText = "添加成片，设置发布版本；源照片不会被覆盖。"; private PublishingPreset? _selectedPreset;
    public PublishingExportViewModel(IPublishingTaskCoordinator coordinator, IPublishingRenderer renderer, IDialogService dialogs)
    {
        _coordinator=coordinator;_renderer=renderer;_dialogs=dialogs;
        AddFilesCommand=new RelayCommand(_=>AddFiles());AddFolderCommand=new RelayCommand(_=>AddFolder());ChooseDestinationCommand=new RelayCommand(_=>ChooseDestination());AddTextWatermarkCommand=new RelayCommand(_=>AddTextLayer());AddImageWatermarkCommand=new RelayCommand(_=>AddImageLayer());PreviousCommand=new AsyncRelayCommand(_=>MovePreviewAsync(-1));NextCommand=new AsyncRelayCommand(_=>MovePreviewAsync(1));RefreshPreviewCommand=new AsyncRelayCommand(_=>RefreshPreviewAsync());StartCommand=new AsyncRelayCommand(_=>StartAsync(),_=>CanStart);SavePresetCommand=new AsyncRelayCommand(_=>SavePresetAsync());
        _=LoadPresetsAsync();
    }
    public ObservableCollection<string> SourceFiles { get; }=[]; public ObservableCollection<PublishingWatermarkLayerViewModel> WatermarkLayers { get; }=[]; public ObservableCollection<PublishingPreset> Presets { get; }=[];
    public IReadOnlyList<PublishingSizeMode> SizeModes { get; }=Enum.GetValues<PublishingSizeMode>(); public IReadOnlyList<WatermarkPosition> Positions { get; }=Enum.GetValues<WatermarkPosition>();
    public ICommand AddFilesCommand{get;} public ICommand AddFolderCommand{get;} public ICommand ChooseDestinationCommand{get;} public ICommand AddTextWatermarkCommand{get;} public ICommand AddImageWatermarkCommand{get;} public ICommand PreviousCommand{get;} public ICommand NextCommand{get;} public ICommand RefreshPreviewCommand{get;} public ICommand StartCommand{get;} public ICommand SavePresetCommand{get;}
    public string DestinationDirectory { get=>_destinationDirectory; set{if(SetProperty(ref _destinationDirectory,value??""))RaiseCommands();} }
    public bool DimensionsEnabled{get=>_dimensionsEnabled;set{SetProperty(ref _dimensionsEnabled,value);_=RefreshPreviewAsync();}} public PublishingSizeMode SizeMode{get=>_sizeMode;set{SetProperty(ref _sizeMode,value);_=RefreshPreviewAsync();}} public int LongestEdge{get=>_longestEdge;set{SetProperty(ref _longestEdge,Math.Clamp(value,320,30000));_=RefreshPreviewAsync();}} public int JpegQuality{get=>_jpegQuality;set=>SetProperty(ref _jpegQuality,Math.Clamp(value,40,100));} public bool PreserveMetadata{get=>_preserveMetadata;set=>SetProperty(ref _preserveMetadata,value);} public bool WatermarksEnabled{get=>_watermarksEnabled;set{SetProperty(ref _watermarksEnabled,value);_=RefreshPreviewAsync();}} public string Suffix{get=>_suffix;set=>SetProperty(ref _suffix,value??"");}
    public BitmapImage? PreviewImage{get=>_previewImage;private set=>SetProperty(ref _previewImage,value);} public bool IsPreviewing{get=>_isPreviewing;private set=>SetProperty(ref _isPreviewing,value);} public string PreviewCounter=>SourceFiles.Count==0?"0 / 0":$"{_previewIndex+1} / {SourceFiles.Count}"; public bool IsBusy{get=>_isBusy;private set{SetProperty(ref _isBusy,value);RaiseCommands();}} public string StatusText{get=>_statusText;private set=>SetProperty(ref _statusText,value);} public bool CanStart=>!IsBusy&&SourceFiles.Count>0&&Directory.Exists(DestinationDirectory);
    public PublishingPreset? SelectedPreset{get=>_selectedPreset;set{if(!SetProperty(ref _selectedPreset,value)||value is null)return;Apply(value.Options);}}
    public void AddFiles(IEnumerable<string> paths){foreach(var path in paths.Where(File.Exists).Where(path=>PublishingDefaults.SupportedExtensions.Contains(Path.GetExtension(path))).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))if(!SourceFiles.Contains(path,StringComparer.OrdinalIgnoreCase))SourceFiles.Add(path);_previewIndex=Math.Clamp(_previewIndex,0,Math.Max(0,SourceFiles.Count-1));OnPropertyChanged(nameof(PreviewCounter));RaiseCommands();_=RefreshPreviewAsync();}
    public void AddFolder(string directory){AddFiles(PublishingFolderInput.Scan(directory));}
    private void AddFiles()=>AddFiles(_dialogs.ChooseFiles("选择发布成片","照片|*.jpg;*.jpeg;*.png|所有文件|*.*",true)); private void AddFolder(){var path=_dialogs.ChooseFolder("选择成片文件夹",null);if(path is not null)AddFolder(path);} private void ChooseDestination(){var path=_dialogs.ChooseFolder("选择发布版本输出目录",DestinationDirectory);if(path is not null)DestinationDirectory=path;}
    private void AddTextLayer(){var layer=new PublishingWatermarkLayerViewModel{Type=WatermarkLayerType.Text,Name="文字水印"};Wire(layer);WatermarkLayers.Add(layer);_=RefreshPreviewAsync();}
    private void AddImageLayer(){var path=_dialogs.ChooseFiles("选择图片水印","图片水印|*.png;*.jpg;*.jpeg|所有文件|*.*",false).FirstOrDefault();if(path is null)return;var layer=new PublishingWatermarkLayerViewModel{Type=WatermarkLayerType.Image,Name=Path.GetFileName(path),ImagePath=path};Wire(layer);WatermarkLayers.Add(layer);_=RefreshPreviewAsync();}
    private void Wire(PublishingWatermarkLayerViewModel layer)=>layer.PropertyChanged+=(_,_)=>_=RefreshPreviewAsync();
    private async Task MovePreviewAsync(int offset){if(SourceFiles.Count==0)return;_previewIndex=(_previewIndex+offset+SourceFiles.Count)%SourceFiles.Count;OnPropertyChanged(nameof(PreviewCounter));await RefreshPreviewAsync();}
    private PublishingOptions Options()=>new(new(DimensionsEnabled,SizeMode,LongestEdge,JpegQuality:JpegQuality,PreserveMetadata:PreserveMetadata),WatermarksEnabled,WatermarkLayers.Select(layer=>layer.ToModel()).ToArray(),PublishingOutputFormat.Jpeg,Suffix);
    private async Task RefreshPreviewAsync(){if(SourceFiles.Count==0||IsPreviewing)return;IsPreviewing=true;string? temporary=null;try{temporary=Path.Combine(Path.GetTempPath(),"PixelTartPublishingPreview",Guid.NewGuid().ToString("N")+".jpg");Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);await _renderer.RenderAsync(SourceFiles[_previewIndex],temporary,Options());var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(temporary);image.EndInit();image.Freeze();PreviewImage=image;}catch{StatusText="预览暂时不可用，请检查照片或水印文件。";}finally{if(temporary is not null)try{File.Delete(temporary);}catch{}IsPreviewing=false;}}
    private async Task StartAsync(){if(!CanStart)return;IsBusy=true;try{var id=await _coordinator.StartAsync(new(SourceFiles.ToArray(),DestinationDirectory,Options()));StatusText="发布任务已进入任务中心。";await _coordinator.WaitForCompletionAsync(id);var state=await _coordinator.GetTaskStateAsync(id);StatusText=state?.State==TaskLifecycleState.Completed?$"已生成 {SourceFiles.Count:N0} 个发布版本。":"发布任务未全部完成，请在任务中心查看原因。";}catch{StatusText="发布任务提交失败，请检查输出目录。";}finally{IsBusy=false;}}
    private PublishingPresetStore PresetStore()=>new(Path.Combine(RAWSelectionAssistant.Core.Utilities.AppDataPaths.Root,"Publishing","presets.json"));
    private async Task LoadPresetsAsync(){foreach(var preset in await PresetStore().LoadAsync())Presets.Add(preset);SelectedPreset=Presets.FirstOrDefault();}
    private async Task SavePresetAsync(){var name=$"我的发布预设 {Presets.Count+1}";var preset=new PublishingPreset(Guid.NewGuid(),name,Options(),DateTimeOffset.UtcNow);await PresetStore().SaveAsync(preset);Presets.Add(preset);SelectedPreset=preset;StatusText="发布预设已保存。";}
    private void Apply(PublishingOptions options){DimensionsEnabled=options.EffectiveDimensions.Enabled;SizeMode=options.EffectiveDimensions.Mode;LongestEdge=options.EffectiveDimensions.LongestEdge;JpegQuality=options.EffectiveDimensions.JpegQuality;PreserveMetadata=options.EffectiveDimensions.PreserveMetadata;WatermarksEnabled=options.WatermarksEnabled;Suffix=options.Suffix;WatermarkLayers.Clear();foreach(var item in options.EffectiveWatermarkLayers){var layer=new PublishingWatermarkLayerViewModel{Type=item.Type,Name=item.Type==WatermarkLayerType.Text?"文字水印":Path.GetFileName(item.ImagePath)??"图片水印",Text=item.Text,ImagePath=item.ImagePath??"",Opacity=item.Opacity,Size=item.WidthPercent,Hue=item.EffectiveColorAdjustments.Hue,Saturation=item.EffectiveColorAdjustments.Saturation,Lightness=item.EffectiveColorAdjustments.Lightness,Invert=item.EffectiveColorAdjustments.Invert,Position=item.Position};Wire(layer);WatermarkLayers.Add(layer);}_=RefreshPreviewAsync();}
    private void RaiseCommands()=>(StartCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
}
