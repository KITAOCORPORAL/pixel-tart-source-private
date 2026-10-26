using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class PlanningCenterViewModel
{
    private CancellationTokenSource? _packageCancellation;
    private bool _isPackageTaskRunning;
    public bool IsPackageTaskRunning{get=>_isPackageTaskRunning;private set=>SetProperty(ref _isPackageTaskRunning,value);}
    private CancellationToken BeginPackageTask(){_packageCancellation?.Dispose();_packageCancellation=new();IsPackageTaskRunning=true;return _packageCancellation.Token;}
    public RelayCommand CancelProjectPackageCommand {get;private set;}=null!;
    public AsyncRelayCommand ExportProjectPackageCommand {get;private set;}=null!;
    public AsyncRelayCommand ImportCurrentProjectPackageCommand {get;private set;}=null!;
    public AsyncRelayCommand RecoverCurrentProjectImportsCommand {get;private set;}=null!;
    public AsyncRelayCommand ImportProjectPackageCommand {get;private set;}=null!;
    public AsyncRelayCommand ShowCopyRecoveryCommand {get;private set;}=null!;
    public AsyncRelayCommand OpenImportedProjectCommand {get;private set;}=null!;
    public string PackageStatus {get;private set;}="项目包包含策划/镜头及已校验自有副本，不是完整跨机便携。";
    public string? ImportedPackageRoot {get;private set;}
    private void InitializeProjectPackages()
    {
        InitializeCurrentProjectImports();
        CancelProjectPackageCommand=new RelayCommand(_=>{_packageCancellation?.Cancel();PackageStatus="已请求取消项目包任务；已完成提交的项目不撤回";OnPropertyChanged(nameof(PackageStatus));});
        ShowCopyRecoveryCommand=new AsyncRelayCommand(async _=>
        {
            if(ProjectId is not Guid id||!await FlushAsync())return;
            try{await _planning.LoadAsync(id);var items=_planning.LastRecovery;_dialogs.ShowInfo(items.Count==0?"本项目没有需要处理的有归属过期暂存。未知文件不自动清理。":string.Join("\n",items.Select(i=>i.Action+"："+i.Path+" — "+i.Reason))+"\n审计记录限定保存在本项目.staging目录，不处理其他目录。");}
            catch(Exception e)when(PackageError(e)){_dialogs.ShowError("恢复检查未完成，文件保留："+e.Message);}
        });
        OpenImportedProjectCommand=new AsyncRelayCommand(async _=>
        {
            try{var selected=_dialogs.ChooseFolder("打开已导入项目数据根（含project.db和import-result.json）");if(selected is null)return;var result=Path.Combine(selected,"import-result.json");PlanningImageStorage.RejectLinks(selected,result);using var json=System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(result));var id=json.RootElement.GetProperty("project").GetGuid();await OpenImportedProjectRootAsync(selected,id);_dialogs.ShowInfo("已打开独立导入项目；账本/外部服务未连接。\n"+selected);}
            catch(Exception e)when(PackageError(e)){_dialogs.ShowError("未打开导入项目："+e.Message);}
        });
        ExportProjectPackageCommand=new AsyncRelayCommand(async _=>
        {
            if(ProjectId is not Guid id||!await FlushAsync())return;
            try
            {
                var state=await _planning.LoadAsync(id);var shots=await _shots.LoadAsync(id);var excluded=PlanningProjectPackage.Dependencies(state,shots);
                if(!_dialogs.Confirm("导出当前策划、镜头及已验证自有图片/附件。以下逐项依赖不包含：\n"+string.Join("\n",excluded)+"\n不宣称完整跨机便携。继续？","策划项目包范围"))return;
                var path=_dialogs.ChooseSaveFile("导出策划项目包（非完整跨机便携）","策划项目包|*.ptplan", ".ptplan",ProjectName+".ptplan");if(path is null)return;var taskToken=BeginPackageTask();try{await ExportProjectPackageAsync(path,taskToken);_dialogs.ShowInfo(PackageStatus);}finally{IsPackageTaskRunning=false;}
            }
            catch(Exception e)when(PackageError(e)){new RAWSelectionAssistant.Core.Services.FileLogService().Error("项目包导出失败",e);PackageStatus="导出失败，请检查位置后重试。";_dialogs.ShowError(PackageStatus);}
        });
        ImportProjectPackageCommand=new AsyncRelayCommand(async _=>
        {
            if(!await FlushAsync())return;
            try
            {
                var path=_dialogs.ChooseFiles("导入策划项目包","策划项目包|*.ptplan",false).FirstOrDefault();if(path is null)return;
                var taskToken=BeginPackageTask();var validated=await Task.Run(()=>PlanningProjectPackage.Validate(path,taskToken));
                if(!_dialogs.Confirm("项目："+validated.Manifest.Name+"\n项目ID："+validated.Manifest.ProjectId+"\n将建立独立数据根，不覆盖现有项目。未包含：\n"+string.Join("\n",PlanningProjectPackage.Dependencies(validated.State,validated.Shots))+"\n同ID目录冲突会拒绝，同名不同ID独立保留。继续？","导入策划项目包"))return;
                var parent=_dialogs.ChooseFolder("选择导入位置（每项目独立新数据根，不覆盖现有目录）");if(parent is null)return;
                var result=await ImportProjectPackageAsync(path,parent,taskToken);_dialogs.ShowInfo("导入完成："+result.DataRoot+"\n未包含依赖：\n"+string.Join("\n",result.ExcludedDependencies));
            }
            catch(Exception e)when(PackageError(e)){PackageStatus="导入失败，原项目未变。请检查文件或重试。";new RAWSelectionAssistant.Core.Services.FileLogService().Error("项目包导入失败",e);_dialogs.ShowError(PackageStatus);}
            finally{IsPackageTaskRunning=false;}
        });
    }
    private static bool PackageError(Exception e)=>e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Text.Json.JsonException or InvalidOperationException or OperationCanceledException;
    public async Task ExportProjectPackageAsync(string path,CancellationToken token=default)
    {
        if(ProjectId is not Guid id||_project is null||!await FlushAsync(token))throw new InvalidOperationException("先选择并保存策划项目。");
        var state=await _planning.LoadAsync(id,token);var shots=await _shots.LoadAsync(id,token);await Task.Run(()=>PlanningProjectPackage.ExportAsync(path,_project,state,shots,_planning.StorageRoot,token),token);
        PackageStatus="项目包已导出；策划/镜头/自有副本已含。未包含：\n"+string.Join("\n",PlanningProjectPackage.Dependencies(state,shots));OnPropertyChanged(nameof(PackageStatus));
    }
    public async Task<PlanningPackageResult> ImportProjectPackageAsync(string path,string parent,CancellationToken token=default)
    {
        if(!await FlushAsync(token))throw new InvalidOperationException("当前项目尚未保存。");
        var result=await Task.Run(()=>PlanningProjectPackage.ImportAsync(path,parent,token),token);await OpenImportedProjectRootAsync(result.DataRoot,result.ProjectId,CancellationToken.None);
        PackageStatus="已导入独立项目根："+result.DataRoot+"\n未包含：\n"+string.Join("\n",result.ExcludedDependencies);OnPropertyChanged(nameof(PackageStatus));return result;
    }
    public async Task OpenImportedProjectRootAsync(string root,Guid id,CancellationToken token=default)
    {
        if(!await FlushAsync(token))throw new InvalidOperationException("先保存当前策划。");
        root=Path.GetFullPath(root);PlanningImageStorage.RejectLinks(Path.GetPathRoot(root)!,root);
        var databasePath=Path.Combine(root,"project.db");if(!File.Exists(databasePath))throw new InvalidDataException("目标不是已导入项目数据根，未建立空数据库");PlanningImageStorage.RejectLinks(root,databasePath);
        var planning=new PlanningProjectStore(Path.Combine(root,"planning"));var planned=await planning.LoadAsync(id,token);if(planned.Document is null)throw new InvalidDataException("目标不含策划文档");await new ProjectShotStore(Path.Combine(root,"shots")).LoadAsync(id,token);
        var db=new PixelTartDatabase(databasePath);var projects=new SqliteProjectRepository(db);if(!(await projects.ListAsync(token)).Any(p=>p.Id==id))throw new InvalidDataException("导入根不含此项目");
        _planningUndo.Clear();_planningRedo.Clear();SelectedDocumentReference=null;SelectedSourceCandidate=null;SourceCandidates.Clear();
        _project=null;_state=null;_booking=null;_projects=projects;var bookings=new SqliteShootBookingRepository(db);_bookings=new ShootBookingService(bookings,new BookingConflictDetector(bookings));
        _shots=new(Path.Combine(root,"shots"));_planning=new(Path.Combine(root,"planning"));_shots.BindPlanningLifecycle(_planning);_visuals=new(Path.Combine(root,"visuals"));_looks=new(Path.Combine(root,"looks"));_canvases=new(Path.Combine(root,"canvas"));_inspiration=null;ReadPlanningExpenses=null;ReferencePathResolver=null;ReferenceSourceLoader=null;ImportedPackageRoot=root;
        await LoadAsync(id,token:token);PackageStatus="当前为独立导入项目："+root+"；外部账本/库/服务未连接";OnPropertyChanged(nameof(PackageStatus));IsOverview=false;await RefreshPlanningListAsync(token);PresentationChanged();
    }
}
