namespace RAWSelectionAssistant.Core.Models;

public enum LutKind { OneDimensional, ThreeDimensional }
public enum LutValidationStatus { Valid, Missing, Invalid, Unsupported }
public enum LutInputInterpretation { Unknown, SrgbDisplay, SonySLog3, CanonLog, NikonNLog, FujifilmFLog, Other }
public enum DisplayProfileStatus { Detected, NotConfigured, Missing, Corrupt, Unsupported, FallbackSrgb }
public enum ClientMonitorFollowMode { FollowMainSelection, FollowLatest, Locked }

public readonly record struct LutRgb(float Red, float Green, float Blue)
{
    public static LutRgb Lerp(LutRgb original, LutRgb transformed, float amount) => new(
        original.Red + ((transformed.Red - original.Red) * amount),
        original.Green + ((transformed.Green - original.Green) * amount),
        original.Blue + ((transformed.Blue - original.Blue) * amount));
}

public sealed record LutDefinition(
    string? Title,
    LutKind Kind,
    int Size,
    LutRgb DomainMin,
    LutRgb DomainMax,
    IReadOnlyList<LutRgb> Values);

public sealed record LutPresetReference(
    Guid Id,
    string DisplayName,
    string SourcePath,
    string NormalizedPath,
    string FileFingerprint,
    LutKind LutKind,
    int LutSize,
    LutRgb DomainMin,
    LutRgb DomainMax,
    bool IsFavorite,
    DateTimeOffset LastValidatedAtUtc,
    LutValidationStatus ValidationStatus,
    LutInputInterpretation InputInterpretation = LutInputInterpretation.Unknown,
    DateTimeOffset? LastUsedAtUtc = null);

public sealed record DisplayColorProfile(
    string StableDisplayKey,
    string FriendlyName,
    string DeviceName,
    string? ProfilePath,
    string ProfileName,
    DisplayProfileStatus Status,
    bool IsSystemDefault,
    DateTimeOffset UpdatedAtUtc,
    string ColorSpaceHint,
    string? ProfileFingerprint = null);

public sealed record MonitorDisplayInfo(
    string StableKey,
    string FriendlyName,
    string DeviceName,
    int Left,
    int Top,
    int Width,
    int Height,
    bool IsPrimary,
    double DpiX,
    double DpiY,
    string DeviceId = "");

public sealed record MonitorDisplayPreference(
    string StableDisplayKey,
    string FriendlyName,
    ClientMonitorFollowMode FollowMode = ClientMonitorFollowMode.FollowMainSelection,
    bool IsFullscreen = true,
    bool ShowFileName = false,
    bool ShowTechnicalMetadata = false,
    bool ShowRating = false,
    bool ShowClientControls = true,
    Guid? SelectedLutId = null,
    double LutStrength = 1,
    int Left = 0,
    int Top = 0,
    int Width = 1280,
    int Height = 720,
    double LastKnownDpi = 96);

public sealed record TetherColorSettings(
    IReadOnlyList<LutPresetReference> LutPresets,
    Guid? ProjectDefaultLutId = null,
    Guid? SessionDefaultLutId = null,
    int DefaultLutStrengthPercent = 100,
    long LutCacheLimitBytes = 512L * 1024 * 1024,
    string WorkingSpace = "sRGB",
    string UntaggedImageInterpretation = "sRGB",
    MonitorDisplayPreference? ClientMonitor = null);

public sealed record LutCacheKey(
    Guid AssetId,
    string ProxyVersion,
    string LutFingerprint,
    LutInputInterpretation InputInterpretation,
    int StrengthPercent,
    string StableDisplayKey,
    string IccFingerprint,
    int RenderVersion);

public sealed record LutParseResult(bool Success, LutDefinition? Definition, string? ErrorCode = null, string? Message = null);

public sealed record ClientMonitorState(
    ClientMonitorFollowMode FollowMode,
    Guid? AssetId,
    bool IsConnected,
    bool IsOpen,
    int NewAssetCount,
    string StatusText);
