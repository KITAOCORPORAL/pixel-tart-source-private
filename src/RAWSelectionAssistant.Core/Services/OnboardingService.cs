using System.Security.Cryptography;
using System.Text;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

/// <summary>
/// Compatibility boundary for retired onboarding settings and callers.
/// The old split tutorial cannot be activated, resumed or create example files.
/// Existing settings, projects and tutorial directories are deliberately untouched.
/// </summary>
public sealed class OnboardingService(
    SettingsService settingsService,
    TutorialDataService tutorialDataService,
    ILogService? logService = null)
{
    private const string ProofSalt = "KitaoPhotoSelector-Onboarding-1.2.0-Completion";
    private static readonly TutorialStep InactiveStep = new(0, "", "", TutorialAction.BeginTutorial,
        TutorialTarget.Welcome, false, false, "", "", null);
    public TutorialState State { get; } = new();
    public TutorialSandboxPaths Sandbox => tutorialDataService.Paths;
    public long SessionVersion => 0;
    public bool NeedsUpgradeOffer => false;
    public IReadOnlyList<TutorialStep> Steps => [];
    public TutorialStep CurrentStep => InactiveStep;

    public Task InitializeAsync(AppSettings settings, bool existingUser, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State.Mode = TutorialMode.Inactive;
        State.ErrorMessage = string.Empty;
        State.VisitedCategories.Clear();
        State.VisitedOutputModes.Clear();
        // Do not forge a completion proof, reset user settings, or touch old demo files.
        return Task.CompletedTask;
    }
    public Task AcceptUpgradeOfferAsync(bool startTutorial, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StartReplayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void ExitReplay() { }
    public Task ExitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public bool DetachForExit() => false;
    public bool CanPerform(TutorialAction action) => true;
    public Task<TutorialValidationResult> PerformAsync(TutorialAction action, TutorialActionContext context,
        CancellationToken cancellationToken = default) => Task.FromResult(TutorialValidationResult.Success());
    public Task<TutorialValidationResult> PerformForSessionAsync(long sessionVersion, TutorialAction action,
        TutorialActionContext context, CancellationToken cancellationToken = default) => Task.FromResult(TutorialValidationResult.Success());
    public Task<bool> BackAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<TutorialSandboxPaths> ResetTutorialDataAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sandbox);
    public Task<TutorialSandboxPaths> EnsureTutorialDataAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sandbox);
    public void DeleteTutorialData(string path) { }
    public bool IsTutorialPath(string path) => tutorialDataService.IsWithinTutorial(path);
    public void EnsureTutorialPath(string path) => tutorialDataService.EnsureWithinTutorial(path);

    // Read-only historical compatibility; never used to decide whether the retired UI is shown.
    public static bool IsCompletionProofValid(AppSettings settings)
    {
        if (!settings.OnboardingCompleted || settings.OnboardingCompletedAt is null) return false;
        var value = $"{ProofSalt}|{settings.OnboardingVersion}|{settings.OnboardingCompletedAt.Value:O}";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(settings.OnboardingCompletionProof ?? string.Empty));
    }
}
