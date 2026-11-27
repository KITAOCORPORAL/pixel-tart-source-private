#if PLANNING_REVIEW_PILOT
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using RAWSelectionAssistant.Services.Planning;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class PlanningCenterView
{
    private PlanningTeamShareClient? _teamShare;
    private PlanningCenterViewModel? _teamShareHeaderOwner;
    private PlanningTeamShareTarget? _teamShareHeaderTarget;
    private PlanningTeamShareBinding? _teamShareBinding;
    private DispatcherTimer? _teamShareFingerprintTimer;
    private long _teamShareWorkspaceEpoch;
    private long _teamShareFingerprintEpoch;
    private bool _teamShareBusy;
    private bool _teamShareLocalChanges;
    private string? _teamShareFeedback;

    partial void InitializeTeamShare()
    {
        _teamShare = PlanningTeamShareClient.TryCreate();
        if (_teamShare is null) return;

        PlanningTeamShareAction.Click += ShareToTeam_Click;
        _teamShareFingerprintTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _teamShareFingerprintTimer.Tick += async (_, _) => await RefreshTeamShareFingerprintAsync();
        DataContextChanged += (_, _) => _teamShareWorkspaceEpoch++;
        IsVisibleChanged += (_, _) =>
        {
            _teamShareWorkspaceEpoch++;
            UpdateTeamShareHeader();
        };
    }

    partial void TeamShareContextChanged(string? property)
    {
        if (_teamShare is null) return;
        if (property is nameof(PlanningCenterViewModel.IsOverview) or nameof(PlanningCenterViewModel.IsPlanningLoading)
            or nameof(PlanningCenterViewModel.IsPreviewMode) or nameof(PlanningCenterViewModel.ProjectId))
            _teamShareWorkspaceEpoch++;
        UpdateTeamShareHeader();
        if (string.IsNullOrEmpty(property) || property is nameof(PlanningCenterViewModel.SaveStatus)
            or nameof(PlanningCenterViewModel.SelectedShot) or nameof(PlanningCenterViewModel.DocumentTitle)
            or nameof(PlanningCenterViewModel.ReferenceImageEpoch))
            ScheduleTeamShareFingerprint();
    }

    partial void UpdateTeamShareHeader()
    {
        if (_teamShare is null) return;
        var owner = _vm;
        var target = reviewWeb is null && IsVisible ? owner?.GetTeamShareTarget() : null;
        if (!ReferenceEquals(owner, _teamShareHeaderOwner) || target != _teamShareHeaderTarget)
        {
            // A generation change refreshes the version comparison but a new plan
            // never inherits the previous plan's publication feedback.
            if (!ReferenceEquals(owner, _teamShareHeaderOwner) || target?.ProjectId != _teamShareHeaderTarget?.ProjectId
                || target?.DocumentId != _teamShareHeaderTarget?.DocumentId)
                _teamShareFeedback = null;
            _teamShareHeaderOwner = owner;
            _teamShareHeaderTarget = target;
            _teamShareLocalChanges = false;
            _teamShareBinding = ReadTeamShareBinding(target);
            ScheduleTeamShareFingerprint();
        }

        if (target is null)
        {
            PlanningTeamShareAction.Visibility = Visibility.Collapsed;
            PlanningTeamShareStatus.Visibility = Visibility.Collapsed;
            return;
        }

        var action = _teamShareBinding is not null ? "继续团队协作" : "共享到团队";
        PlanningTeamShareAction.Content = action;
        AutomationProperties.SetName(PlanningTeamShareAction, action);
        AutomationProperties.SetHelpText(PlanningTeamShareAction, $"当前策划案 #{target.ProjectId.ToString("N")[..8]}");
        PlanningTeamShareAction.ToolTip = _teamShareBinding is not null ? "打开线上最新版本" : "将当前策划案发布给团队";
        PlanningTeamShareAction.Visibility = Visibility.Visible;
        PlanningTeamShareAction.IsEnabled = !_teamShareBusy;
        var local = _teamShareBinding is not null && (_teamShareLocalChanges || owner!.HasTeamSharePendingChanges(target))
            ? "本地有后续修改。继续协作会打开线上最新版本。" : null;
        PlanningTeamShareStatus.Text = string.Join("\n", new[] { _teamShareFeedback, local }.OfType<string>());
        PlanningTeamShareStatus.Visibility = PlanningTeamShareStatus.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private PlanningTeamShareBinding? ReadTeamShareBinding(PlanningTeamShareTarget? target)
    {
        if (target is null) return null;
        try { return _teamShare!.ReadBinding(target.ProjectId, target.DocumentId); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { return null; } // A status check can recover a ready receipt without re-publishing it.
    }

    private void ScheduleTeamShareFingerprint()
    {
        _teamShareFingerprintEpoch++;
        _teamShareFingerprintTimer?.Stop();
        if (_teamShareBinding is not null && _teamShareHeaderTarget is not null) _teamShareFingerprintTimer?.Start();
    }

    private async Task RefreshTeamShareFingerprintAsync()
    {
        _teamShareFingerprintTimer?.Stop();
        if (_teamShareHeaderTarget is not { } target || _teamShareBinding is not { } binding) return;
        var epoch = _teamShareFingerprintEpoch;
        try
        {
            var fingerprint = await ReadTeamShareFingerprintAsync(target);
            if (epoch != _teamShareFingerprintEpoch) return;
            _teamShareLocalChanges = !string.IsNullOrWhiteSpace(binding.PublishedFingerprint)
                && !string.Equals(fingerprint, binding.PublishedFingerprint, StringComparison.OrdinalIgnoreCase);
            UpdateTeamShareHeader();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.Text.Json.JsonException)
        {
            // A local read can race a save. Leave the previous indication in place;
            // the next save or plan open schedules another comparison.
        }
    }

    private async void ShareToTeam_Click(object sender, RoutedEventArgs e)
    {
        if (_teamShareBusy || _vm is not { } owner || _teamShare is not { } share || owner.GetTeamShareTarget() is not { } target)
            return;

        var epoch = _teamShareWorkspaceEpoch;
        var hasBinding = _teamShareBinding is not null;
        var contactingTeam = false;
        var publicationReady = false;
        bool Current() => epoch == _teamShareWorkspaceEpoch && IsVisible && ReferenceEquals(_vm, owner) && owner.GetTeamShareTarget() == target;
        void Feedback(string? message)
        {
            if (!Current()) return;
            _teamShareFeedback = message;
            UpdateTeamShareHeader();
        }

        bool StopOnSaveFailure(PlanningTeamShareSaveResult result)
        {
            if (result == PlanningTeamShareSaveResult.Saved) return false;
            Feedback(result == PlanningTeamShareSaveResult.SaveFailed
                ? "策划保存失败，草稿已保留。保存成功后可重试共享。"
                : "策划已变化，请重新打开后再共享。");
            return true;
        }

        async Task OpenReadyAsync(PlanningTeamShareReceipt receipt, string? fallbackFingerprint = null)
        {
            publicationReady = true;
            share.AcceptReceipt(receipt, fallbackFingerprint);
            if (_teamShareHeaderTarget is { } displayed && displayed.ProjectId == target.ProjectId && displayed.DocumentId == target.DocumentId)
            {
                _teamShareBinding = ReadTeamShareBinding(displayed);
                ScheduleTeamShareFingerprint();
            }
            if (!Current()) return;
            Feedback("正在打开线上最新版本…");
            await share.OpenOwnerAsync(receipt, Window.GetWindow(this));
            Feedback(null);
        }

        _teamShareBusy = true;
        try
        {
            if (!hasBinding)
            {
                Feedback("正在保存当前策划…");
                var result = await owner.SaveForTeamShareAsync(target);
                if (!Current() || StopOnSaveFailure(result)) return;
            }

            Feedback("正在连接团队…");
            contactingTeam = true;
            var status = await share.GetStatusAsync(target.ProjectId, target.DocumentId);
            if (status.State == "ready") { await OpenReadyAsync(status); return; }
            if (!Current()) return;
            if (hasBinding)
            {
                Feedback("暂时无法打开已发布的团队策划。请重试，或联系团队管理员。");
                return;
            }
            if (status.State is not ("partial" or "notPublished")) throw new InvalidDataException("Unknown publication state.");

            contactingTeam = false;
            Feedback("正在检查可共享内容…");
            var revision = owner.TeamShareEditRevision;
            using var prepared = await PlanningTeamSharePreparation.PrepareAsync(target.ProjectId, target.DocumentId,
                target.PlanningRoot, target.ShotRoot);
            if (!Current()) return;
            if (owner.TeamShareEditRevision != revision)
            {
                Feedback("策划在检查期间有新修改，请重新共享。");
                return;
            }

            if (prepared.Issues.Any(issue => issue.Blocking))
            {
                Feedback("有内容需要修复，策划尚未发布。");
                ThemedMessageDialog.Show(Window.GetWindow(this), "暂时不能共享", TeamSharePreflightMessage(owner.DocumentTitle, prepared.Issues), ThemedMessageKind.Warning);
                return;
            }

            if (prepared.Issues.Count != 0)
            {
                Feedback(null);
                if (!ThemedMessageDialog.ConfirmTeamShare(Window.GetWindow(this), TeamSharePreflightMessage(owner.DocumentTitle, prepared.Issues), status.State == "partial") || !Current())
                    return;
            }

            // Rich text and grid edits may be newer than the view-model revision
            // while the snapshot is built or its preflight is shown.
            var confirmed = await owner.SaveForTeamShareAsync(target);
            if (!Current() || StopOnSaveFailure(confirmed)) return;
            var fingerprint = await ReadTeamShareFingerprintAsync(target);
            if (!Current()) return;
            if (owner.TeamShareEditRevision != revision || !string.Equals(prepared.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                Feedback("策划有新修改，请重新检查后发布。");
                return;
            }

            contactingTeam = true;
            Feedback("正在发布策划到团队…");
            var receipt = await share.PublishAsync(prepared);
            if (receipt.State == "ready") await OpenReadyAsync(receipt, prepared.Fingerprint);
            else Feedback("发布尚未完成。点击「共享到团队」可继续重试。");
        }
        catch (Exception error)
        {
            // Transport errors can contain owner-ticket URLs. Only report the stage
            // and failure class; the active view never prints remote exception text.
            System.Diagnostics.Trace.TraceWarning("Team share action failed ({0}).", error.GetType().Name);
            Feedback(publicationReady ? "团队策划已发布。暂时无法打开，请点击标题栏中的团队按钮重试。"
                : contactingTeam ? "团队连接暂时失败。请重试，已上传的内容可以继续。"
                : "无法准备共享内容。请检查当前策划及其文件，再重试共享。");
        }
        finally
        {
            _teamShareBusy = false;
            UpdateTeamShareHeader();
        }
    }

    private static Task<string> ReadTeamShareFingerprintAsync(PlanningTeamShareTarget target) =>
        Task.Run(() => PlanningTeamShareClient.ReadLocalFingerprintAsync(target.ProjectId, target.PlanningRoot, target.ShotRoot));

    private static string TeamSharePreflightMessage(string planTitle, IReadOnlyList<PlanningTeamShareIssue> issues)
    {
        var parts = new List<string> { $"策划案：{planTitle}" };
        if (issues.Any(issue => issue.Blocking)) parts.Add("请先修复下列内容，再重新共享：");
        else parts.Add("将此策划案当前已保存的版本发布给团队。发布后，团队在网页中继续编辑；以后在本机修改草稿不会覆盖团队版本。");
        foreach (var section in issues.GroupBy(issue => issue.Section))
        {
            parts.Add(section.Key + "\n" + string.Join("\n", section.Select(issue => $"• {issue.Item}：{issue.Message}")));
        }
        return string.Join("\n\n", parts);
    }
}
#endif
