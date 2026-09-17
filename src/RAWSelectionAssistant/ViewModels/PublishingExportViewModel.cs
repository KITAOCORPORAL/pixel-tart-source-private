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
    private string _fontFamily = "Microsoft YaHei UI"; private string _fontWeight = "SemiBold"; private double _fontSize = 34; private string _color = "#FFFFFFFF"; private double _letterSpacing; private double _marginPercent = 3; private double _horizontalOffset; private double _verticalOffset;
    public Guid Id { get; init; } = Guid.NewGuid(); public WatermarkLayerType Type { get; init; }
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
    public string FontFamily { get => _fontFamily; set => SetProperty(ref _fontFamily, value ?? ""); }
    public string FontWeight { get => _fontWeight; set => SetProperty(ref _fontWeight, value ?? ""); }
    public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value); }
    public string Color { get => _color; set => SetProperty(ref _color, value ?? ""); }
    public double LetterSpacing { get => _letterSpacing; set => SetProperty(ref _letterSpacing, value); }
    public double MarginPercent { get => _marginPercent; set => SetProperty(ref _marginPercent, value); }
    public double HorizontalOffset { get => _horizontalOffset; set => SetProperty(ref _horizontalOffset, value); }
    public double VerticalOffset { get => _verticalOffset; set => SetProperty(ref _verticalOffset, value); }
    public void RestoreColor() { Hue = 0; Saturation = 0; Lightness = 0; Invert = false; }
    public ICommand RestoreColorCommand => _restoreColorCommand ??= new RelayCommand(_ => RestoreColor());
    private ICommand? _restoreColorCommand;
    public bool IsText => Type == WatermarkLayerType.Text; public bool IsImage => Type == WatermarkLayerType.Image;
    public WatermarkLayer ToModel() => new(Id, Type, Enabled, string.IsNullOrWhiteSpace(ImagePath) ? null : ImagePath, Text, FontFamily, FontWeight, FontSize, Color, LetterSpacing, Opacity, Size, Position, MarginPercent, HorizontalOffset, VerticalOffset, new(Hue, Saturation, Lightness, Invert));
}

public sealed record PublishingChoice<T>(T Value, string Name);

public sealed class PublishingExportViewModel : ObservableObject
{
    private readonly IPublishingTaskCoordinator _coordinator; private readonly IPublishingRenderer _renderer; private readonly IDialogService _dialogs;
    private string _destinationDirectory = ""; private bool _dimensionsEnabled = true; private PublishingSizeMode _sizeMode = PublishingSizeMode.LongestEdge; private int _longestEdge = 2400; private int _exactWidth = 1920; private int _exactHeight = 1080; private int _jpegQuality = 88; private bool _preserveMetadata = true; private bool _watermarksEnabled = true; private string _suffix = PublishingDefaults.DefaultSuffix; private string _presetName = ""; private Guid? _activeProjectId; private int _previewIndex; private int _previewRevision; private BitmapImage? _previewImage; private bool _isPreviewing; private bool _isBusy; private string _statusText = "添加成片，设置发布版本；源照片不会被覆盖。"; private PublishingPreset? _selectedPreset;
    public PublishingExportViewModel(IPublishingTaskCoordinator coordinator, IPublishingRenderer renderer, IDialogService dialogs)
    {
        _coordinator=coordinator;_renderer=renderer;_dialogs=dialogs;
        AddFilesCommand=new RelayCommand(_=>AddFiles());AddFolderCommand=new RelayCommand(_=>AddFolder());ChooseDestinationCommand=new RelayCommand(_=>ChooseDestination());AddTextWatermarkCommand=new RelayCommand(_=>AddTextLayer());AddImageWatermarkCommand=new RelayCommand(_=>AddImageLayer());PreviousCommand=new AsyncRelayCommand(_=>MovePreviewAsync(-1));NextCommand=new AsyncRelayCommand(_=>MovePreviewAsync(1));RefreshPreviewCommand=new AsyncRelayCommand(_=>RefreshPreviewAsync());StartCommand=new AsyncRelayCommand(_=>StartAsync(),_=>CanStart);SavePresetCommand=new AsyncRelayCommand(_=>SavePresetAsync());
        WatermarkLayers.CollectionChanged += (_, change) => { if (change.NewItems is not null) foreach (PublishingWatermarkLayerViewModel layer in change.NewItems) Wire(layer); _=RefreshPreviewAsync(); };
        _=LoadPresetsAsync();
    }
    public ObservableCollection<string> SourceFiles { get; }=[]; public ObservableCollection<PublishingWatermarkLayerViewModel> WatermarkLayers { get; }=[]; public ObservableCollection<PublishingPreset> Presets { get; }=[];
    public IReadOnlyList<PublishingChoice<PublishingSizeMode>> SizeModes { get; }=[new(PublishingSizeMode.Original,"保持原尺寸"),new(PublishingSizeMode.LongestEdge,"限制长边"),new(PublishingSizeMode.Exact,"指定尺寸（等比适配）")];
    public IReadOnlyList<PublishingChoice<WatermarkPosition>> Positions { get; }=[new(WatermarkPosition.TopLeft,"左上"),new(WatermarkPosition.TopCenter,"上中"),new(WatermarkPosition.TopRight,"右上"),new(WatermarkPosition.MiddleLeft,"左中"),new(WatermarkPosition.Center,"居中"),new(WatermarkPosition.MiddleRight,"右中"),new(WatermarkPosition.BottomLeft,"左下"),new(WatermarkPosition.BottomCenter,"下中"),new(WatermarkPosition.BottomRight,"右下")];
    public ICommand AddFilesCommand{get;} public ICommand AddFolderCommand{get;} public ICommand ChooseDestinationCommand{get;} public ICommand AddTextWatermarkCommand{get;} public ICommand AddImageWatermarkCommand{get;} public ICommand PreviousCommand{get;} public ICommand NextCommand{get;} public ICommand RefreshPreviewCommand{get;} public ICommand StartCommand{get;} public ICommand SavePresetCommand{get;}
    public string DestinationDirectory { get=>_destinationDirectory; set{if(SetProperty(ref _destinationDirectory,value??""))RaiseCommands();} }
    public string PresetName { get => _presetName; set => SetProperty(ref _presetName, value ?? ""); }
    public int PreviewIndex { get=>_previewIndex; set { if(SourceFiles.Count==0)return; _previewIndex=Math.Clamp(value,0,SourceFiles.Count-1);OnPropertyChanged(nameof(PreviewCounter));_=RefreshPreviewAsync(); } }
    public bool DimensionsEnabled{get=>_dimensionsEnabled;set{SetProperty(ref _dimensionsEnabled,value);_=RefreshPreviewAsync();}} public PublishingSizeMode SizeMode{get=>_sizeMode;set{SetProperty(ref _sizeMode,value);_=RefreshPreviewAsync();}} public int LongestEdge{get=>_longestEdge;set{SetProperty(ref _longestEdge,Math.Clamp(value,320,30000));_=RefreshPreviewAsync();}} public int ExactWidth{get=>_exactWidth;set{SetProperty(ref _exactWidth,Math.Clamp(value,1,30000));_=RefreshPreviewAsync();}} public int ExactHeight{get=>_exactHeight;set{SetProperty(ref _exactHeight,Math.Clamp(value,1,30000));_=RefreshPreviewAsync();}} public int JpegQuality{get=>_jpegQuality;set{SetProperty(ref _jpegQuality,Math.Clamp(value,40,100));_=RefreshPreviewAsync();}} public bool PreserveMetadata{get=>_preserveMetadata;set=>SetProperty(ref _preserveMetadata,value);} public bool WatermarksEnabled{get=>_watermarksEnabled;set{SetProperty(ref _watermarksEnabled,value);_=RefreshPreviewAsync();}} public string Suffix{get=>_suffix;set=>SetProperty(ref _suffix,value??"");}
    public BitmapImage? PreviewImage{get=>_previewImage;private set=>SetProperty(ref _previewImage,value);} public bool IsPreviewing{get=>_isPreviewing;private set=>SetProperty(ref _isPreviewing,value);} public string PreviewCounter=>SourceFiles.Count==0?"0 / 0":$"{_previewIndex+1} / {SourceFiles.Count}"; public bool IsBusy{get=>_isBusy;private set{SetProperty(ref _isBusy,value);RaiseCommands();}} public string StatusText{get=>_statusText;private set=>SetProperty(ref _statusText,value);} public bool CanStart=>!IsBusy&&SourceFiles.Count>0&&Directory.Exists(DestinationDirectory);
    public PublishingPreset? SelectedPreset{get=>_selectedPreset;set{if(!SetProperty(ref _selectedPreset,value)||value is null)return;PresetName=value.Name;Apply(value.Options);}}
    public void AddFiles(IEnumerable<string> paths){var requested=paths.ToArray();var unsupported=requested.Where(path=>!PublishingDefaults.SupportedExtensions.Contains(Path.GetExtension(path))).ToArray();foreach(var path in requested.Where(File.Exists).Where(path=>PublishingDefaults.SupportedExtensions.Contains(Path.GetExtension(path))).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))if(!SourceFiles.Contains(path,StringComparer.OrdinalIgnoreCase))SourceFiles.Add(path);if(unsupported.Length>0)StatusText=$"已跳过 {unsupported.Length} 个暂不支持的文件；当前支持 JPG、JPEG、PNG。";_previewIndex=Math.Clamp(_previewIndex,0,Math.Max(0,SourceFiles.Count-1));OnPropertyChanged(nameof(PreviewCounter));RaiseCommands();_=RefreshPreviewAsync();}
    public void AddFolder(string directory){if(!Directory.Exists(directory)){StatusText="成片文件夹不可用。";return;}var candidates=Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly).Where(path=>PublishingDefaults.SupportedExtensions.Contains(Path.GetExtension(path))||Path.GetExtension(path).Equals(".tif",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(path).Equals(".tiff",StringComparison.OrdinalIgnoreCase));AddFiles(candidates);}
    private void AddFiles()=>AddFiles(_dialogs.ChooseFiles("选择发布成片","照片|*.jpg;*.jpeg;*.png|所有文件|*.*",true)); private void AddFolder(){var path=_dialogs.ChooseFolder("选择成片文件夹",null);if(path is not null)AddFolder(path);} private void ChooseDestination(){var path=_dialogs.ChooseFolder("选择发布版本输出目录",DestinationDirectory);if(path is not null)DestinationDirectory=path;}
    private void AddTextLayer(){WatermarkLayers.Add(new PublishingWatermarkLayerViewModel{Type=WatermarkLayerType.Text,Name="文字水印"});}
    private void AddImageLayer(){var path=_dialogs.ChooseFiles("选择图片水印","图片水印|*.png;*.jpg;*.jpeg|所有文件|*.*",false).FirstOrDefault();if(path is null)return;WatermarkLayers.Add(new PublishingWatermarkLayerViewModel{Type=WatermarkLayerType.Image,Name=Path.GetFileName(path),ImagePath=path});}
    private void Wire(PublishingWatermarkLayerViewModel layer)=>layer.PropertyChanged+=(_,_)=>_=RefreshPreviewAsync();
    private async Task MovePreviewAsync(int offset){if(SourceFiles.Count==0)return;_previewIndex=(_previewIndex+offset+SourceFiles.Count)%SourceFiles.Count;OnPropertyChanged(nameof(PreviewCounter));await RefreshPreviewAsync();}
    private PublishingOptions Options()=>new(new(DimensionsEnabled,SizeMode,LongestEdge,ExactWidth,ExactHeight,JpegQuality,PreserveMetadata),WatermarksEnabled,WatermarkLayers.Select(layer=>layer.ToModel()).ToArray(),PublishingOutputFormat.Jpeg,Suffix);
    private async Task RefreshPreviewAsync(){var requested=++_previewRevision;if(SourceFiles.Count==0||IsPreviewing)return;IsPreviewing=true;try{do{requested=_previewRevision;string? temporary=null;try{temporary=Path.Combine(Path.GetTempPath(),"PixelTartPublishingPreview",Guid.NewGuid().ToString("N")+".jpg");Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);await _renderer.RenderAsync(SourceFiles[_previewIndex],temporary,Options());if(requested==_previewRevision){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(temporary);image.EndInit();image.Freeze();PreviewImage=image;}}catch{if(requested==_previewRevision)StatusText="预览暂时不可用，请检查照片或水印文件。";}finally{if(temporary is not null)try{File.Delete(temporary);}catch{}}}while(requested!=_previewRevision&&SourceFiles.Count>0);}finally{IsPreviewing=false;}}
    private async Task StartAsync(){if(!CanStart)return;IsBusy=true;try{var id=await _coordinator.StartAsync(new(SourceFiles.ToArray(),DestinationDirectory,Options()));StatusText="发布任务已进入任务中心。";await _coordinator.WaitForCompletionAsync(id);var state=await _coordinator.GetTaskStateAsync(id);StatusText=state?.State==TaskLifecycleState.Completed?$"已生成 {SourceFiles.Count:N0} 个发布版本。":"发布任务未全部完成，请在任务中心查看原因。";}catch{StatusText="发布任务提交失败，请检查输出目录。";}finally{IsBusy=false;}}
    private PublishingPresetStore PresetStore()=>new(Path.Combine(RAWSelectionAssistant.Core.Utilities.AppDataPaths.Root,"Publishing","presets.json"));
    private ProjectPublishingDefaultStore ProjectPresetStore()=>new(Path.Combine(RAWSelectionAssistant.Core.Utilities.AppDataPaths.Root,"Publishing","project-defaults.json"));
    public async Task ActivateProjectAsync(Guid projectId)
    {
        if (projectId == Guid.Empty) return;
        _activeProjectId = projectId;
        var presetId = await ProjectPresetStore().GetAsync(projectId);
        var preset = Presets.FirstOrDefault(item => item.Id == presetId);
        if (preset is not null) SelectedPreset = preset;
    }
    private async Task LoadPresetsAsync(){foreach(var preset in await PresetStore().LoadAsync())Presets.Add(preset);SelectedPreset=Presets.FirstOrDefault();}
    private async Task SavePresetAsync(){var name=string.IsNullOrWhiteSpace(PresetName)?$"我的发布预设 {Presets.Count+1}":PresetName.Trim();var preset=new PublishingPreset(Guid.NewGuid(),name,Options(),DateTimeOffset.UtcNow);await PresetStore().SaveAsync(preset);if(_activeProjectId is Guid projectId)await ProjectPresetStore().SetAsync(new(projectId,preset.Id));Presets.Add(preset);SelectedPreset=preset;StatusText=_activeProjectId is null?"发布预设已保存。":"发布预设已保存，并设为当前项目默认。";}
    private void Apply(PublishingOptions options){DimensionsEnabled=options.EffectiveDimensions.Enabled;SizeMode=options.EffectiveDimensions.Mode;LongestEdge=options.EffectiveDimensions.LongestEdge;ExactWidth=options.EffectiveDimensions.Width;ExactHeight=options.EffectiveDimensions.Height;JpegQuality=options.EffectiveDimensions.JpegQuality;PreserveMetadata=options.EffectiveDimensions.PreserveMetadata;WatermarksEnabled=options.WatermarksEnabled;Suffix=options.Suffix;WatermarkLayers.Clear();foreach(var item in options.EffectiveWatermarkLayers){var layer=new PublishingWatermarkLayerViewModel{Id=item.Id,Type=item.Type,Name=item.Type==WatermarkLayerType.Text?"文字水印":Path.GetFileName(item.ImagePath)??"图片水印",Text=item.Text,ImagePath=item.ImagePath??"",FontFamily=item.FontFamily,FontWeight=item.FontWeight,FontSize=item.FontSize,Color=item.Color,LetterSpacing=item.LetterSpacing,Opacity=item.Opacity,Size=item.WidthPercent,Hue=item.EffectiveColorAdjustments.Hue,Saturation=item.EffectiveColorAdjustments.Saturation,Lightness=item.EffectiveColorAdjustments.Lightness,Invert=item.EffectiveColorAdjustments.Invert,Position=item.Position,MarginPercent=item.MarginPercent,HorizontalOffset=item.HorizontalOffset,VerticalOffset=item.VerticalOffset};WatermarkLayers.Add(layer);}_=RefreshPreviewAsync();}
    private void RaiseCommands()=>(StartCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
}
