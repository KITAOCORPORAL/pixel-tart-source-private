using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class PlanningCenterViewModel
{
    private readonly Stack<(Guid Project, PlanningDocument Document, ProjectShot[] Shots)> _planningUndo = new();
    private readonly Stack<(Guid Project, PlanningDocument Document, ProjectShot[] Shots)> _planningRedo = new();
    public PlanningResources ResourceCatalog => _document.Resources;
    public Func<Guid, Task<IReadOnlyList<FinanceTransaction>>>? ReadPlanningExpenses { get; set; }
    public IReadOnlyList<FinanceTransaction> PlanningExpenses { get; private set; } = [];
    public string ResourceStatus { get; private set; } = "";
    private T DetachPlanning<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private void CapturePlanningUndo()
    {
        _planningRedo.Clear();
        if (ProjectId is Guid id) _planningUndo.Push((id, PlanningImageStorage.BindDocument(DetachPlanning(_document),_planning.StorageRoot), DetachPlanning(Shots.Select(s => ShotDraft(s.ShotId)).ToArray())));
    }
    public async Task<bool> SaveResourceAsync(PlanningResource resource)
    {
        if (!await FlushAsync()) return false;
        try
        {
            resource.Validate();
            if (resource.ShotIds.Any(id => !Shots.Any(s => s.ShotId == id))) throw new InvalidDataException("关联镜头不在当前有效清单。");
            CapturePlanningUndo(); UpdateDocument(_document with { Resources = ResourceCatalog.Upsert(resource) });
            return await FlushAsync();
        }
        catch (Exception e) when(e is ArgumentException or InvalidDataException) { ResourceStatus = e.Message; OnPropertyChanged(nameof(ResourceStatus)); return false; }
    }
    public Task<bool> ArchiveResourceAsync(Guid id, bool archived) => SaveResourceAsync(ResourceCatalog.Items.First(i => i.Id == id) with { Archived = archived });
    public Task<bool> CopyResourceAsync(Guid id) => SaveResourceAsync(ResourceCatalog.Items.First(i => i.Id == id) with { Id = Guid.NewGuid(), Name = ResourceCatalog.Items.First(i => i.Id == id).Name + " 副本", Archived = false });
    private async Task<bool> FlushProgrammaticPlanningAsync()
    {
        var capture=CapturePendingEditorDraft;CapturePendingEditorDraft=null;
        try{return await FlushAsync();}finally{CapturePendingEditorDraft=capture;}
    }
    public async Task<bool> UndoPlanningAsync()
    {
        if (_planningUndo.Count == 0 || _planningUndo.Peek().Project != ProjectId || !await FlushAsync()) return false;
        _planningRedo.Push((ProjectId!.Value,PlanningImageStorage.BindDocument(DetachPlanning(_document),_planning.StorageRoot),DetachPlanning(Shots.Select(s=>ShotDraft(s.ShotId)).ToArray())));
        var snapshot = _planningUndo.Pop(); EditorGeneration++; UpdateDocument(snapshot.Document);
        foreach (var shot in snapshot.Shots) StageShot(shot);
        var saved = await FlushProgrammaticPlanningAsync(); PresentationChanged(); return saved;
    }
    public async Task<bool> RedoPlanningAsync()
    {
        if(_planningRedo.Count==0||_planningRedo.Peek().Project!=ProjectId||!await FlushAsync())return false;
        _planningUndo.Push((ProjectId!.Value,PlanningImageStorage.BindDocument(DetachPlanning(_document),_planning.StorageRoot),DetachPlanning(Shots.Select(s=>ShotDraft(s.ShotId)).ToArray())));
        var snapshot=_planningRedo.Pop();EditorGeneration++;UpdateDocument(snapshot.Document);foreach(var shot in snapshot.Shots)StageShot(shot);
        var saved=await FlushProgrammaticPlanningAsync();PresentationChanged();return saved;
    }
    public async Task<bool> ImportPlanningGearAsync(IEnumerable<PlanningGearItem> source)
    {
        if (!await FlushAsync()) return false;
        var next = ResourceCatalog.ImportGear(source); CapturePlanningUndo(); UpdateDocument(_document with { Resources = next }); return await FlushAsync();
    }
    private Guid? _planningExpenseProject;
    public async Task RefreshPlanningExpensesAsync()
    {
        if (ProjectId is not Guid id || ReadPlanningExpenses is null) { ResourceStatus = "收支只读来源未连接"; return; }
        if(_planningExpenseProject!=id){PlanningExpenses=[];_planningExpenseProject=id;}
        try
        {
            var values = await ReadPlanningExpenses(id);
            if (ProjectId != id) return;
            PlanningExpenses = values.Where(v => v.ProjectId == id && v.Kind == FinanceTransactionKind.Expense).DistinctBy(v => v.Id).ToArray();
            ResourceStatus = "按项目只读读取支出；币种分开，不修改账本。";
        }
        catch(Exception e) when(e is IOException or InvalidOperationException) { ResourceStatus = "账本读取失败，保留上次结果：" + e.Message; }
        OnPropertyChanged(nameof(ResourceStatus)); OnPropertyChanged(nameof(PlanningExpenses));
    }
}
