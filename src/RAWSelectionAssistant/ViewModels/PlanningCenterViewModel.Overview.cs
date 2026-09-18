using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class PlanningCenterViewModel
{
    private bool _isOverview = true;
    private string _projectSearch = "";
    private string _overviewFilter = "最近策划";
    private string _overviewStatus = "选择项目开始策划，或新建策划。";
    private string _newPlanningName = "";
    public event EventHandler? ProjectContextChanged;
    public ObservableCollection<PlanningProjectCard> PlanningProjects { get; } = [];
    public ICollectionView PlanningProjectsView { get; private set; } = null!;
    public bool IsOverview { get => _isOverview; private set => SetProperty(ref _isOverview, value); }
    public string ProjectSearch { get => _projectSearch; set { if (SetProperty(ref _projectSearch, value)) PlanningProjectsView.Refresh(); } }
    public string OverviewFilter { get => _overviewFilter; set { if (SetProperty(ref _overviewFilter, value)) { PlanningProjectsView.SortDescriptions.Clear(); PlanningProjectsView.SortDescriptions.Add(new(value == "即将拍摄" ? nameof(PlanningProjectCard.ShootAt) : nameof(PlanningProjectCard.UpdatedAt), value == "即将拍摄" ? ListSortDirection.Ascending : ListSortDirection.Descending)); PlanningProjectsView.Refresh(); } } }
    public IReadOnlyList<string> OverviewFilters { get; } = ["最近策划", "进行中的项目", "即将拍摄", "最近修改"];
    public string OverviewStatus { get => _overviewStatus; private set => SetProperty(ref _overviewStatus, value); }
    public string NewPlanningName { get => _newPlanningName; set => SetProperty(ref _newPlanningName, value); }
    public AsyncRelayCommand ShowOverviewCommand { get; private set; } = null!;
    public AsyncRelayCommand OpenPlanningProjectCommand { get; private set; } = null!;
    public AsyncRelayCommand CreatePlanningCommand { get; private set; } = null!;

    private void InitializeOverview()
    {
        PlanningProjectsView = CollectionViewSource.GetDefaultView(PlanningProjects);
        PlanningProjectsView.SortDescriptions.Add(new(nameof(PlanningProjectCard.UpdatedAt), ListSortDirection.Descending));
        PlanningProjectsView.Filter = value => value is PlanningProjectCard card
            && (string.IsNullOrWhiteSpace(ProjectSearch) || card.Name.Contains(ProjectSearch, StringComparison.CurrentCultureIgnoreCase))
            && (OverviewFilter != "进行中的项目" || card.Completed < card.Total || card.Total == 0)
            && (OverviewFilter != "即将拍摄" || card.ShootAt >= DateTimeOffset.Now);
        ShowOverviewCommand = new AsyncRelayCommand(_ => ShowOverviewAsync());
        OpenPlanningProjectCommand = new AsyncRelayCommand(value => value is PlanningProjectCard card ? OpenProjectSafelyAsync(card.ProjectId) : Task.CompletedTask);
        CreatePlanningCommand = new AsyncRelayCommand(_ => CreatePlanningAsync());
    }

    public async Task ShowOverviewAsync(CancellationToken token = default)
    {
        IsOverview = true;
        try
        {
            var projects = await _projects.ListAsync(token);
            PlanningProjects.Clear();
            foreach (var project in projects.OrderByDescending(item => item.UpdatedAt))
            {
                try
                {
                    var planning = await _planning.LoadAsync(project.Id, token);
                    var shots = (await _shots.LoadAsync(project.Id, token)).Shots.Where(shot => !shot.IsArchived).ToArray();
                    var booking = planning.BookingId is Guid id ? await _bookings.GetAsync(id, false, token) : null;
                    var cover = shots.SelectMany(shot => shot.References).Select(reference => reference.ExternalReference).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
                    PlanningProjects.Add(new(project.Id, project.Name, cover, booking?.StartAtUtc, shots.Count(shot => shot.Status == ProjectShotStatus.Completed), shots.Length, shots.Select(shot => shot.UpdatedAt).Append(project.UpdatedAt).Max()));
                }
                catch (Exception error) when (IsRecoverableProjectError(error))
                {
                    new FileLogService().Error("策划项目摘要读取失败，保留项目入口。", error);
                    PlanningProjects.Add(new(project.Id, project.Name, null, null, 0, 0, project.UpdatedAt));
                }
            }
            OverviewStatus = PlanningProjects.Count == 0 ? "还没有策划项目。输入名称即可新建策划。" : "选择项目继续策划；原有拍摄清单和参考资料保持不变。";
        }
        catch (Exception error) when (IsRecoverableProjectError(error))
        {
            new FileLogService().Error("策划总览读取失败。", error);
            OverviewStatus = "暂时无法读取项目，请稍后重试。原有资料未被修改。";
        }
    }

    public async Task OpenProjectSafelyAsync(Guid projectId, Guid? bookingId = null, Guid? shotId = null)
    {
        try
        {
            await LoadAsync(projectId, bookingId);
            if (shotId is Guid id) SelectedShot = Shots.FirstOrDefault(shot => shot.ShotId == id) ?? SelectedShot;
        }
        catch (Exception error) when (IsRecoverableProjectError(error) || error is InvalidOperationException)
        {
            new FileLogService().Error("上次策划无法恢复，返回策划总览。", error);
            _project = null; _state = null; _booking = null; Shots.Clear(); SelectedShot = null; NotifyAll();
            ProjectContextChanged?.Invoke(this, EventArgs.Empty);
            await ShowOverviewAsync();
            OverviewStatus = "上次策划暂时无法打开，已返回总览。原有资料已保留。";
        }
    }

    public Task EnterWorkspaceAsync(Guid? lastProject = null, Guid? lastShot = null) =>
        HasProject ? Task.CompletedTask : lastProject is Guid id ? OpenProjectSafelyAsync(id, shotId: lastShot) : ShowOverviewAsync();

    private async Task CreatePlanningAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPlanningName)) { OverviewStatus = "请先输入策划项目名称。"; return; }
        var project = new PhotoProjectRecord { Id = Guid.NewGuid(), Name = NewPlanningName.Trim(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        try { await _projects.UpsertAsync(project); NewPlanningName = ""; await OpenProjectSafelyAsync(project.Id); }
        catch (Exception error) when (IsRecoverableProjectError(error)) { new FileLogService().Error("新建策划失败。", error); OverviewStatus = "新建策划失败，请稍后重试。"; }
    }

    private static bool IsRecoverableProjectError(Exception error) => error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException or ArgumentException;
}

public sealed record PlanningProjectCard(Guid ProjectId, string Name, string? Cover, DateTimeOffset? ShootAt, int Completed, int Total, DateTimeOffset UpdatedAt)
{
    public string DateText => ShootAt?.ToLocalTime().ToString("MM月dd日") ?? "尚未安排拍摄";
    public string Progress => $"{Completed} / {Total} 已拍";
}
