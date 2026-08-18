using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class SettingsService
{
    private readonly ILogService _logService;
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    public bool WasSettingsFilePresent { get; private set; }
    public bool WasSettingsFileCorrupted { get; private set; }
    public bool WasLegacySettings { get; private set; }

    public SettingsService(ILogService logService, string? settingsFilePath = null)
    {
        _logService = logService;
        _settingsFilePath = settingsFilePath ?? AppDataPaths.SettingsFile;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AppDataPaths.EnsureCreated();
            WasSettingsFilePresent = File.Exists(_settingsFilePath);
            if (!File.Exists(_settingsFilePath))
            {
                var defaults = new AppSettings();
                Upgrade(defaults);
                return defaults;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            WasLegacySettings = !document.RootElement.TryGetProperty("onboardingVersion", out _) &&
                                !document.RootElement.TryGetProperty(nameof(AppSettings.OnboardingVersion), out _);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _options, cancellationToken).ConfigureAwait(false)
                           ?? new AppSettings();
            if (!document.RootElement.TryGetProperty(nameof(AppSettings.QuickToolLayout), out _))
                settings.QuickToolLayout = new QuickToolLayout { OrderedToolIds = QuickToolsService.Normalize(settings.PinnedQuickTools) };
            if (WasLegacySettings) settings.OnboardingLegacyUser = true;
            Upgrade(settings);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            WasSettingsFileCorrupted = true;
            _logService.Error("设置文件损坏或无法读取，已恢复默认设置。", ex);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _ = await TrySaveAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TrySaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AppDataPaths.EnsureCreated();
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
            Upgrade(settings);
            var temporaryPath = _settingsFilePath + ".tmp";
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
            {
                await JsonSerializer.SerializeAsync(stream, settings, _options, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _settingsFilePath, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.Error("无法保存用户设置。", ex);
            return false;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private static void Upgrade(AppSettings settings)
    {
        settings.Appearance ??= new AppearanceSettings();
        settings.ReportSettings ??= new ReportSettings();
        settings.PinnedQuickTools = QuickToolsService.Normalize(settings.PinnedQuickTools);
        settings.QuickToolLayout ??= new QuickToolLayout();
        settings.QuickToolLayout.SchemaVersion = QuickToolLayout.CurrentSchemaVersion;
        settings.QuickToolLayout.OrderedToolIds = QuickToolsService.Normalize(
            settings.QuickToolLayout.OrderedToolIds.Count > 0 ? settings.QuickToolLayout.OrderedToolIds : settings.PinnedQuickTools);
        settings.PinnedQuickTools = settings.QuickToolLayout.OrderedToolIds.ToList();
        settings.ProductQuickToolLayout ??= new ProductQuickToolLayout();
        settings.ProductQuickToolLayout.SchemaVersion = ProductQuickToolLayout.CurrentSchemaVersion;
        settings.ProductQuickToolLayout.OrderedToolIds = ProductToolboxPolicy.Normalize(
            settings.ProductQuickToolLayout.OrderedToolIds);
        settings.LastPrimaryPage = PrimaryNavigationPolicy.Normalize(settings.LastPrimaryPage);
        settings.AssetLibraryWorkspace ??= new AssetLibraryWorkspaceSettings();
        settings.AssetLibraryWorkspace.Normalize();
        settings.Appearance.CustomAccentColor = NormalizeAccent(settings.Appearance.CustomAccentColor);
        if (!Enum.IsDefined(settings.Appearance.Theme)) settings.Appearance.Theme = ThemeMode.System;
        if (!Enum.IsDefined(settings.Appearance.Accent)) settings.Appearance.Accent = AccentPreset.KitaoBlue;
        if (!Enum.IsDefined(settings.Appearance.Density)) settings.Appearance.Density = InterfaceDensity.Comfortable;
        if (!Enum.IsDefined(settings.Appearance.Sidebar)) settings.Appearance.Sidebar = SidebarMode.Remember;
        if (!Enum.IsDefined(settings.Appearance.Motion)) settings.Appearance.Motion = MotionPreference.Normal;
        if (!Enum.IsDefined(settings.Appearance.FontScale)) settings.Appearance.FontScale = FontScale.Standard;
        settings.EnabledJpegExtensions = NormalizeOrDefault(settings.EnabledJpegExtensions, MediaExtensionPolicy.DefaultJpegExtensions);
        settings.EnabledRawExtensions = NormalizeOrDefault(
            settings.EnabledRawExtensions.Concat(settings.CustomRawExtensions ?? []),
            MediaExtensionPolicy.DefaultRawExtensions);
        settings.CustomExtensions = NormalizeOrDefault(settings.CustomExtensions, []);
        settings.CustomRawExtensions ??= [];
        settings.SourceDirectories ??= [];
        settings.CustomerJpegMode ??= settings.AllowCustomerJpegFallback
            ? CustomerJpegHandlingMode.AllowCustomerFile
            : CustomerJpegHandlingMode.Strict;
        settings.AllowCustomerJpegFallback = settings.CustomerJpegMode == CustomerJpegHandlingMode.AllowCustomerFile;
        settings.OnboardingVersion = string.IsNullOrWhiteSpace(settings.OnboardingVersion) ? Branding.ProductVersion : settings.OnboardingVersion;
        settings.OnboardingCurrentStep = Math.Clamp(settings.OnboardingCurrentStep, 1, 22);
    }

    private static string NormalizeAccent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "#C98220";
        var normalized = value.Trim().ToUpperInvariant();
        if (!normalized.StartsWith('#')) normalized = $"#{normalized}";
        return normalized.Length == 7 && normalized.Skip(1).All(Uri.IsHexDigit) ? normalized : "#C98220";
    }

    private static List<string> NormalizeOrDefault(IEnumerable<string>? values, IEnumerable<string> defaults)
    {
        var normalized = (values ?? [])
            .Select(MediaExtensionPolicy.NormalizeExtension)
            .Where(x => x.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return normalized.Count > 0 ? normalized : defaults.ToList();
    }
}
