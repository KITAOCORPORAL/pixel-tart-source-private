using System.Security.Cryptography;
using System.Text;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class OnboardingService(
    SettingsService settingsService,
    TutorialDataService tutorialDataService,
    ILogService? logService = null)
{
    // Keep this value stable so a patch upgrade does not invalidate completed tutorials.
    private const string ProofSalt = "KitaoPhotoSelector-Onboarding-1.2.0-Completion";
    private AppSettings? _settings;
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private readonly object _exitGate = new();
    private bool _exitPersistencePending;
    private long _sessionVersion;

    public TutorialState State { get; } = new();
    public TutorialSandboxPaths Sandbox => tutorialDataService.Paths;
    public long SessionVersion => Volatile.Read(ref _sessionVersion);
    public bool NeedsUpgradeOffer { get; private set; }
    public IReadOnlyList<TutorialStep> Steps { get; } = CreateSteps();
    public TutorialStep CurrentStep => Steps[Math.Clamp(State.CurrentStep, 1, Steps.Count) - 1];

    public async Task InitializeAsync(AppSettings settings, bool existingUser, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        var validCompletion = settings.OnboardingCompleted && IsCompletionProofValid(settings);
        if (validCompletion)
        {
            State.Mode = TutorialMode.Inactive;
            return;
        }

        if (existingUser || settings.OnboardingLegacyUser)
        {
            settings.OnboardingLegacyUser = true;
            settings.OnboardingCompleted = false;
            settings.OnboardingCompletionProof = string.Empty;
            State.Mode = TutorialMode.Inactive;
            NeedsUpgradeOffer = !settings.OnboardingUpgradeOfferShown;
            await settingsService.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            return;
        }

        await tutorialDataService.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        State.Mode = TutorialMode.Required;
        Interlocked.Increment(ref _sessionVersion);
        State.CurrentStep = Math.Clamp(settings.OnboardingCurrentStep, 1, Steps.Count);
        settings.OnboardingCompleted = false;
        settings.OnboardingVersion = Branding.ProductVersion;
        settings.OnboardingCompletedAt = null;
        settings.OnboardingCurrentStep = State.CurrentStep;
        settings.OnboardingCompletionProof = string.Empty;
        await settingsService.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task AcceptUpgradeOfferAsync(bool startTutorial, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        _settings!.OnboardingUpgradeOfferShown = true;
        NeedsUpgradeOffer = false;
        if (startTutorial)
        {
            await StartReplayAsync(cancellationToken).ConfigureAwait(false);
        }
        await settingsService.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartReplayAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await tutorialDataService.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        State.Mode = TutorialMode.Replay;
        Interlocked.Increment(ref _sessionVersion);
        State.CurrentStep = 1;
        State.ErrorMessage = string.Empty;
        State.VisitedCategories.Clear();
        State.VisitedOutputModes.Clear();
    }

    public void ExitReplay()
    {
        if (State.Mode != TutorialMode.Replay) return;
        State.Mode = TutorialMode.Inactive;
        State.CurrentStep = 1;
        State.ErrorMessage = string.Empty;
    }

    public async Task ExitAsync(CancellationToken cancellationToken = default)
    {
        var shouldPersist = DetachForExit();
        if (!shouldPersist) return;

        await _transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_exitGate)
            {
                if (!_exitPersistencePending) return;
            }

            if (await settingsService.TrySaveAsync(_settings!, cancellationToken).ConfigureAwait(false))
            {
                lock (_exitGate) _exitPersistencePending = false;
            }
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public bool DetachForExit()
    {
        lock (_exitGate)
        {
            if (!State.IsActive) return _exitPersistencePending;

            var wasRequired = State.IsRequired;
            Interlocked.Increment(ref _sessionVersion);
            if (wasRequired)
            {
                EnsureInitialized();
                _settings!.OnboardingCompleted = false;
                _settings.OnboardingCompletionProof = string.Empty;
                _settings.OnboardingCurrentStep = Math.Clamp(State.CurrentStep, 1, Steps.Count);
                _exitPersistencePending = true;
            }

            State.Mode = TutorialMode.Inactive;
            if (!wasRequired) State.CurrentStep = 1;
            State.ErrorMessage = string.Empty;
            State.VisitedCategories.Clear();
            State.VisitedOutputModes.Clear();
            return _exitPersistencePending;
        }
    }

    public bool CanPerform(TutorialAction action) => !State.IsActive || CurrentStep.RequiredAction == action;

    public async Task<TutorialValidationResult> PerformAsync(
        TutorialAction action,
        TutorialActionContext context,
        CancellationToken cancellationToken = default) =>
        await PerformForSessionAsync(SessionVersion, action, context, cancellationToken).ConfigureAwait(false);

    public async Task<TutorialValidationResult> PerformForSessionAsync(
        long sessionVersion,
        TutorialAction action,
        TutorialActionContext context,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (!IsSessionCurrent(sessionVersion)) return TutorialValidationResult.Success();
        if (CurrentStep.RequiredAction != action)
        {
            return Fail("请先完成当前高亮步骤。");
        }

        ObserveSelections(context);
        if (action == TutorialAction.SelectCollectionCategories &&
            (State.VisitedCategories.Count < 4 || context.CollectionCategory != RAWSelectionAssistant.Core.Models.CollectionCategory.JpegAndRaw))
        {
            return TutorialValidationResult.Success();
        }
        if (action == TutorialAction.SelectOutputModes &&
            (State.VisitedOutputModes.Count < 3 || context.OutputMode != RAWSelectionAssistant.Core.Models.OutputMode.ByFileCategory))
        {
            return TutorialValidationResult.Success();
        }
        var validation = Validate(CurrentStep, context);
        if (!IsSessionCurrent(sessionVersion)) return TutorialValidationResult.Success();
        if (!validation.Succeeded)
        {
            State.ErrorMessage = validation.Message;
            return validation;
        }

        State.ErrorMessage = string.Empty;
        if (action == TutorialAction.FinishTutorial)
        {
            await CompleteAsync(sessionVersion, cancellationToken).ConfigureAwait(false);
            return TutorialValidationResult.Success();
        }

        if (!IsSessionCurrent(sessionVersion)) return TutorialValidationResult.Success();
        State.CurrentStep = Math.Min(Steps.Count, State.CurrentStep + 1);
        if (State.IsRequired)
        {
            _settings!.OnboardingCurrentStep = State.CurrentStep;
            await settingsService.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
        }
        return TutorialValidationResult.Success();
    }

    private bool IsSessionCurrent(long sessionVersion) =>
        State.IsActive && Volatile.Read(ref _sessionVersion) == sessionVersion;

    public async Task<bool> BackAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var sessionVersion = SessionVersion;
        if (!IsSessionCurrent(sessionVersion) || !CurrentStep.AllowBack || State.CurrentStep <= 1) return false;
        State.CurrentStep--;
        State.ErrorMessage = string.Empty;
        if (State.IsRequired)
        {
            _settings!.OnboardingCurrentStep = State.CurrentStep;
            await settingsService.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
        }
        return IsSessionCurrent(sessionVersion);
    }

    public Task<TutorialSandboxPaths> ResetTutorialDataAsync(CancellationToken cancellationToken = default) =>
        tutorialDataService.ResetAsync(cancellationToken);
    public Task<TutorialSandboxPaths> EnsureTutorialDataAsync(CancellationToken cancellationToken = default) =>
        tutorialDataService.EnsureCreatedAsync(cancellationToken);

    public void DeleteTutorialData(string path) => tutorialDataService.Delete(path);
    public bool IsTutorialPath(string path) => tutorialDataService.IsWithinTutorial(path);
    public void EnsureTutorialPath(string path) => tutorialDataService.EnsureWithinTutorial(path);

    public static bool IsCompletionProofValid(AppSettings settings)
    {
        if (!settings.OnboardingCompleted || settings.OnboardingCompletedAt is null) return false;
        var expected = CreateProof(settings.OnboardingCompletedAt.Value, settings.OnboardingVersion);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(settings.OnboardingCompletionProof ?? string.Empty));
    }

    private async Task CompleteAsync(long sessionVersion, CancellationToken cancellationToken)
    {
        if (!IsSessionCurrent(sessionVersion)) return;
        var wasReplay = State.Mode == TutorialMode.Replay;
        if (!wasReplay)
        {
            var completedAt = DateTimeOffset.Now;
            _settings!.OnboardingCompleted = true;
            _settings.OnboardingVersion = Branding.ProductVersion;
            _settings.OnboardingCompletedAt = completedAt;
            _settings.OnboardingCurrentStep = Steps.Count;
            _settings.OnboardingCompletionProof = CreateProof(completedAt, Branding.ProductVersion);
            await settingsService.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
            if (!IsSessionCurrent(sessionVersion)) return;
            logService?.Info($"{Branding.ProductName} 首次新手教程已完成。");
        }
        if (!IsSessionCurrent(sessionVersion)) return;
        State.Mode = TutorialMode.Inactive;
        State.CurrentStep = 1;
        State.ErrorMessage = string.Empty;
    }

    private void ObserveSelections(TutorialActionContext context)
    {
        if (context.CollectionCategory is { } category) State.VisitedCategories.Add(category);
        if (context.OutputMode is { } mode) State.VisitedOutputModes.Add(mode);
    }

    private TutorialValidationResult Validate(TutorialStep step, TutorialActionContext context) => step.RequiredAction switch
    {
        TutorialAction.AddSourceDirectory when context.SourceDirectoryCount < 1 => Fail(step.ErrorMessage),
        TutorialAction.SelectCollectionCategories when State.VisitedCategories.Count < 4 || context.CollectionCategory != RAWSelectionAssistant.Core.Models.CollectionCategory.JpegAndRaw => Fail(step.ErrorMessage),
        TutorialAction.ScanSourceFiles when context.IndexedJpegCount < 3 || context.IndexedRawCount < 3 => Fail(step.ErrorMessage),
        TutorialAction.LoadCustomerSelection when context.SelectionCount < 1 => Fail(step.ErrorMessage),
        TutorialAction.ParseNumbers when context.SelectionCount < 3 => Fail(step.ErrorMessage),
        TutorialAction.ClearSelections when context.SelectionCount < 3 => Fail(step.ErrorMessage),
        TutorialAction.MatchFiles when context.CompleteMatchCount < 3 => Fail(step.ErrorMessage),
        TutorialAction.ViewDetails when !context.DetailsViewed => Fail(step.ErrorMessage),
        TutorialAction.SelectOutputDirectory when string.IsNullOrWhiteSpace(context.OutputDirectory) || !tutorialDataService.IsWithinTutorial(context.OutputDirectory) => Fail(step.ErrorMessage),
        TutorialAction.EnterProjectName when !string.Equals(context.ProjectName, Branding.TutorialProjectName, StringComparison.Ordinal) => Fail(step.ErrorMessage),
        TutorialAction.SelectOutputModes when State.VisitedOutputModes.Count < 3 || context.OutputMode != RAWSelectionAssistant.Core.Models.OutputMode.ByFileCategory => Fail(step.ErrorMessage),
        TutorialAction.CopyMatchedFiles when context.CopiedJpegCount < 3 || context.CopiedRawCount < 3 => Fail(step.ErrorMessage),
        TutorialAction.ExportReports when !context.ReportsExist => Fail(step.ErrorMessage),
        TutorialAction.OpenOutputDirectory when !context.OutputOpened => Fail(step.ErrorMessage),
        TutorialAction.ClearCurrentTask when !context.OutputPreserved => Fail(step.ErrorMessage),
        _ => TutorialValidationResult.Success()
    };

    private TutorialValidationResult Fail(string message)
    {
        State.ErrorMessage = message;
        return TutorialValidationResult.Failure(message);
    }

    private void EnsureInitialized()
    {
        if (_settings is null) throw new InvalidOperationException("新手教程尚未初始化。");
    }

    private static string CreateProof(DateTimeOffset completedAt, string version)
    {
        var value = $"{ProofSalt}|{version}|{completedAt:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static IReadOnlyList<TutorialStep> CreateSteps() =>
    [
        Step(1, "欢迎使用像素蛋挞", "接下来会用演示照片完成一次完整归片，不会读取、移动或修改你的真实照片。", TutorialAction.BeginTutorial, TutorialTarget.Welcome, false, "请点击“开始教程”。"),
        Step(2, "认识照片来源目录", "点击“添加”，加入内置教程来源目录。", TutorialAction.AddSourceDirectory, TutorialTarget.AddSourceButton, false, "教程来源目录尚未加入。"),
        Step(3, "安全移除搜索目录", "点击“删除”。它只移除搜索目录记录，不会删除硬盘照片。", TutorialAction.RemoveSourceDirectory, TutorialTarget.RemoveSourceButton, true, "请点击删除来源目录。"),
        Step(4, "选择归片类别", "依次查看仅 JPG、仅 RAW、JPG + RAW、自定义格式，最后选回 JPG + RAW。", TutorialAction.SelectCollectionCategories, TutorialTarget.CollectionCategorySelector, true, "请查看全部四种类别，并最终选择 JPG + RAW。"),
        Step(5, "扫描照片文件", "点击扫描，真实建立教程 JPG 与 RAW 索引。", TutorialAction.ScanSourceFiles, TutorialTarget.ScanButton, true, "教程 JPG 或 RAW 尚未完整进入索引。"),
        Step(6, "安全取消耗时任务", "模拟扫描正在运行，请点击“取消当前任务”。", TutorialAction.CancelSimulatedTask, TutorialTarget.CancelButton, true, "请点击取消当前任务。"),
        Step(7, "加载客户选片", "点击“加载教程选片”，使用与真实拖放相同的解析流程。", TutorialAction.LoadCustomerSelection, TutorialTarget.CustomerDropArea, true, "教程客户选片尚未加入。"),
        Step(8, "粘贴编号", "点击“粘贴编号”，把 1235、DSC01236.JPG 放入输入框。", TutorialAction.PasteNumbers, TutorialTarget.PasteButton, true, "请点击粘贴编号。"),
        Step(9, "解析编号", "点击“解析编号”，确认三条演示选片记录。", TutorialAction.ParseNumbers, TutorialTarget.ParseButton, true, "尚未正确解析三条演示编号。"),
        Step(10, "了解清空选片", "点击清空；教程会安全恢复演示记录，硬盘照片不会受影响。", TutorialAction.ClearSelections, TutorialTarget.ClearSelectionsButton, true, "请点击清空选片。"),
        Step(11, "开始匹配", "点击开始匹配，三组编号都应找到 JPG 和 RAW。", TutorialAction.MatchFiles, TutorialTarget.MatchButton, true, "三组 JPG + RAW 尚未全部匹配。"),
        Step(12, "查看匹配详情", "表格会自动向右定位到“处理”列。如果仍未看到，请拖动表格底部横向滚动条向右滑，看到后点击第一条记录的“查看明细”；也可以点击下方按钮直接打开。", TutorialAction.ViewDetails, TutorialTarget.FirstDetailsButton, true, "请打开并关闭第一条匹配详情。"),
        Step(13, "理解 JPG 质量判断", "客户 JPG 默认只识别编号；尺寸、EXIF 和大小都只是辅助判断。", TutorialAction.AcknowledgeJpegQuality, TutorialTarget.JpegQualityArea, true, "请点击“我知道了”。"),
        Step(14, "选择输出目录", "点击“选择”，教程固定使用安全的 Tutorial\\Output。", TutorialAction.SelectOutputDirectory, TutorialTarget.BrowseOutputButton, true, "教程输出目录尚未设置。"),
        Step(15, "输入项目名称", "在项目名称中输入“教程示例项目”。", TutorialAction.EnterProjectName, TutorialTarget.ProjectNameInput, true, "请输入完整的“教程示例项目”。"),
        Step(16, "选择输出分类", "依次查看三种输出方式，最后选“按文件类别输出”。", TutorialAction.SelectOutputModes, TutorialTarget.OutputModeSelector, true, "请查看三种输出方式，并最终选择按 JPG、RAW 分类。"),
        Step(17, "复制已匹配文件", "点击复制，真实复制三张 JPG 和三份 RAW 到教程输出目录。", TutorialAction.CopyMatchedFiles, TutorialTarget.CopyButton, true, "教程 JPG 和 RAW 尚未完整复制。"),
        Step(18, "导出匹配报告", "点击导出，生成 CSV、JSON 和操作日志。", TutorialAction.ExportReports, TutorialTarget.ExportButton, true, "三个教程报告尚未全部生成。"),
        Step(19, "打开输出文件夹", "由你主动点击打开教程输出目录。", TutorialAction.OpenOutputDirectory, TutorialTarget.OpenOutputButton, true, "教程输出目录尚未打开。"),
        Step(20, "清空当前任务", "点击清空任务；已经复制的教程文件和报告会保留。", TutorialAction.ClearCurrentTask, TutorialTarget.ClearTaskButton, true, "请清空任务并保留输出文件。"),
        Step(21, "免费版与专业版", "免费版可直接完成基础 JPG、RAW 与 JPG + RAW 归片；专业版用于多来源、高速索引、进阶报告和批量项目。无需购买即可继续使用免费版。", TutorialAction.AcknowledgeEditions, TutorialTarget.EditionStatusArea, true, "请点击“继续免费使用”。"),
        Step(22, "你已经完成第一次归片", "添加来源、扫描、导入编号、匹配、复制和报告都已完成。", TutorialAction.FinishTutorial, TutorialTarget.Completed, true, "请点击“开始使用像素蛋挞”。")
    ];

    private static TutorialStep Step(int number, string title, string instruction, TutorialAction action, TutorialTarget target, bool allowBack, string error) =>
        new(number, title, instruction, action, target, allowBack, true, error, error, number < 22 ? number + 1 : null);
}
