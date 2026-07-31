using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class ExistingUserDetectionService
{
    public bool IsExistingUser(
        AppSettings settings,
        bool legacySettingsDetected,
        bool settingsFileWasPresent,
        bool historicalIndexExists,
        bool historicalReportExists,
        bool historicalLogExists,
        bool currentTutorialInProgress = false) =>
        !currentTutorialInProgress && (
            legacySettingsDetected ||
            settings.OnboardingLegacyUser ||
            settings.SourceDirectories.Count > 0 ||
            settings.RecentRawDirectories.Count > 0 ||
            historicalIndexExists ||
            historicalReportExists ||
            historicalLogExists ||
            settingsFileWasPresent && HasMeaningfulHistory(settings));

    public bool IsCurrentTutorialInProgress(AppSettings settings, bool settingsFileWasPresent, bool legacySettingsDetected) =>
        settingsFileWasPresent &&
        !legacySettingsDetected &&
        !settings.OnboardingCompleted &&
        !settings.OnboardingLegacyUser;

    private static bool HasMeaningfulHistory(AppSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.RecentOutputDirectory) ||
        !string.IsNullOrWhiteSpace(settings.RecentProjectName);
}
