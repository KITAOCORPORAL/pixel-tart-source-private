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
    private string _overviewFilter = "全部";
    private string _overviewStatus = "选择项目开始策划，或新建策划。";
    private string _newPlanningName = "";
    public event EventHandler? ProjectContextChanged;
    public ObservableCollection<PlanningProjectCard> PlanningProjects { get; } = [];
    public ICollectionView PlanningProjectsView { get; private set; } = null!;
    public bool IsOverview { get => _isOverview; private set => SetProperty(ref _isOverview, value); }
    public string ProjectSearch { get => _projectSearch; set { if (SetProperty(ref _projectSearch, value)) PlanningProjectsView.Refresh(); } }
    public string OverviewFilter { get => _overviewFilter; set { if (SetProperty(ref _overviewFilter, value)) { PlanningProjectsView.SortDescriptions.Clear(); PlanningProjectsView.SortDescriptions.Add(new(value == "即将拍摄" ? nameof(PlanningProjectCard.ShootAt) : nameof(PlanningProjectCard.UpdatedAt), value == "即将拍摄" ? ListSortDirection.Ascending : ListSortDirection.Descending)); PlanningProjectsView.Refresh(); } } }
    public IReadOnlyList<string> OverviewFilters { get; } = ["全部", "进行中", "草稿", "已完成"];
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
            && (string.IsNullOrWhiteSpace(ProjectSearch) || (card.Name + " " + card.People + " " + card.DateText + " " + card.Location).Contains(ProjectSearch, StringComparison.CurrentCultureIgnoreCase))
            && (OverviewFilter == "全部" || card.Status == OverviewFilter);
        ShowOverviewCommand = new AsyncRelayCommand(_ => ShowOverviewAsync());
        OpenPlanningProjectCommand = new AsyncRelayCommand(value => value is PlanningProjectCard card ? OpenProjectSafelyAsync(card.ProjectId) : Task.CompletedTask);
        CreatePlanningCommand = new AsyncRelayCommand(_ => CreatePlanningAsync());
    }

    public async Task ShowOverviewAsync(CancellationToken token = default)
    {
        if (!await FlushAsync()) return;
        IsOverview = true;
        IsPreviewMode = false;
        await RefreshPlanningListAsync(token);
        PresentationChanged();
    }

    public async Task RefreshPlanningListAsync(CancellationToken token = default)
    {
        try
        {
            var projects = await _projects.ListAsync(token);
            PlanningProjects.Clear();
            foreach (var project in projects.OrderByDescending(item => item.UpdatedAt))
            {
                try
                {
                    var shots = (await _shots.LoadAsync(project.Id, token)).Shots.Where(shot => !shot.IsArchived).ToArray();
                    if (!_planning.Exists(project.Id) && shots.Length == 0) continue;
                    var planning = await _planning.LoadAsync(project.Id, token);
                    var booking = planning.BookingId is Guid id ? await _bookings.GetAsync(id, false, token) : null;
                    var cover = shots.SelectMany(shot => shot.References).Select(reference => reference.ExternalReference).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
                    PlanningProjects.Add(new(project.Id, string.IsNullOrWhiteSpace(planning.Document?.Title) ? project.Name : planning.Document.Title, cover, booking?.StartAtUtc ?? planning.Document?.ShootDate, shots.Count(shot => shot.Status == ProjectShotStatus.Completed), shots.Length, planning.UpdatedAt ?? project.UpdatedAt,
                        booking?.Location ?? planning.Document?.Location ?? "地点待定", planning.Document?.People ?? booking?.ClientDisplayName ?? "", planning.Document?.Status ?? "草稿",
                        shots.SelectMany(shot => shot.References).Select(reference => reference.ReferenceId).Concat(planning.Document?.References.Select(reference => reference.Source.ReferenceId) ?? []).Distinct().Count()));
                }
                catch (Exception error) when (IsRecoverableProjectError(error))
                {
                    new FileLogService().Error("策划项目摘要读取失败，保留项目入口。", error);
                    PlanningProjects.Add(new(project.Id, project.Name, null, null, 0, 0, project.UpdatedAt));
                }
            }
            OverviewStatus = PlanningProjects.Count == 0 ? "还没有策划案。从一次拍摄开始，把灵感、镜头和执行资料整理到一起。" : "选择一个策划案开始";
        }
        catch (Exception error) when (IsRecoverableProjectError(error))
        {
            new FileLogService().Error("策划总览读取失败。", error);
            OverviewStatus = "暂时无法读取项目，请稍后重试。原有资料未被修改。";
        }
    }

    public async Task OpenProjectSafelyAsync(Guid projectId, Guid? bookingId = null, Guid? shotId = null)
    {
        if (!await FlushAsync()) return;
        try
        {
            await LoadAsync(projectId, bookingId);
            if (shotId is Guid id) { SelectedShot = Shots.FirstOrDefault(shot => shot.ShotId == id) ?? SelectedShot; ContentPage = "镜头清单"; }
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
        if (!await FlushAsync()) return;
        var project = NewLinkedProject ?? new PhotoProjectRecord { Id = Guid.NewGuid(), Name = NewPlanningName.Trim(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        try
        {
            if (NewLinkedProject is null) await _projects.UpsertAsync(project);
            if (_planning.Exists(project.Id)) { OverviewStatus = "该项目已有策划案，已打开原策划。"; await OpenProjectSafelyAsync(project.Id); IsCreateOpen = false; return; }
            await _planning.UpdateDocumentAsync(project.Id, new PlanningDocument { Title = NewPlanningName.Trim(), Location = NewLocation, ShootDate = NewShootDate is DateTime date ? new DateTimeOffset(date) : null }, new());
            NewPlanningName = ""; NewLinkedProject = null; NewLocation = ""; NewShootDate = null; IsCreateOpen = false;
            await OpenProjectSafelyAsync(project.Id);
        }
        catch (Exception error) when (IsRecoverableProjectError(error)) { new FileLogService().Error("新建策划失败。", error); OverviewStatus = "新建策划失败，请稍后重试。"; }
    }

    private static bool IsRecoverableProjectError(Exception error) => error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException or ArgumentException;
}

public sealed record PlanningProjectCard(Guid ProjectId, string Name, string? Cover, DateTimeOffset? ShootAt, int Completed, int Total, DateTimeOffset UpdatedAt,
    string Location = "地点待定", string People = "", string Status = "草稿", int ReferenceCount = 0)
{
    public string DateText => ShootAt?.ToLocalTime().ToString("MM月dd日") ?? "尚未安排拍摄";
    public string Progress => $"{Completed} / {Total} 已拍";
    public string Detail => $"{DateText} · {Location}";
    public string CountText => $"{Status}   参考图 {ReferenceCount} · 镜头 {Total}";
}
