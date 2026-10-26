using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    public bool R53Professional{get=>Layout.R53Professional;set{if(Layout.R53Professional==value)return;Layout=Layout with{R53Professional=value};OnPropertyChanged();ScheduleR53Autosave();}}
    public IReadOnlyList<StudioEditVersion> R53Variants=>ActiveTarget?.EditVersions.Where(v=>!v.Archived).ToArray()??[];
    public Guid? R53MainVariantId=>ActiveTarget?.EditVersions.FirstOrDefault(v=>v.IsMain&&!v.Archived)?.Id;
    public int R53VariantRating
    {get=>ActiveTarget?.EditVersions.FirstOrDefault(v=>v.Id==ActiveTarget.ActiveEditVersionId)?.Rating??0;set{if(ActiveTarget is not {} target)return;CaptureEditVersion(target);var i=target.EditVersions.ToList().FindIndex(v=>v.Id==target.ActiveEditVersionId);if(i>=0)target.EditVersions[i]=target.EditVersions[i] with{Rating=Math.Clamp(value,0,5)};NotifyR53Variants();ScheduleR53Autosave();}}
    public string R53VariantColorLabel
    {get=>ActiveTarget?.EditVersions.FirstOrDefault(v=>v.Id==ActiveTarget.ActiveEditVersionId)?.ColorLabel??"无";set{if(ActiveTarget is not {} target)return;CaptureEditVersion(target);var i=target.EditVersions.ToList().FindIndex(v=>v.Id==target.ActiveEditVersionId);if(i>=0)target.EditVersions[i]=target.EditVersions[i] with{ColorLabel=value is "红" or "橙" or "黄" or "绿" or "蓝" or "紫"?value:null};NotifyR53Variants();ScheduleR53Autosave();}}
    public string R53VariantStatus=>ActiveTarget is {} t?$"变体 {Array.FindIndex(R53Variants.ToArray(),v=>v.Id==t.ActiveEditVersionId)+1}/{R53Variants.Count} · {t.EditVersions.FirstOrDefault(v=>v.Id==t.ActiveEditVersionId)?.Name} · 主变体 {t.EditVersions.FirstOrDefault(v=>v.IsMain&&!v.Archived)?.Name}":"未选择照片";
    private readonly Dictionary<Guid,Stack<StudioEditVersion>> _r53Deleted=[];
    private void EnsureR53Variants(ReferenceTargetItem target)
    {
        if(target.EditVersions.All(v=>!v.IsOriginal))
        {
            var bytes=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Guangjing.Original.v1:"+target.Id));
            var original=new StudioEditVersion(new Guid(bytes.AsSpan(0,16)),"原始",DateTimeOffset.UtcNow,target.LibraryId,target.AssetId,target.SourceHash,null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto){IsOriginal=true};
            target.EditVersions.Insert(0,original);
        }
        if(target.EditVersions.All(v=>!v.IsMain||v.Archived))
        {var index=target.EditVersions.ToList().FindIndex(v=>v.Id==target.ActiveEditVersionId&&!v.Archived);if(index<0)index=0;target.EditVersions[index]=target.EditVersions[index] with{IsMain=true};}
        if(ReferenceEquals(target,ActiveTarget))Editor.ReadOnlyTarget=target.EditVersions.FirstOrDefault(v=>v.Id==target.ActiveEditVersionId)?.IsOriginal==true;
        NotifyR53Variants();
    }
    private readonly System.Windows.Threading.DispatcherTimer _r53Autosave=new(){Interval=TimeSpan.FromMilliseconds(900)};
    public string R53SaveStatus{get;private set;}="未保存项目";
    private bool _r53Autosaving;
    private long _r53EditRevision;
    private long _r53SavedRevision;
    internal void NotifyR53Edit()=>ScheduleR53Autosave();
    private void InitializeR53Autosave()
    {
        _r53Autosave.Tick+=async(_,_)=>
        {
            if(_r53Autosaving||IsLoading||IsExporting||IsInstantGenerating)return;_r53Autosave.Stop();
            if(SessionPath is null)return;_r53Autosaving=true;R53SaveStatus="正在保存…";OnPropertyChanged(nameof(R53SaveStatus));
            try{await SaveSessionAsync(SessionPath);}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException){System.Diagnostics.Trace.TraceError("R53 autosave: "+e);R53SaveStatus="保存失败 · 点击保存重试";}
            finally{_r53Autosaving=false;OnPropertyChanged(nameof(R53SaveStatus));}
        };
        Editor.PropertyChanged+=(_,e)=>{if(!IsLoading&&!_applyingActiveSnapshot&&e.PropertyName is nameof(Editor.AdjustmentStack) or nameof(Editor.SelectedLook) or nameof(Editor.FilmSettings))ScheduleR53Autosave();};
    }
    private void ScheduleR53Autosave()
    {
        if(_disposed)return;_r53EditRevision++;R53SaveStatus=SessionPath is null?"未保存 · 首次保存选择项目位置":"待保存";OnPropertyChanged(nameof(R53SaveStatus));
        if(SessionPath is not null){_r53Autosave.Stop();_r53Autosave.Start();}
    }
    /// <summary>Called by the real shell close path. A failed save keeps the window open.</summary>
    public async Task<bool> FlushR53BeforeCloseAsync()
    {
        _r53Autosave.Stop();
        if (Targets.Count == 0) return true;
        if (IsLoading || IsExporting || IsInstantGenerating)
        {
            R53SaveStatus = "请等待当前任务完成后退出";
            OnPropertyChanged(nameof(R53SaveStatus));
            return false;
        }
        var path = SessionPath;
        if (path is null)
        {
            await SaveSessionCommand.ExecuteAsync(null);
            if (SessionPath is null)
                return _dialogs.Confirm("暗室工作尚未保存。放弃本次未保存修改并退出？源照片不会删除。", "退出光镜");
            return true;
        }
        try
        {
            await SaveSessionAsync(path);
            // If editing resumed while IO was pending, do not close over a newer edit.
            if (_r53SavedRevision != _r53EditRevision) return false;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        {
            System.Diagnostics.Trace.TraceError("R53 close save: " + error);
            R53SaveStatus = "保存失败 · 窗口保持打开，请重试保存";
            OnPropertyChanged(nameof(R53SaveStatus));
            return false;
        }
    }
    private void NotifyR53Variants(){Editor.ReadOnlyTarget=ActiveTarget?.EditVersions.FirstOrDefault(v=>v.Id==ActiveTarget.ActiveEditVersionId)?.IsOriginal==true;OnPropertyChanged(nameof(R53Variants));OnPropertyChanged(nameof(R53VariantStatus));OnPropertyChanged(nameof(R53MainVariantId));OnPropertyChanged(nameof(R53VariantRating));OnPropertyChanged(nameof(R53VariantColorLabel));}
    public async Task CreateR53VariantAsync(bool fromOriginal)
    {
        if(ActiveTarget is not {} target||IsLoading||IsExporting||IsInstantGenerating)return;
        Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);EnsureR53Variants(target);
        var source=fromOriginal?target.EditVersions.First(v=>v.IsOriginal):target.EditVersions.Single(v=>v.Id==target.ActiveEditVersionId);
        var next=source with{Id=Guid.NewGuid(),Name=fromOriginal?"原始新建 "+R53Variants.Count:"副本 "+R53Variants.Count,IsOriginal=false,IsMain=false,MainExplicit=false,Archived=false,CreatedAt=DateTimeOffset.UtcNow,OriginVersionId=source.Id,Stack=source.Stack?.DeepClone()};
        target.EditVersions.Add(next);await SwitchEditVersionAsync(next.Id);NotifyR53Variants();ScheduleR53Autosave();
    }
    public void SetR53MainVariant()
    {
        if(ActiveTarget is not {} target||IsLoading||IsExporting||IsInstantGenerating)return;
        CaptureEditVersion(target);EnsureR53Variants(target);
        for(int i=0;i<target.EditVersions.Count;i++)target.EditVersions[i]=target.EditVersions[i] with{IsMain=target.EditVersions[i].Id==target.ActiveEditVersionId,MainExplicit=target.EditVersions[i].Id==target.ActiveEditVersionId};
        NotifyR53Variants();ScheduleR53Autosave();StatusText="主变体已设置；保存项目后保留。";
    }
    public async Task DeleteR53VariantAsync()
    {
        if(ActiveTarget is not {} target||IsLoading||IsExporting||IsInstantGenerating)return;
        Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);EnsureR53Variants(target);var active=target.EditVersions.Single(v=>v.Id==target.ActiveEditVersionId);
        if(active.IsOriginal){StatusText="原始变体不可删除；源照片保持只读。";return;}
        if(!_dialogs.Confirm("删除变体“"+active.Name+"”？可撤销；不删除源照片文件。已有项目引用会显示该变体不可用，不替换为其他变体。","删除变体"))return;
        var next=target.EditVersions.First(v=>v.IsOriginal&&!v.Archived);await SwitchEditVersionAsync(next.Id);
        if(!ReferenceEquals(target,ActiveTarget)||target.ActiveEditVersionId!=next.Id)return;
        if(!_r53Deleted.TryGetValue(target.Id,out var undo))_r53Deleted[target.Id]=undo=new();undo.Push(active);
        var index=target.EditVersions.ToList().FindIndex(v=>v.Id==active.Id);target.EditVersions[index]=active with{Archived=true,IsMain=false,MainExplicit=false};
        if(active.IsMain){var n=target.EditVersions.ToList().FindIndex(v=>v.Id==next.Id);target.EditVersions[n]=target.EditVersions[n] with{IsMain=true,MainExplicit=active.MainExplicit};}
        NotifyR53Variants();ScheduleR53Autosave();StatusText="变体已删除，可撤销；源照片未修改。";
    }
    public async Task UndoR53VariantDeleteAsync()
    {
        if(ActiveTarget is not {} target||IsLoading||IsExporting||IsInstantGenerating||!_r53Deleted.TryGetValue(target.Id,out var history)||history.Count==0)return;
        var deleted=history.Pop();var index=target.EditVersions.ToList().FindIndex(v=>v.Id==deleted.Id);
        if(index<0){StatusText="删除记录已不属于当前项目，未替换其他变体。";return;}
        // Restore this one identity only. Later edits/new variants are never rolled back.
        if(deleted.IsMain)for(int i=0;i<target.EditVersions.Count;i++)target.EditVersions[i]=target.EditVersions[i] with{IsMain=false,MainExplicit=false};
        target.EditVersions[index]=deleted;await SwitchEditVersionAsync(deleted.Id);NotifyR53Variants();ScheduleR53Autosave();
    }
    private StudioEditVersion MainOutputVersion(ReferenceTargetItem target)
    {
        CaptureEditVersion(target);EnsureR53Variants(target);
        // Old projects have no explicit main choice. Their historical output remains the
        // selected valid edit, rather than silently exporting a newly migrated empty original.
        if(!target.EditVersions.Any(v=>v.MainExplicit&&!v.Archived))
            for(int i=0;i<target.EditVersions.Count;i++)target.EditVersions[i]=target.EditVersions[i] with{IsMain=target.EditVersions[i].Id==target.ActiveEditVersionId};
        return target.EditVersions.Single(v=>v.IsMain&&!v.Archived);
    }
    public async Task PrepareR53MainExportAsync()
    {
        if(ActiveTarget is not {} target)return;Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);EnsureR53Variants(target);
        var main=MainOutputVersion(target);await SwitchEditVersionAsync(main.Id);NotifyR53Variants();
    }
}
