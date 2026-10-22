using System.IO;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class PlanningCenterViewModel
{
    private bool IsPackageImported=>_document.Extensions?.ContainsKey("PackageImportExcludedDependencies")==true;
    private void InitializeCurrentProjectImports()
    {
        RecoverCurrentProjectImportsCommand=new AsyncRelayCommand(async _=>
        {
            try{var rows=await RecoverCurrentProjectImportsAsync();_dialogs.ShowInfo(rows.Count==0?"当前根无本功能待恢复暂存；其他目录未扫描。":string.Join("\n",rows.Select(x=>x.Status+"："+x.Stage+" — "+x.Detail)));}
            catch(Exception e)when(PackageError(e)){_dialogs.ShowError("恢复未完成，保留待审计："+e.Message);}
        });
        ImportCurrentProjectPackageCommand=new AsyncRelayCommand(async _=>
        {
            if(!await FlushAsync())return;
            try
            {
                var file=_dialogs.ChooseFiles("导入策划项目包到当前应用","策划项目包|*.ptplan",false).FirstOrDefault();if(file is null)return;_packageCancellation?.Dispose();_packageCancellation=new();var v=await Task.Run(()=>PlanningProjectPackage.Validate(file,_packageCancellation.Token));
                if(!_dialogs.Confirm("项目："+v.Manifest.Name+"\nID："+v.Manifest.ProjectId+"\n将写入数据库："+(_projects is SqliteProjectRepository sql?sql.PlanningImportDatabaseLocation:"当前项目数据库")+"\n策划："+_planning.StorageRoot+"\n镜头："+_shots.StorageRoot+"\n仅新增，同ID拒绝，同名不同ID分开保留。\n未包含：\n"+string.Join("\n",v.Manifest.ExcludedDependencies),"导入到当前应用"))return;
                var result=await ImportPackageToCurrentRootAsync(file,_packageCancellation.Token);_dialogs.ShowInfo("已加入普通策划项目列表："+result.Name+"\nID："+result.ProjectId+"\n未包含：\n"+string.Join("\n",result.ExcludedDependencies));
            }
            catch(Exception e)when(PackageError(e)||e is Microsoft.Data.Sqlite.SqliteException){_dialogs.ShowError("当前根导入未完成，原项目保持："+e.Message);}
        });
    }
    public Task<IReadOnlyList<PlanningImportRecovery>> RecoverCurrentProjectImportsAsync(CancellationToken token=default)=>_projects is IPlanningImportRepository db?PlanningCurrentRootImport.RecoverAsync(_planning,_shots,db,token):Task.FromResult<IReadOnlyList<PlanningImportRecovery>>([]);
    public async Task<PlanningCurrentImportResult> ImportPackageToCurrentRootAsync(string file,CancellationToken token=default)
    {
        if(!await FlushAsync(token))throw new IOException("当前策划未保存");if(_projects is not IPlanningImportRepository db)throw new NotSupportedException("当前项目仓储不支持严格导入事务");
        var result=await Task.Run(()=>PlanningCurrentRootImport.ImportAsync(file,_planning,_shots,db,token),token);await RefreshPlanningListAsync();await OpenProjectSafelyAsync(result.ProjectId);PackageStatus="已导入当前应用："+result.ProjectId;OnPropertyChanged(nameof(PackageStatus));return result;
    }
}
