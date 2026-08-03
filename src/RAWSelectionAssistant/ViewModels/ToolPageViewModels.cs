using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed record OptionItem<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

public sealed class OrganizePhotosViewModel : ObservableObject
{
    private readonly OrganizeService _service;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _cancellation;
    private OptionItem<OrganizeRuleType> _selectedRule;
    private OptionItem<OrganizeOperationType> _selectedOperation;
    private OptionItem<OrganizeConflictPolicy> _selectedConflict;
    private OrganizePhotoItem? _selectedPhoto;
    private PhotoGroupDefinition? _selectedGroup;
    private string _outputPath = string.Empty;
    private string _customParameter = string.Empty;
    private int _fixedCount = 100;
    private bool _verifySha256;
    private bool _isBusy;
    private double _progress;
    private string _statusMessage = "请选择照片或文件夹";
    private OrganizePlan? _currentPlan;
    private OrganizeExecutionResult? _lastResult;

    public OrganizePhotosViewModel(OrganizeService service, IDialogService dialogs)
    {
        _service = service;
        _dialogs = dialogs;
        _selectedRule = RuleOptions[0];
        _selectedOperation = OperationOptions[1];
        _selectedConflict = ConflictOptions[0];
        AddPhotosCommand = new AsyncRelayCommand(_ => AddPhotosAsync(), _ => !IsBusy);
        AddFolderCommand = new AsyncRelayCommand(_ => AddFolderAsync(), _ => !IsBusy);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput(), _ => !IsBusy);
        GroupCommand = new RelayCommand(_ => Regroup(), _ => !IsBusy && Photos.Count > 0);
        PreviewPlanCommand = new RelayCommand(_ => PreviewPlan(), _ => !IsBusy && Groups.Count > 0);
        ExecuteCommand = new AsyncRelayCommand(_ => ExecuteAsync(), _ => !IsBusy && CurrentPlan is not null);
        CancelCommand = new RelayCommand(_ => _cancellation?.Cancel(), _ => IsBusy);
        ExcludeSelectedCommand = new RelayCommand(_ => ExcludeSelected(), _ => !IsBusy && SelectedPhoto is not null);
        NewGroupCommand = new RelayCommand(_ => NewGroup(), _ => !IsBusy);
        DeleteEmptyGroupsCommand = new RelayCommand(_ => DeleteEmptyGroups(), _ => !IsBusy);
        MergeGroupCommand = new RelayCommand(_ => MergeSelectedGroup(), _ => !IsBusy && SelectedGroup is not null);
        SplitGroupCommand = new RelayCommand(_ => SplitSelectedGroup(), _ => !IsBusy && SelectedGroup?.Count > 1);
        DeleteGroupCommand = new RelayCommand(_ => DeleteSelectedGroup(), _ => !IsBusy && SelectedGroup is not null);
        SetCoverCommand = new RelayCommand(_ => SetCover(), _ => !IsBusy && SelectedGroup is not null && SelectedPhoto is not null);
        SortGroupsCommand = new RelayCommand(_ => SortGroups(), _ => !IsBusy && Groups.Count > 1);
        ExportReportsCommand = new AsyncRelayCommand(_ => ExportReportsAsync(), _ => !IsBusy && LastResult is not null);
        UndoMoveCommand = new AsyncRelayCommand(_ => UndoMoveAsync(), _ => !IsBusy && LastResult?.Manifest.OperationType == OrganizeOperationType.Move);
    }

    public string PageTitle => ToolRegistry.Get(ToolId.PhotoOrganize).DisplayName;
    public ObservableCollection<string> SourceInputs { get; } = [];
    public ObservableCollection<OrganizePhotoItem> Photos { get; } = [];
    public ObservableCollection<PhotoGroupDefinition> Groups { get; } = [];
    public IReadOnlyList<OptionItem<OrganizeRuleType>> RuleOptions { get; } =
    [
        new(OrganizeRuleType.OriginalFolder,"原文件夹"), new(OrganizeRuleType.CaptureDate,"拍摄日期"), new(OrganizeRuleType.CaptureYear,"拍摄年份"),
        new(OrganizeRuleType.CaptureYearMonth,"拍摄年月"), new(OrganizeRuleType.CaptureDateHour,"日期和小时"), new(OrganizeRuleType.CameraMake,"相机品牌"),
        new(OrganizeRuleType.CameraModel,"相机型号"), new(OrganizeRuleType.LensModel,"镜头型号"), new(OrganizeRuleType.FileFormat,"文件格式"),
        new(OrganizeRuleType.Landscape,"横图"), new(OrganizeRuleType.Portrait,"竖图"), new(OrganizeRuleType.Square,"方图"),
        new(OrganizeRuleType.FileNamePrefix,"文件名前缀"), new(OrganizeRuleType.FileNameNumber,"文件名数字段"), new(OrganizeRuleType.FileSizeRange,"文件大小区间"),
        new(OrganizeRuleType.FixedCount,"每 N 张一组"), new(OrganizeRuleType.CustomKeyword,"自定义关键词"), new(OrganizeRuleType.Manual,"手动分组")
    ];
    public IReadOnlyList<OptionItem<OrganizeOperationType>> OperationOptions { get; } = [new(OrganizeOperationType.SavePlan,"仅保存方案"), new(OrganizeOperationType.Copy,"复制到新目录（默认）"), new(OrganizeOperationType.Move,"移动到新目录（高风险）")];
    public IReadOnlyList<OptionItem<OrganizeConflictPolicy>> ConflictOptions { get; } = [new(OrganizeConflictPolicy.AutoNumber,"自动编号（默认）"), new(OrganizeConflictPolicy.Skip,"跳过"), new(OrganizeConflictPolicy.AddSourceFolder,"添加原文件夹名"), new(OrganizeConflictPolicy.AddCaptureDate,"添加拍摄日期"), new(OrganizeConflictPolicy.AddShortHash,"添加短哈希"), new(OrganizeConflictPolicy.Overwrite,"覆盖（需额外确认）")];

    public OptionItem<OrganizeRuleType> SelectedRule { get => _selectedRule; set { if(SetProperty(ref _selectedRule,value)) Regroup(); } }
    public OptionItem<OrganizeOperationType> SelectedOperation { get => _selectedOperation; set { if(SetProperty(ref _selectedOperation,value)) InvalidatePlan(); } }
    public OptionItem<OrganizeConflictPolicy> SelectedConflict { get => _selectedConflict; set { if(SetProperty(ref _selectedConflict,value)) InvalidatePlan(); } }
    public OrganizePhotoItem? SelectedPhoto { get=>_selectedPhoto; set { if(SetProperty(ref _selectedPhoto,value)) RefreshCommandStates(); } }
    public PhotoGroupDefinition? SelectedGroup { get=>_selectedGroup; set { if(SetProperty(ref _selectedGroup,value)) RefreshCommandStates(); } }
    public string OutputPath { get=>_outputPath; set { if(SetProperty(ref _outputPath,value)) InvalidatePlan(); } }
    public string CustomParameter { get=>_customParameter; set { if(SetProperty(ref _customParameter,value)) Regroup(); } }
    public int FixedCount { get=>_fixedCount; set { if(SetProperty(ref _fixedCount,Math.Max(1,value))) Regroup(); } }
    public bool VerifySha256 { get=>_verifySha256; set { if(SetProperty(ref _verifySha256,value)) InvalidatePlan(); } }
    public bool IsBusy { get=>_isBusy; private set { if(SetProperty(ref _isBusy,value)) RefreshCommandStates(); } }
    public double Progress { get=>_progress; private set=>SetProperty(ref _progress,value); }
    public string StatusMessage { get=>_statusMessage; private set=>SetProperty(ref _statusMessage,value); }
    public OrganizePlan? CurrentPlan { get=>_currentPlan; private set { if(SetProperty(ref _currentPlan,value)) { OnPropertyChanged(nameof(PlanSummary)); OnPropertyChanged(nameof(RiskSummary)); RefreshCommandStates(); } } }
    public OrganizeExecutionResult? LastResult { get=>_lastResult; private set { if(SetProperty(ref _lastResult,value)) RefreshCommandStates(); } }
    public string PlanSummary => CurrentPlan is null ? "尚未生成操作清单" : $"来源 {Photos.Count} · 有效 {CurrentPlan.Items.Count} · 分组 {CurrentPlan.Groups.Count} · 元数据缺失 {CurrentPlan.MetadataMissingCount} · 预计 {FormatBytes(CurrentPlan.EstimatedOutputBytes)}";
    public string RiskSummary => CurrentPlan is null ? "默认复制、不覆盖、不删除源文件" : $"操作：{SelectedOperation.Label}；冲突：{SelectedConflict.Label}；重名风险 {CurrentPlan.ConflictRiskCount}";

    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand GroupCommand { get; }
    public RelayCommand PreviewPlanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ExcludeSelectedCommand { get; }
    public RelayCommand NewGroupCommand { get; }
    public RelayCommand DeleteEmptyGroupsCommand { get; }
    public RelayCommand MergeGroupCommand { get; }
    public RelayCommand SplitGroupCommand { get; }
    public RelayCommand DeleteGroupCommand { get; }
    public RelayCommand SetCoverCommand { get; }
    public RelayCommand SortGroupsCommand { get; }
    public AsyncRelayCommand AddPhotosCommand { get; }
    public AsyncRelayCommand AddFolderCommand { get; }
    public AsyncRelayCommand ExecuteCommand { get; }
    public AsyncRelayCommand ExportReportsCommand { get; }
    public AsyncRelayCommand UndoMoveCommand { get; }

    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var values=paths.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(values.Length==0)return;
        foreach(var value in values) if(!SourceInputs.Contains(value,StringComparer.OrdinalIgnoreCase)) SourceInputs.Add(value);
        IsBusy=true; StatusMessage="正在读取照片与元数据…"; _cancellation=new();
        try
        {
            var progress=new Progress<OrganizeExecutionProgress>(x=>Progress=x.Total==0?0:x.Completed*100d/x.Total);
            var scanned=await _service.ScanAsync(values,_cancellation.Token,progress);
            foreach(var photo in scanned) if(Photos.All(x=>!string.Equals(x.SourcePath,photo.SourcePath,StringComparison.OrdinalIgnoreCase))) Photos.Add(photo);
            Regroup(); StatusMessage=$"已读取 {Photos.Count} 个文件";
        }
        catch(OperationCanceledException){StatusMessage="已取消读取";}
        finally{IsBusy=false;_cancellation.Dispose();_cancellation=null;Progress=0;}
    }

    private Task AddPhotosAsync()=>AddPathsAsync(_dialogs.ChooseFiles("选择要整理的照片","照片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.webp;*.heic;*.arw;*.cr2;*.cr3;*.nef;*.raf;*.dng;*.rw2;*.orf;*.pef|所有文件|*.*"));
    private Task AddFolderAsync(){var folder=_dialogs.ChooseFolder("选择要整理的照片文件夹");return folder is null?Task.CompletedTask:AddPathsAsync([folder]);}
    private void BrowseOutput(){var folder=_dialogs.ChooseFolder("选择整理输出目录",OutputPath);if(folder is not null)OutputPath=folder;}
    private void Regroup(){if(Photos.Count==0){RefreshCommandStates();return;}Groups.Clear();foreach(var group in _service.Group(Photos,new OrganizeRule(SelectedRule.Value,CustomParameter,FixedCount)))Groups.Add(group);InvalidatePlan();StatusMessage=$"已生成 {Groups.Count} 个分组";RefreshCommandStates();}
    private void PreviewPlan(){try{CurrentPlan=_service.BuildPlan(Photos,Groups,SourceInputs,OutputPath,new OrganizeRule(SelectedRule.Value,CustomParameter,FixedCount),SelectedOperation.Value,SelectedConflict.Value,VerifySha256);StatusMessage="操作清单已生成，请核对摘要后执行";}catch(Exception ex){_dialogs.ShowError(ex.Message);}}
    private async Task ExecuteAsync(){if(CurrentPlan is null)return;var sourceSummary=string.Join("；",CurrentPlan.SourceRoots.Take(3));var message=$"{PlanSummary}\n{RiskSummary}\n来源：{sourceSummary}\n输出：{(string.IsNullOrWhiteSpace(CurrentPlan.OutputRoot)?"仅保存方案，不写照片":CurrentPlan.OutputRoot)}\n\n用户确认的是当前具体清单。是否继续？";if(!_dialogs.Confirm(message,"确认整理操作"))return;if(CurrentPlan.OperationType==OrganizeOperationType.SavePlan){var planPath=_dialogs.ChooseSaveFile("保存整理方案","像素蛋挞整理方案|*.json",".json",$"整理方案_{DateTime.Now:yyyyMMdd_HHmm}.json");if(planPath is null)return;StatusMessage=$"整理方案已保存：{await _service.SavePlanAsync(CurrentPlan,planPath)}";return;}if(CurrentPlan.OperationType==OrganizeOperationType.Move&&!_dialogs.Confirm($"将移动 {CurrentPlan.Items.Count} 个文件。目标校验成功后才删除源文件，是否继续？","移动文件二次确认"))return;if(CurrentPlan.ConflictPolicy==OrganizeConflictPolicy.Overwrite&&!_dialogs.Confirm("覆盖会替换已有目标文件。是否明确允许覆盖？","覆盖额外确认"))return;IsBusy=true;_cancellation=new();StatusMessage="正在执行整理清单…";try{var progress=new Progress<OrganizeExecutionProgress>(x=>{Progress=x.Total==0?0:x.Completed*100d/x.Total;StatusMessage=string.IsNullOrWhiteSpace(x.CurrentFile)?"整理完成":$"正在处理 {Path.GetFileName(x.CurrentFile)}";});LastResult=await _service.ExecuteAsync(CurrentPlan,CurrentPlan.OperationType==OrganizeOperationType.Move,CurrentPlan.ConflictPolicy==OrganizeConflictPolicy.Overwrite,_cancellation.Token,progress);await _service.ExportReportsAsync(LastResult.Manifest,CurrentPlan.OutputRoot);StatusMessage=$"完成 {LastResult.Succeeded}，失败 {LastResult.Failed}，跳过 {LastResult.Skipped}";}finally{IsBusy=false;_cancellation.Dispose();_cancellation=null;Progress=0;}}
    private async Task ExportReportsAsync(){if(LastResult is null)return;var folder=_dialogs.ChooseFolder("选择报告输出目录",OutputPath);if(folder is null)return;await _service.ExportReportsAsync(LastResult.Manifest,folder);StatusMessage="CSV、JSON 和 TXT 报告已导出";}
    private async Task UndoMoveAsync(){if(LastResult is null)return;if(!_dialogs.Confirm("撤销前会重新校验目标文件、原路径和哈希；任何前提不满足都会停止。","撤销移动整理"))return;IsBusy=true;try{StatusMessage=await _service.UndoMoveAsync(LastResult.Manifest)?"移动整理已安全撤销":"撤销前置条件不满足，未执行任何强制覆盖";}finally{IsBusy=false;}}
    private void ExcludeSelected(){if(SelectedPhoto is null)return;SelectedPhoto.Excluded=true;Regroup();StatusMessage=$"已排除 {SelectedPhoto.FileName}，源文件未删除";}
    private void NewGroup(){Groups.Add(new PhotoGroupDefinition{Name=$"新分组 {Groups.Count+1}"});}
    private void DeleteEmptyGroups(){foreach(var group in Groups.Where(x=>x.Count==0).ToArray())Groups.Remove(group);}
    public void MovePhotosToGroup(IEnumerable<OrganizePhotoItem> photos, PhotoGroupDefinition? target)
    {
        if(target is null)return;
        foreach(var photo in photos.Distinct())
        {
            foreach(var group in Groups)group.SourcePaths.RemoveAll(path=>string.Equals(path,photo.SourcePath,StringComparison.OrdinalIgnoreCase));
            if(!target.SourcePaths.Contains(photo.SourcePath,StringComparer.OrdinalIgnoreCase))target.SourcePaths.Add(photo.SourcePath);
            photo.GroupName=target.Name;
        }
        InvalidatePlan();OnPropertyChanged(nameof(Groups));StatusMessage=$"已移动照片到“{target.Name}”，源文件未修改";
    }
    private void MergeSelectedGroup(){if(SelectedGroup is null||Groups.Count<2)return;var target=Groups.First(x=>!ReferenceEquals(x,SelectedGroup));foreach(var path in SelectedGroup.SourcePaths)if(!target.SourcePaths.Contains(path,StringComparer.OrdinalIgnoreCase))target.SourcePaths.Add(path);foreach(var photo in Photos.Where(x=>SelectedGroup.SourcePaths.Contains(x.SourcePath,StringComparer.OrdinalIgnoreCase)))photo.GroupName=target.Name;Groups.Remove(SelectedGroup);SelectedGroup=target;InvalidatePlan();}
    private void SplitSelectedGroup(){if(SelectedGroup is null||SelectedGroup.Count<2)return;var created=new PhotoGroupDefinition{Name=SelectedGroup.Name+"_拆分"};foreach(var path in SelectedGroup.SourcePaths.Skip(SelectedGroup.Count/2).ToArray()){SelectedGroup.SourcePaths.Remove(path);created.SourcePaths.Add(path);var photo=Photos.First(x=>string.Equals(x.SourcePath,path,StringComparison.OrdinalIgnoreCase));photo.GroupName=created.Name;}Groups.Add(created);SelectedGroup=created;InvalidatePlan();}
    private void DeleteSelectedGroup(){if(SelectedGroup is null)return;var removed=SelectedGroup;var fallback=Groups.FirstOrDefault(x=>!ReferenceEquals(x,removed)&&x.Name=="未分组")??new PhotoGroupDefinition{Name="未分组"};if(!Groups.Contains(fallback))Groups.Add(fallback);foreach(var path in removed.SourcePaths){if(!fallback.SourcePaths.Contains(path,StringComparer.OrdinalIgnoreCase))fallback.SourcePaths.Add(path);var photo=Photos.First(x=>string.Equals(x.SourcePath,path,StringComparison.OrdinalIgnoreCase));photo.GroupName=fallback.Name;}Groups.Remove(removed);SelectedGroup=fallback;InvalidatePlan();StatusMessage="已删除分组定义，源照片未删除";}
    private void SetCover(){if(SelectedGroup is null||SelectedPhoto is null)return;SelectedGroup.CoverSourcePath=SelectedPhoto.SourcePath;StatusMessage=$"已将 {SelectedPhoto.FileName} 设为分组封面";}
    private void SortGroups(){var ordered=Groups.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();Groups.Clear();foreach(var group in ordered)Groups.Add(group);InvalidatePlan();}
    private void InvalidatePlan(){CurrentPlan=null;}
    private void RefreshCommandStates()
    {
        BrowseOutputCommand.RaiseCanExecuteChanged(); GroupCommand.RaiseCanExecuteChanged(); PreviewPlanCommand.RaiseCanExecuteChanged(); CancelCommand.RaiseCanExecuteChanged(); ExcludeSelectedCommand.RaiseCanExecuteChanged();
        NewGroupCommand.RaiseCanExecuteChanged(); DeleteEmptyGroupsCommand.RaiseCanExecuteChanged(); MergeGroupCommand.RaiseCanExecuteChanged(); SplitGroupCommand.RaiseCanExecuteChanged(); DeleteGroupCommand.RaiseCanExecuteChanged();
        SetCoverCommand.RaiseCanExecuteChanged(); SortGroupsCommand.RaiseCanExecuteChanged(); AddPhotosCommand.RaiseCanExecuteChanged(); AddFolderCommand.RaiseCanExecuteChanged(); ExecuteCommand.RaiseCanExecuteChanged(); ExportReportsCommand.RaiseCanExecuteChanged(); UndoMoveCommand.RaiseCanExecuteChanged();
    }
    private static string FormatBytes(long bytes)=>bytes>=1024L*1024*1024?$"{bytes/1024d/1024/1024:0.##} GB":$"{bytes/1024d/1024:0.##} MB";
}

public sealed class CollageImageViewModel : ObservableObject
{
    private readonly CollageImageState _state;
    public CollageImageViewModel(CollageImageState state){_state=state;try{Thumbnail=CollageExportService.LoadBitmap(state.SourcePath);}catch{}}
    public string SourcePath=>_state.SourcePath;
    public string FileName=>Path.GetFileName(SourcePath);
    public BitmapImage? Thumbnail{get;}
    public CollageImageState State=>_state;
}

public sealed class CollageViewModel : ObservableObject
{
    private readonly CollageExportService _exportService;
    private readonly IDialogService _dialogs;
    private CollageTemplate _selectedTemplate;
    private CollageImageViewModel? _selectedImage;
    private CollageMode _mode;
    private string _aspectRatio="1:1";
    private bool _isBusy;
    private double _progress;
    private string _statusMessage="导入照片后选择模板";
    private CancellationTokenSource? _cancellation;
    private readonly Stack<List<CollageImageState>> _undo = new();
    private readonly Stack<List<CollageImageState>> _redo = new();

    public CollageViewModel(CollageExportService exportService, IDialogService dialogs)
    {
        _exportService=exportService;_dialogs=dialogs;_selectedTemplate=CollageTemplateCatalog.All[0];
        AddPhotosCommand=new RelayCommand(_=>AddPhotos(),_=>!IsBusy);ClearCommand=new RelayCommand(_=>Clear(),_=>!IsBusy&&Images.Count>0);RemoveCommand=new RelayCommand(_=>RemoveSelected(),_=>!IsBusy&&SelectedImage is not null);ReplaceCommand=new RelayCommand(_=>ReplaceSelected(),_=>!IsBusy&&SelectedImage is not null);SwapLeftCommand=new RelayCommand(_=>MoveSelected(-1),_=>SelectedImage is not null);SwapRightCommand=new RelayCommand(_=>MoveSelected(1),_=>SelectedImage is not null);RotateCommand=new RelayCommand(_=>TransformSelected("rotate"),_=>SelectedImage is not null);FlipHorizontalCommand=new RelayCommand(_=>TransformSelected("flipH"),_=>SelectedImage is not null);FlipVerticalCommand=new RelayCommand(_=>TransformSelected("flipV"),_=>SelectedImage is not null);ResetImageCommand=new RelayCommand(_=>TransformSelected("reset"),_=>SelectedImage is not null);UndoCommand=new RelayCommand(_=>Undo());RedoCommand=new RelayCommand(_=>Redo());ExportCommand=new AsyncRelayCommand(_=>ExportAsync(),_=>!IsBusy&&Images.Count>0);CancelCommand=new RelayCommand(_=>_cancellation?.Cancel(),_=>IsBusy);
    }

    public string PageTitle=>ToolRegistry.Get(ToolId.Collage).DisplayName;
    public CollageProject Project{get;}=new();
    public ObservableCollection<CollageImageViewModel> Images{get;}=[];
    public IReadOnlyList<CollageTemplate> Templates=>CollageTemplateCatalog.All;
    public IReadOnlyList<OptionItem<CollageMode>> Modes{get;}=[new(CollageMode.Template,"模板拼图"),new(CollageMode.VerticalStrip,"纵向长图"),new(CollageMode.HorizontalStrip,"横向长图")];
    public IReadOnlyList<string> AspectRatios{get;}=["自由","1:1","4:5","3:4","2:3","3:2","16:9","9:16","A4 竖版","A4 横版","1080×1080","1080×1350","1080×1440","1920×1080","1080×1920"];
    public CollageTemplate SelectedTemplate{get=>_selectedTemplate;set{if(!SetProperty(ref _selectedTemplate,value))return;Project.TemplateId=value.Id;AssignSlots();NotifyPreview();}}
    public CollageMode Mode{get=>_mode;set{if(!SetProperty(ref _mode,value))return;Project.Mode=value;NotifyPreview();}}
    public string AspectRatio{get=>_aspectRatio;set{if(!SetProperty(ref _aspectRatio,value))return;ApplyAspect(value);OnPropertyChanged(nameof(EstimatedExportSizeText));NotifyPreview();}}
    public CollageImageViewModel? SelectedImage{get=>_selectedImage;set{if(!SetProperty(ref _selectedImage,value))return;OnPropertyChanged(nameof(SelectedZoom));OnPropertyChanged(nameof(SelectedOffsetX));OnPropertyChanged(nameof(SelectedOffsetY));OnPropertyChanged(nameof(SelectedFitMode));}}
    public bool IsBusy{get=>_isBusy;private set=>SetProperty(ref _isBusy,value);}
    public double Progress{get=>_progress;private set=>SetProperty(ref _progress,value);}
    public string StatusMessage{get=>_statusMessage;private set=>SetProperty(ref _statusMessage,value);}
    public string SpacingText=>$"间距 {Project.Export.Spacing:0} px";
    public string MarginText=>$"外边距 {Project.Export.OuterMargin:0} px";
    public string CornerText=>$"圆角 {Project.Export.CornerRadius:0} px";
    public double Spacing{get=>Project.Export.Spacing;set{Project.Export.Spacing=value;OnPropertyChanged();OnPropertyChanged(nameof(SpacingText));NotifyPreview();}}
    public double OuterMargin{get=>Project.Export.OuterMargin;set{Project.Export.OuterMargin=value;OnPropertyChanged();OnPropertyChanged(nameof(MarginText));NotifyPreview();}}
    public double CornerRadius{get=>Project.Export.CornerRadius;set{Project.Export.CornerRadius=value;OnPropertyChanged();OnPropertyChanged(nameof(CornerText));NotifyPreview();}}
    public double BorderWidth{get=>Project.Export.BorderWidth;set{Project.Export.BorderWidth=value;OnPropertyChanged();NotifyPreview();}}
    public string BackgroundColor{get=>Project.Export.BackgroundColor;set{Project.Export.BackgroundColor=value;OnPropertyChanged();NotifyPreview();}}
    public string BorderColor{get=>Project.Export.BorderColor;set{Project.Export.BorderColor=value;OnPropertyChanged();NotifyPreview();}}
    public bool TransparentBackground{get=>Project.Export.TransparentBackground;set{Project.Export.TransparentBackground=value;OnPropertyChanged();NotifyPreview();}}
    public bool Shadow{get=>Project.Export.Shadow;set{Project.Export.Shadow=value;OnPropertyChanged();NotifyPreview();}}
    public int JpegQuality{get=>Project.Export.JpegQuality;set{Project.Export.JpegQuality=Math.Clamp(value,1,100);OnPropertyChanged();}}
    public string EstimatedExportSizeText=>$"约 {Project.Export.PixelWidth*Project.Export.PixelHeight*(Project.Export.Format=="PNG"?1.2:.35)/1024/1024:0.##} MB";
    public IReadOnlyList<OptionItem<CollageFitMode>> FitModes{get;}=[new(CollageFitMode.FillCrop,"填充裁切"),new(CollageFitMode.Fit,"完整显示"),new(CollageFitMode.OriginalRatio,"原始比例")];
    public CollageFitMode SelectedFitMode{get=>SelectedImage?.State.FitMode??CollageFitMode.FillCrop;set{if(SelectedImage is null)return;PushUndo();SelectedImage.State.FitMode=value;OnPropertyChanged();NotifyPreview();}}
    public double SelectedZoom{get=>SelectedImage?.State.Zoom??1;set{if(SelectedImage is null)return;SelectedImage.State.Zoom=Math.Clamp(value,.2,8);OnPropertyChanged();NotifyPreview();}}
    public double SelectedOffsetX{get=>SelectedImage?.State.OffsetX??0;set{if(SelectedImage is null)return;SelectedImage.State.OffsetX=Math.Clamp(value,-.5,.5);OnPropertyChanged();NotifyPreview();}}
    public double SelectedOffsetY{get=>SelectedImage?.State.OffsetY??0;set{if(SelectedImage is null)return;SelectedImage.State.OffsetY=Math.Clamp(value,-.5,.5);OnPropertyChanged();NotifyPreview();}}
    public event EventHandler? PreviewChanged;
    public RelayCommand AddPhotosCommand{get;} public RelayCommand ClearCommand{get;} public RelayCommand RemoveCommand{get;} public RelayCommand ReplaceCommand{get;} public RelayCommand SwapLeftCommand{get;} public RelayCommand SwapRightCommand{get;} public RelayCommand RotateCommand{get;} public RelayCommand FlipHorizontalCommand{get;} public RelayCommand FlipVerticalCommand{get;} public RelayCommand ResetImageCommand{get;} public RelayCommand UndoCommand{get;} public RelayCommand RedoCommand{get;} public RelayCommand CancelCommand{get;} public AsyncRelayCommand ExportCommand{get;}

    public void AddPaths(IEnumerable<string> paths){var values=paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Where(path=>Project.Images.All(x=>!string.Equals(x.SourcePath,path,StringComparison.OrdinalIgnoreCase))).ToArray();if(values.Length==0)return;PushUndo();foreach(var path in values){var state=new CollageImageState{SourcePath=Path.GetFullPath(path),SlotId=(Project.Images.Count+1).ToString()};Project.Images.Add(state);Images.Add(new CollageImageViewModel(state));}AssignSlots();StatusMessage=$"已导入 {Images.Count} 张照片";NotifyPreview();}
    private void AddPhotos()=>AddPaths(_dialogs.ChooseFiles("选择拼图照片","照片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp|所有文件|*.*"));
    private void Clear(){PushUndo();Project.Images.Clear();Images.Clear();SelectedImage=null;StatusMessage="画布已清空";NotifyPreview();}
    private void RemoveSelected(){if(SelectedImage is null)return;PushUndo();Project.Images.Remove(SelectedImage.State);Images.Remove(SelectedImage);SelectedImage=null;AssignSlots();NotifyPreview();}
    private void ReplaceSelected(){if(SelectedImage is null)return;var path=_dialogs.ChooseFiles("替换所选照片","照片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp|所有文件|*.*",false).FirstOrDefault();if(path is null)return;PushUndo();var index=Images.IndexOf(SelectedImage);var state=CloneState(SelectedImage.State);state.SourcePath=Path.GetFullPath(path);Project.Images[index]=state;Images[index]=new CollageImageViewModel(state);SelectedImage=Images[index];NotifyPreview();}
    private void MoveSelected(int offset){if(SelectedImage is null)return;var from=Images.IndexOf(SelectedImage);var to=Math.Clamp(from+offset,0,Images.Count-1);if(from==to)return;PushUndo();Images.Move(from,to);Project.Images.Remove(SelectedImage.State);Project.Images.Insert(to,SelectedImage.State);AssignSlots();NotifyPreview();}
    private void TransformSelected(string action){if(SelectedImage is null)return;PushUndo();var state=SelectedImage.State;switch(action){case"rotate":state.Rotation=(state.Rotation+90)%360;break;case"flipH":state.FlipHorizontal=!state.FlipHorizontal;break;case"flipV":state.FlipVertical=!state.FlipVertical;break;default:state.Zoom=1;state.OffsetX=state.OffsetY=0;state.Rotation=0;state.FlipHorizontal=state.FlipVertical=false;break;}OnPropertyChanged(nameof(SelectedZoom));OnPropertyChanged(nameof(SelectedOffsetX));OnPropertyChanged(nameof(SelectedOffsetY));NotifyPreview();}
    public void AdjustSelectedZoom(double delta){if(SelectedImage is null)return;SelectedZoom=Math.Clamp(SelectedZoom+delta,.2,8);}
    private void PushUndo(){_undo.Push(Project.Images.Select(CloneState).ToList());_redo.Clear();}
    private void Undo(){if(_undo.Count==0)return;_redo.Push(Project.Images.Select(CloneState).ToList());Restore(_undo.Pop());StatusMessage="已撤销";}
    private void Redo(){if(_redo.Count==0)return;_undo.Push(Project.Images.Select(CloneState).ToList());Restore(_redo.Pop());StatusMessage="已重做";}
    private void Restore(IEnumerable<CollageImageState> states){Project.Images.Clear();Images.Clear();foreach(var state in states.Select(CloneState)){Project.Images.Add(state);Images.Add(new CollageImageViewModel(state));}AssignSlots();SelectedImage=Images.FirstOrDefault();NotifyPreview();}
    private static CollageImageState CloneState(CollageImageState state)=>new(){SourcePath=state.SourcePath,SlotId=state.SlotId,Zoom=state.Zoom,OffsetX=state.OffsetX,OffsetY=state.OffsetY,Rotation=state.Rotation,FlipHorizontal=state.FlipHorizontal,FlipVertical=state.FlipVertical,FitMode=state.FitMode};
    private void AssignSlots(){for(var i=0;i<Project.Images.Count;i++)Project.Images[i].SlotId=(i+1).ToString();}
    private async Task ExportAsync(){Project.Export.Format="JPG";var path=_dialogs.ChooseSaveFile("导出拼图","JPEG|*.jpg|PNG|*.png",".jpg",$"像素蛋挞拼图_{DateTime.Now:yyyyMMdd_HHmm}.jpg");if(path is null)return;Project.Export.Format=string.Equals(Path.GetExtension(path),".png",StringComparison.OrdinalIgnoreCase)?"PNG":"JPG";if(Project.Export.Format=="JPG")Project.Export.TransparentBackground=false;OnPropertyChanged(nameof(EstimatedExportSizeText));if(!_dialogs.Confirm($"输出尺寸：{Project.Export.PixelWidth} × {Project.Export.PixelHeight}\n格式：{Project.Export.Format}\n质量：{Project.Export.JpegQuality}\n透明背景：{Project.Export.TransparentBackground}\n预计文件大小：{EstimatedExportSizeText}\n输出：{path}\n\n默认不覆盖，冲突时自动编号。是否导出？","确认拼图导出"))return;IsBusy=true;_cancellation=new();StatusMessage="正在从原图导出…";try{var result=await _exportService.ExportAsync(Project,path,_cancellation.Token);StatusMessage=$"已导出 {result.PixelWidth}×{result.PixelHeight}，{result.FileSizeBytes/1024d/1024:0.##} MB";if(_dialogs.Confirm("拼图已导出，是否在资源管理器中定位文件？","导出完成"))_dialogs.RevealFile(result.OutputPath);}catch(OperationCanceledException){StatusMessage="已取消导出，不完整文件已清理";}catch(Exception ex){_dialogs.ShowError(ex.Message);StatusMessage="导出失败";}finally{IsBusy=false;_cancellation.Dispose();_cancellation=null;Progress=0;}}
    private void ApplyAspect(string value){(Project.Export.PixelWidth,Project.Export.PixelHeight)=value switch{"4:5"=>(1920,2400),"3:4"=>(1800,2400),"2:3"=>(1600,2400),"3:2"=>(2400,1600),"16:9"=>(1920,1080),"9:16"=>(1080,1920),"A4 竖版"=>(2480,3508),"A4 横版"=>(3508,2480),"1080×1080"=>(1080,1080),"1080×1350"=>(1080,1350),"1080×1440"=>(1080,1440),"1920×1080"=>(1920,1080),"1080×1920"=>(1080,1920),_=>(2400,2400)};}
    private void NotifyPreview()=>PreviewChanged?.Invoke(this,EventArgs.Empty);
}
