using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class PlanningCenterViewModel : ObservableObject
{
    private readonly IProjectRepository _projects;
    private readonly IShootBookingService _bookings;
    private readonly IDialogService _dialogs;
    private readonly ProjectShotStore _shots;
    private readonly PlanningProjectStore _planning;
    private readonly ProjectVisualReferenceStore _visuals;
    private readonly ReferenceLookStore _looks;
    private readonly CanvasDocumentStore _canvases;
    private readonly IInspirationTrayService? _inspiration;
    private PhotoProjectRecord? _project;
    private ShootBooking? _booking;
    private PlanningProjectState? _state;
    private ProjectShot? _selectedShot;
    private ProjectShotReference? _selectedReference;
    private string _searchText="";
    private ProjectShotStatus? _filter;
    private string _saveStatus="已保存";
    private bool _isQuickPreviewOpen;
    private bool _isSourceDrawerOpen;
    private ShotReferenceKindOption _selectedReferenceKind=ShotReferenceKindOption.All[0];
    private CancellationTokenSource? _autosave;
    private CancellationTokenSource? _shotAutosave;
    private string _shotTitle="";
    private string _shotScene="";
    private string _shotNotes="";
    private string _referenceNote="";
    private int _shotEstimatedMinutes;

    public PlanningCenterViewModel(IProjectRepository projects, IShootBookingService bookings, IDialogService dialogs,
        ProjectShotStore? shots=null, PlanningProjectStore? planning=null, ProjectVisualReferenceStore? visuals=null,
        ReferenceLookStore? looks=null, CanvasDocumentStore? canvases=null, IInspirationTrayService? inspiration=null)
    {
        _projects=projects;_bookings=bookings;_dialogs=dialogs;
        _shots=shots??new(Path.Combine(AppDataPaths.DataDirectory,"ProjectShots"));
        _planning=planning??new(Path.Combine(AppDataPaths.DataDirectory,"ProjectPlanning"));
        _visuals=visuals??new(Path.Combine(AppDataPaths.DataDirectory,"ProjectVisuals"));
        _looks=looks??new(Path.Combine(AppDataPaths.DataDirectory,"ProjectVisuals"));
        _canvases=canvases??new(Path.Combine(AppDataPaths.DataDirectory,"FreeCanvas"));_inspiration=inspiration;
        ShotsView=CollectionViewSource.GetDefaultView(Shots);ShotsView.Filter=FilterShot;
        NewShotCommand=new AsyncRelayCommand(_=>NewShotAsync());DuplicateShotCommand=new AsyncRelayCommand(_=>DuplicateShotAsync(),_=>SelectedShot is not null);
        ArchiveShotCommand=new AsyncRelayCommand(_=>ArchiveShotAsync(),_=>SelectedShot is not null);SetCurrentShotCommand=new AsyncRelayCommand(_=>SetCurrentShotAsync(),_=>SelectedShot is not null);
        MarkShotCompletedCommand=new AsyncRelayCommand(_=>SetStatusAsync(ProjectShotStatus.Completed),_=>SelectedShot is not null);MarkShotSkippedCommand=new AsyncRelayCommand(_=>SetStatusAsync(ProjectShotStatus.Skipped),_=>SelectedShot is not null);
        MoveShotUpCommand=new AsyncRelayCommand(_=>MoveShotAsync(-1),_=>CanMove(-1));MoveShotDownCommand=new AsyncRelayCommand(_=>MoveShotAsync(1),_=>CanMove(1));
        RemoveReferenceCommand=new AsyncRelayCommand(_=>RemoveReferenceAsync(),_=>SelectedShot is not null&&SelectedReference is not null);
        TogglePinCommand=new AsyncRelayCommand(_=>TogglePinAsync(),_=>SelectedShot is not null&&SelectedReference is not null);
        AddLocalReferenceCommand=new AsyncRelayCommand(_=>AddLocalReferenceAsync(),_=>SelectedShot is not null);
        MoveReferenceUpCommand=new AsyncRelayCommand(_=>MoveReferenceAsync(-1),_=>CanMoveReference(-1));
        MoveReferenceDownCommand=new AsyncRelayCommand(_=>MoveReferenceAsync(1),_=>CanMoveReference(1));
        OpenQuickPreviewCommand=new RelayCommand(_=>IsQuickPreviewOpen=SelectedReference is not null,_=>SelectedReference is not null);CloseQuickPreviewCommand=new RelayCommand(_=>IsQuickPreviewOpen=false);
        ToggleSourceDrawerCommand=new RelayCommand(_=>IsSourceDrawerOpen=!IsSourceDrawerOpen);
        EnterTetherCommand=new AsyncRelayCommand(_=>EnterTetherAsync(),_=>ProjectId is not null&&SelectedShot is not null);
    }

    public event EventHandler<ShootExecutionContext>? EnterTetherRequested;
    public ObservableCollection<ProjectShot> Shots{get;}=[]; public ICollectionView ShotsView{get;}
    public ObservableCollection<ProjectShotReference> References{get;}=[];public ObservableCollection<PlanningVisualLink> VisualLinks{get;}=[];
    public ObservableCollection<CanvasDocument> ProjectCanvases{get;}=[];public ObservableCollection<InspirationCollectionSummary> ProjectBoards{get;}=[];
    public ObservableCollection<ProjectPaletteColorItem> PaletteColors{get;}=[];public ObservableCollection<ReferenceLook> ColorSchemes{get;}=[];
    public RelayCommand OpenQuickPreviewCommand{get;} public RelayCommand CloseQuickPreviewCommand{get;} public RelayCommand ToggleSourceDrawerCommand{get;}
    public AsyncRelayCommand NewShotCommand{get;} public AsyncRelayCommand DuplicateShotCommand{get;} public AsyncRelayCommand ArchiveShotCommand{get;} public AsyncRelayCommand SetCurrentShotCommand{get;}
    public AsyncRelayCommand MarkShotCompletedCommand{get;} public AsyncRelayCommand MarkShotSkippedCommand{get;} public AsyncRelayCommand MoveShotUpCommand{get;} public AsyncRelayCommand MoveShotDownCommand{get;}
    public AsyncRelayCommand RemoveReferenceCommand{get;} public AsyncRelayCommand TogglePinCommand{get;}
    public AsyncRelayCommand AddLocalReferenceCommand{get;} public AsyncRelayCommand MoveReferenceUpCommand{get;} public AsyncRelayCommand MoveReferenceDownCommand{get;}
    public AsyncRelayCommand EnterTetherCommand{get;private set;}=null!;
    public Guid? ProjectId=>_project?.Id;public string ProjectName=>_project?.Name??"未选择项目";public string BookingDate=>_booking?.StartAtUtc.ToLocalTime().ToString("MM月dd日")??"未安排日期";public string Location=>_booking?.Location??"未填写地点";
    public string ProgressText=>$"{Shots.Count(shot=>shot.Status==ProjectShotStatus.Completed)} / {Shots.Count} 已拍";public string EstimatedTimeText=>$"预计 {Shots.Sum(shot=>shot.EstimatedMinutes)} 分钟";
    public string SaveStatus{get=>_saveStatus;private set=>SetProperty(ref _saveStatus,value);}public bool HasProject=>ProjectId is not null;public bool HasShots=>Shots.Count>0;
    public ProjectShot? SelectedShot{get=>_selectedShot;set{if(SetProperty(ref _selectedShot,value)){LoadShotEditor(value);RefreshReferences();OnPropertyChanged(nameof(ShotHeading));OnPropertyChanged(nameof(InspectorHeading));}}}
    public ProjectShotReference? SelectedReference{get=>_selectedReference;set{if(SetProperty(ref _selectedReference,value)){_referenceNote=value?.Note??"";OnPropertyChanged(nameof(ReferenceNote));OnPropertyChanged(nameof(InspectorHeading));}}}
    public string ShotHeading=>SelectedShot is null?"尚未建立拍摄清单":$"Shot {SelectedShot.Order+1:00} · {SelectedShot.Name}";public string InspectorHeading=>SelectedReference?.Title??SelectedShot?.Name??ProjectName;
    public string SearchText{get=>_searchText;set{if(SetProperty(ref _searchText,value))ShotsView.Refresh();}}public ProjectShotStatus? SelectedFilter{get=>_filter;set{if(SetProperty(ref _filter,value))ShotsView.Refresh();}}
    public bool IsQuickPreviewOpen{get=>_isQuickPreviewOpen;set=>SetProperty(ref _isQuickPreviewOpen,value);}public bool IsSourceDrawerOpen{get=>_isSourceDrawerOpen;set=>SetProperty(ref _isSourceDrawerOpen,value);}
    public IReadOnlyList<ShotReferenceKindOption> ReferenceKinds=>ShotReferenceKindOption.All;
    public ShotReferenceKindOption SelectedReferenceKind{get=>_selectedReferenceKind;set=>SetProperty(ref _selectedReferenceKind,value);}
    public string ShotTitle{get=>_shotTitle;set{if(SetProperty(ref _shotTitle,value))QueueShotAutosave();}}
    public string ShotScene{get=>_shotScene;set{if(SetProperty(ref _shotScene,value))QueueShotAutosave();}}
    public string ShotNotes{get=>_shotNotes;set{if(SetProperty(ref _shotNotes,value))QueueShotAutosave();}}
    public int ShotEstimatedMinutes{get=>_shotEstimatedMinutes;set{if(SetProperty(ref _shotEstimatedMinutes,Math.Clamp(value,0,1440)))QueueShotAutosave();}}
    public string ReferenceNote{get=>_referenceNote;set{if(SetProperty(ref _referenceNote,value))QueueReferenceAutosave();}}
    public string ShootGoal{get=>_state?.Summary?.ShootGoal??"";set=>UpdateSummary(summary=>summary with{ShootGoal=value});}public string Keywords{get=>string.Join(" / ",_state?.Summary?.Keywords??[]);set=>UpdateSummary(summary=>summary with{Keywords=value.Split(['/','，',','],StringSplitOptions.RemoveEmptyEntries)});}
    public string ClientRequirements{get=>_state?.Summary?.ClientRequirements??"";set=>UpdateSummary(summary=>summary with{ClientRequirements=value});}public string MustCapture{get=>_state?.Summary?.MustCapture??"";set=>UpdateSummary(summary=>summary with{MustCapture=value});}
    public string PlanningNotes{get=>_state?.Summary?.Notes??"";set=>UpdateSummary(summary=>summary with{Notes=value});}public string OutputPurpose{get=>_state?.Summary?.OutputPurpose??"";set=>UpdateSummary(summary=>summary with{OutputPurpose=value});}

    public async Task LoadAsync(Guid projectId,Guid? bookingId=null,CancellationToken token=default)
    {
        _project=(await _projects.ListAsync(token)).FirstOrDefault(item=>item.Id==projectId);if(_project is null)throw new InvalidOperationException("项目不存在或已归档。");
        _state=await _planning.LoadAsync(projectId,token);if(bookingId is Guid id){_booking=await _bookings.GetAsync(id,false,token);if(_state.BookingId!=id){_state=_state with{BookingId=id};await _planning.SaveAsync(_state,token);_state=await _planning.LoadAsync(projectId,token);}}
        var catalog=await _shots.LoadAsync(projectId,token);Shots.Clear();foreach(var shot in catalog.Shots.Where(item=>!item.IsArchived).OrderBy(item=>item.Order))Shots.Add(shot);SelectedShot=Shots.FirstOrDefault(item=>item.ShotId==_state.CurrentShotId)??Shots.FirstOrDefault();
        VisualLinks.Clear();foreach(var link in _state.VisualLinks??[])VisualLinks.Add(link);ProjectCanvases.Clear();foreach(var canvas in await _canvases.ListAsync(projectId,token))ProjectCanvases.Add(canvas);
        ProjectBoards.Clear();if(_inspiration is not null)foreach(var board in (await _inspiration.ListCollectionsAsync(token)).Where(item=>item.ProjectId==projectId))ProjectBoards.Add(board);
        var visual=await _visuals.LoadAsync(projectId,token);PaletteColors.Clear();if(visual.DefaultPalette is not null)foreach(var color in visual.DefaultPalette.Colors)PaletteColors.Add(new(color.Hex,color.Weight));
        ColorSchemes.Clear();var catalogLooks=await _looks.LoadAsync(token);foreach(var look in catalogLooks.Looks.Where(item=>item.ProjectId==projectId))ColorSchemes.Add(look);
        NotifyAll();EnterTetherCommand.RaiseCanExecuteChanged();
    }

    private async Task NewShotAsync(){if(ProjectId is not Guid project)return;var now=DateTimeOffset.UtcNow;var shot=new ProjectShot(Guid.NewGuid(),project,Shots.Count,"未命名拍摄",ProjectShotStatus.NotStarted,null,null,[],now,now,EstimatedMinutes:10);await _shots.SaveAsync(shot);Shots.Add(shot);SelectedShot=shot;NotifyAll();}
    private async Task DuplicateShotAsync(){if(SelectedShot is not{} source)return;var now=DateTimeOffset.UtcNow;var copy=source with{ShotId=Guid.NewGuid(),Order=Shots.Count,Name=source.Name+" 副本",Status=ProjectShotStatus.NotStarted,CapturedAt=null,CreatedAt=now,UpdatedAt=now,References=source.References.Select(item=>item with{ReferenceId=Guid.NewGuid(),PoseStatus=PoseExecutionStatus.NotShot}).ToArray()};await _shots.SaveAsync(copy);Shots.Add(copy);SelectedShot=copy;NotifyAll();}
    private async Task ArchiveShotAsync(){if(SelectedShot is not{} shot)return;await _shots.SaveAsync(shot with{IsArchived=true,UpdatedAt=DateTimeOffset.UtcNow});Shots.Remove(shot);SelectedShot=Shots.FirstOrDefault();NotifyAll();}
    private async Task SetCurrentShotAsync(){if(ProjectId is not Guid project||SelectedShot is null)return;await _planning.SetCurrentShotAsync(project,SelectedShot.ShotId);_state=await _planning.LoadAsync(project);await SetStatusAsync(ProjectShotStatus.InProgress);}
    private async Task SetStatusAsync(ProjectShotStatus status){if(SelectedShot is not{} shot)return;var updated=shot with{Status=status,CapturedAt=status==ProjectShotStatus.Completed?DateTimeOffset.UtcNow:shot.CapturedAt,UpdatedAt=DateTimeOffset.UtcNow};await _shots.SaveAsync(updated);ReplaceShot(updated);}
    private bool CanMove(int delta)=>SelectedShot is not null&&Shots.IndexOf(SelectedShot)+delta>=0&&Shots.IndexOf(SelectedShot)+delta<Shots.Count;
    private async Task MoveShotAsync(int delta){if(ProjectId is not Guid project||SelectedShot is null)return;var index=Shots.IndexOf(SelectedShot);var target=index+delta;if(target<0||target>=Shots.Count)return;Shots.Move(index,target);await _shots.ReorderAsync(project,Shots.Select(item=>item.ShotId).ToArray());var catalog=await _shots.LoadAsync(project);Shots.Clear();foreach(var shot in catalog.Shots.Where(item=>!item.IsArchived))Shots.Add(shot);SelectedShot=Shots.First(item=>item.ShotId==_selectedShot!.ShotId);}
    public async Task ReorderShotAsync(ProjectShot source,ProjectShot target){if(ProjectId is not Guid project||source.ShotId==target.ShotId)return;var from=Shots.IndexOf(source);var to=Shots.IndexOf(target);if(from<0||to<0)return;Shots.Move(from,to);await _shots.ReorderAsync(project,Shots.Select(item=>item.ShotId).ToArray());var catalog=await _shots.LoadAsync(project);var selected=SelectedShot?.ShotId;Shots.Clear();foreach(var shot in catalog.Shots.Where(item=>!item.IsArchived).OrderBy(item=>item.Order))Shots.Add(shot);SelectedShot=Shots.FirstOrDefault(item=>item.ShotId==selected)??Shots.FirstOrDefault();}
    private async Task RemoveReferenceAsync(){if(SelectedShot is not{} shot||SelectedReference is not{} reference)return;var updated=shot with{References=shot.References.Where(item=>item.ReferenceId!=reference.ReferenceId).ToArray(),UpdatedAt=DateTimeOffset.UtcNow};await _shots.SaveAsync(updated);ReplaceShot(updated);}
    private async Task TogglePinAsync(){if(SelectedShot is not{} shot||SelectedReference is not{} reference)return;var updated=shot with{References=shot.References.Select(item=>item.ReferenceId==reference.ReferenceId?item with{IsPinned=!item.IsPinned}:item).ToArray(),UpdatedAt=DateTimeOffset.UtcNow};await _shots.SaveAsync(updated);ReplaceShot(updated);}
    private async Task AddLocalReferenceAsync()
    {
        if(SelectedShot is not{} shot)return;
        var paths=_dialogs.ChooseFiles("选择拍摄参考（仅关联原位置）","图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.webp|所有文件|*.*",true);
        if(paths.Count==0)return;
        var additions=paths.Select(path=>new ProjectShotReference(Guid.NewGuid(),SelectedReferenceKind.Kind,ExternalReference:Path.GetFullPath(path),Title:Path.GetFileNameWithoutExtension(path))).ToArray();
        var updated=shot with{References=shot.References.Concat(additions).ToArray(),UpdatedAt=DateTimeOffset.UtcNow};
        await _shots.SaveAsync(updated);ReplaceShot(updated);SelectedReference=References.FirstOrDefault(item=>item.ReferenceId==additions[0].ReferenceId);
    }
    private bool CanMoveReference(int delta)=>SelectedReference is not null&&References.IndexOf(SelectedReference)+delta>=0&&References.IndexOf(SelectedReference)+delta<References.Count;
    private async Task MoveReferenceAsync(int delta)
    {
        if(SelectedShot is not{} shot||SelectedReference is not{} reference)return;
        var references=shot.References.ToList();var index=references.FindIndex(item=>item.ReferenceId==reference.ReferenceId);var target=index+delta;if(index<0||target<0||target>=references.Count)return;
        (references[index],references[target])=(references[target],references[index]);var updated=shot with{References=references,UpdatedAt=DateTimeOffset.UtcNow};await _shots.SaveAsync(updated);ReplaceShot(updated);SelectedReference=References.First(item=>item.ReferenceId==reference.ReferenceId);
    }
    private void LoadShotEditor(ProjectShot? shot){_shotTitle=shot?.Name??"";_shotScene=shot?.Scene??"";_shotNotes=shot?.Notes??"";_shotEstimatedMinutes=shot?.EstimatedMinutes??0;foreach(var name in new[]{nameof(ShotTitle),nameof(ShotScene),nameof(ShotNotes),nameof(ShotEstimatedMinutes)})OnPropertyChanged(name);}
    private async void QueueShotAutosave(){_shotAutosave?.Cancel();_shotAutosave?.Dispose();_shotAutosave=new();var token=_shotAutosave.Token;SaveStatus="正在保存…";try{await Task.Delay(350,token);if(SelectedShot is not{} shot||string.IsNullOrWhiteSpace(_shotTitle))return;var updated=shot with{Name=_shotTitle,Scene=_shotScene,Notes=_shotNotes,EstimatedMinutes=_shotEstimatedMinutes,UpdatedAt=DateTimeOffset.UtcNow};await _shots.SaveAsync(updated,token);ReplaceShot(updated);SaveStatus="已保存";}catch(OperationCanceledException){}catch{SaveStatus="保存失败，请稍后重试";}}
    private async void QueueReferenceAutosave(){if(SelectedShot is not{} shot||SelectedReference is not{} reference)return;try{var updatedReference=reference with{Note=_referenceNote};var updated=shot with{References=shot.References.Select(item=>item.ReferenceId==reference.ReferenceId?updatedReference:item).ToArray(),UpdatedAt=DateTimeOffset.UtcNow};await _shots.SaveAsync(updated);ReplaceShot(updated);SelectedReference=References.First(item=>item.ReferenceId==reference.ReferenceId);}catch{SaveStatus="保存失败，请稍后重试";}}
    private async Task EnterTetherAsync(){if(ProjectId is not Guid project)return;var context=await new PlanningExecutionContextService(_shots,_planning,_visuals,_looks).BuildAsync(project);EnterTetherRequested?.Invoke(this,context);}
    private void UpdateSummary(Func<PlanningSummary,PlanningSummary> update){if(_state is null)return;_state=_state with{Summary=update(_state.Summary??new())};foreach(var name in new[]{nameof(ShootGoal),nameof(Keywords),nameof(ClientRequirements),nameof(MustCapture),nameof(PlanningNotes),nameof(OutputPurpose)})OnPropertyChanged(name);QueueAutosave();}
    private async void QueueAutosave(){_autosave?.Cancel();_autosave?.Dispose();_autosave=new();var token=_autosave.Token;SaveStatus="正在保存…";try{await Task.Delay(350,token);if(_state is null)return;await _planning.SaveAsync(_state,token);_state=await _planning.LoadAsync(_state.ProjectId,token);SaveStatus="已保存";}catch(OperationCanceledException){}catch{SaveStatus="保存失败，请稍后重试";}}
    private bool FilterShot(object value)=>value is ProjectShot shot&&(_filter is null||shot.Status==_filter)&&(string.IsNullOrWhiteSpace(_searchText)||shot.Name.Contains(_searchText,StringComparison.CurrentCultureIgnoreCase)||(shot.Notes?.Contains(_searchText,StringComparison.CurrentCultureIgnoreCase)??false));
    private void ReplaceShot(ProjectShot shot){var index=Shots.ToList().FindIndex(item=>item.ShotId==shot.ShotId);if(index>=0)Shots[index]=shot;_selectedShot=shot;OnPropertyChanged(nameof(SelectedShot));RefreshReferences();NotifyAll();}
    private void RefreshReferences(){References.Clear();if(SelectedShot is not null)foreach(var reference in SelectedShot.References)References.Add(reference);SelectedReference=References.FirstOrDefault();}
    private void NotifyAll(){foreach(var name in new[]{nameof(ProjectId),nameof(ProjectName),nameof(BookingDate),nameof(Location),nameof(ProgressText),nameof(EstimatedTimeText),nameof(HasProject),nameof(HasShots),nameof(ShotHeading),nameof(InspectorHeading)})OnPropertyChanged(name);ShotsView.Refresh();}
}

public sealed record ProjectPaletteColorItem(string Hex,double Weight);
public sealed record ShotReferenceKindOption(ShotReferenceKind Kind,string Label)
{
    public static IReadOnlyList<ShotReferenceKindOption> All { get; } =
    [new(ShotReferenceKind.Lighting,"灯位"),new(ShotReferenceKind.Pose,"姿势"),new(ShotReferenceKind.Storyboard,"分镜"),new(ShotReferenceKind.Styling,"造型"),new(ShotReferenceKind.General,"普通参考")];
}
