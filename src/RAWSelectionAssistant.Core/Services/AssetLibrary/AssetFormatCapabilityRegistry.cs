namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

/// <summary>
/// The media type names used by the Asset Library.  The values intentionally keep
/// the existing repository names (Image, Raw, Document and Video) where they are
/// already persisted in SQLite.  New values are only introduced where the product
/// needs to distinguish a font from a document.
/// </summary>
public static class AssetMediaTypes
{
    public const string Image = "Image";
    public const string Raw = "Raw";
    public const string Document = "Document";
    public const string Video = "Video";
    public const string Font = "Font";
    public const string Other = "Other";
}

/// <summary>
/// Describes what the Asset Library can actually do with one file format.
///
/// A format being indexable does not imply that it has a thumbnail, viewer or
/// visual-analysis provider.  Provider names are stable contract identifiers, not
/// implementation type names; a missing provider is represented by <see langword="null"/>.
/// </summary>
public sealed record AssetFormatCapability
{
    public AssetFormatCapability(
        string extension,
        string mediaType,
        bool canImport,
        bool canIndex,
        bool canThumbnail,
        bool canView,
        bool canAnalyze,
        string? viewerProvider = null,
        string? thumbnailProvider = null,
        string? analysisProvider = null,
        string? limitationReason = null)
    {
        Extension = AssetFormatCapabilityRegistry.NormalizeExtension(extension);
        if (Extension.Length == 0)
            throw new ArgumentException("A format capability requires an extension.", nameof(extension));

        MediaType = string.IsNullOrWhiteSpace(mediaType) ? AssetMediaTypes.Other : mediaType.Trim();
        CanImport = canImport;
        CanIndex = canIndex;
        CanThumbnail = canThumbnail;
        CanView = canView;
        CanAnalyze = canAnalyze;
        ViewerProvider = NullIfBlank(viewerProvider);
        ThumbnailProvider = NullIfBlank(thumbnailProvider);
        AnalysisProvider = NullIfBlank(analysisProvider);
        LimitationReason = NullIfBlank(limitationReason);

        if (CanView && ViewerProvider is null)
            throw new ArgumentException("A viewable format must name its viewer provider.", nameof(viewerProvider));
        if (CanThumbnail && ThumbnailProvider is null)
            throw new ArgumentException("A thumbnail-capable format must name its thumbnail provider.", nameof(thumbnailProvider));
        if (CanAnalyze && AnalysisProvider is null)
            throw new ArgumentException("An analyzable format must name its analysis provider.", nameof(analysisProvider));
    }

    /// <summary>Canonical, upper-case extension including the leading dot.</summary>
    public string Extension { get; }

    /// <summary>Existing AssetItems.MediaType-compatible classification.</summary>
    public string MediaType { get; }

    public bool CanImport { get; }
    public bool CanIndex { get; }
    public bool CanThumbnail { get; }
    public bool CanView { get; }
    public bool CanAnalyze { get; }
    public string? ViewerProvider { get; }
    public string? ThumbnailProvider { get; }
    public string? AnalysisProvider { get; }
    public string? LimitationReason { get; }

    // Aliases make the contract explicit to callers that use “Id” terminology.
    public string NormalizedExtension => Extension;
    public string? ViewerProviderId => ViewerProvider;
    public string? ThumbnailProviderId => ThumbnailProvider;
    public string? AnalysisProviderId => AnalysisProvider;

    public bool IsSupported => CanImport || CanIndex || CanThumbnail || CanView || CanAnalyze;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Single source of truth for Asset Library format capabilities.
///
/// The registry deliberately lives in Core rather than the WPF module.  Import,
/// repository classification, metadata extraction and UI adapters can therefore
/// consume the same table without taking a dependency on WPF.
/// </summary>
public sealed class AssetFormatCapabilityRegistry
{
    public const string WpfBitmapThumbnailProvider = "thumbnail.wpf-bitmap-v1";
    public const string WpfRasterAnalysisProvider = "analysis.wpf-raster-v1";

    private readonly IReadOnlyList<AssetFormatCapability> _items;
    private readonly IReadOnlyDictionary<string, AssetFormatCapability> _byExtension;

    public AssetFormatCapabilityRegistry(IEnumerable<AssetFormatCapability>? capabilities = null)
    {
        var values = (capabilities ?? CreateDefaultCapabilities()).ToArray();
        if (values.Length == 0)
            throw new ArgumentException("At least one format capability is required.", nameof(capabilities));

        var byExtension = new Dictionary<string, AssetFormatCapability>(StringComparer.OrdinalIgnoreCase);
        foreach (var capability in values)
        {
            var extension = NormalizeExtension(capability.Extension);
            if (extension.Length == 0)
                throw new ArgumentException("A format capability requires an extension.", nameof(capabilities));
            if (!byExtension.TryAdd(extension, capability))
                throw new ArgumentException($"Duplicate format capability: {extension}", nameof(capabilities));
        }

        _byExtension = byExtension;
        _items = values;
    }

    /// <summary>The production table used when no dependency injection override is supplied.</summary>
    public static AssetFormatCapabilityRegistry Default { get; } = new();

    public static AssetFormatCapabilityRegistry CreateDefault() => new();

    public IReadOnlyList<AssetFormatCapability> Items => _items;
    public IReadOnlyList<AssetFormatCapability> Capabilities => _items;

    public IReadOnlyList<string> ImportableExtensions => _items
        .Where(item => item.CanImport)
        .Select(item => item.Extension)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Resolves an extension or a file path.  Matching is case-insensitive and
    /// accepts values with or without a leading dot.
    /// </summary>
    public bool TryGet(string? extensionOrPath, out AssetFormatCapability capability)
    {
        var extension = NormalizeExtension(extensionOrPath);
        return _byExtension.TryGetValue(extension, out capability!);
    }

    public bool TryGetByExtension(string? extensionOrPath, out AssetFormatCapability capability) =>
        TryGet(extensionOrPath, out capability);

    public AssetFormatCapability GetOrUnknown(string? extensionOrPath)
    {
        if (TryGet(extensionOrPath, out var capability)) return capability;
        return Unknown(NormalizeExtension(extensionOrPath));
    }

    public AssetFormatCapability GetOrDefault(string? extensionOrPath) => GetOrUnknown(extensionOrPath);

    /// <summary>
    /// Builds a Windows file-picker filter from the importable entries in this
    /// registry.  The extension spelling in a filter is lower-case for readability;
    /// Windows matching itself remains case-insensitive.
    /// </summary>
    public string BuildPickerFilter(string displayName = "支持的素材", bool includeAllFiles = true)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "支持的素材" : displayName.Trim();
        var patterns = _items
            .Where(item => item.CanImport)
            .Select(item => $"*{item.Extension.ToLowerInvariant()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var filter = $"{name}|{string.Join(';', patterns)}";
        return includeAllFiles ? filter + "|所有文件|*.*" : filter;
    }

    public static string BuildDefaultPickerFilter(bool includeAllFiles = true) =>
        Default.BuildPickerFilter("支持的素材", includeAllFiles);

    /// <summary>
    /// Normalizes either a bare extension ("jpg"), a dotted extension (".JPG"),
    /// or a path ("C:\\photos\\capture.jpg") to an upper-case dotted extension.
    /// Invalid/extensionless values become the empty string.
    /// </summary>
    public static string NormalizeExtension(string? extensionOrPath)
    {
        if (string.IsNullOrWhiteSpace(extensionOrPath)) return string.Empty;
        var value = extensionOrPath.Trim();
        try
        {
            var looksLikePath = value.Contains(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                value.Contains(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal) || value.Contains(':');
            var extension = looksLikePath ? Path.GetExtension(value) : value;
            extension = extension.Trim();
            if (extension.Length == 0 || extension == ".") return string.Empty;
            if (!extension.StartsWith(".", StringComparison.Ordinal)) extension = "." + extension;
            return extension.ToUpperInvariant();
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static AssetFormatCapability Unknown(string extension) => new(
        extension.Length == 0 ? ".UNKNOWN" : extension,
        AssetMediaTypes.Other,
        canImport: false,
        canIndex: false,
        canThumbnail: false,
        canView: false,
        canAnalyze: false,
        limitationReason: extension.Length == 0
            ? "文件没有可识别的扩展名。"
            : $"扩展名 {extension} 未登记在素材库格式能力表中。");

    private static IReadOnlyList<AssetFormatCapability> CreateDefaultCapabilities()
    {
        const string rasterLimitation = "素材库当前没有独立 Viewer；此格式的缩略图和视觉分析使用 WPF 栅格 provider。";
        const string wicLimitation = "WEBP 是否可用取决于目标系统的 WIC 编解码器；本阶段不将其声明为稳定缩略图或分析能力。";
        const string indexedOnly = "当前仅保证导入和元数据索引；素材库尚未接入该格式的缩略图、Viewer 或视觉分析 provider。";
        const string rawLimitation = "当前仅保证导入和元数据索引；RAW 缩略图、Viewer 和视觉分析 provider 尚未接入素材库。";
        const string viewerOnlyLimitation = "素材库当前没有该格式的缩略图、Viewer 或视觉分析 provider。";

        var values = new List<AssetFormatCapability>();

        Add(values, [".JPG", ".JPEG", ".PNG", ".BMP", ".TIF", ".TIFF"], AssetMediaTypes.Image,
            rasterLimitation, canThumbnail: true, canAnalyze: true);
        Add(values, [".WEBP"], AssetMediaTypes.Image,
            wicLimitation, canThumbnail: false, canAnalyze: false);

        Add(values, [".ARW", ".CR2", ".CR3", ".NEF", ".RAF", ".ORF", ".RW2", ".DNG",
            ".NRW", ".ORI", ".PEF", ".3FR", ".FFF", ".IIQ", ".SRW", ".RWL"], AssetMediaTypes.Raw,
            rawLimitation);
        Add(values, [".PSD", ".PSB", ".SVG"], AssetMediaTypes.Document, indexedOnly);
        Add(values, [".GIF"], AssetMediaTypes.Image, viewerOnlyLimitation);
        Add(values, [".MP4", ".MOV", ".AVI"], AssetMediaTypes.Video, indexedOnly);
        Add(values, [".PDF"], AssetMediaTypes.Document, indexedOnly);
        Add(values, [".TTF", ".OTF", ".TTC", ".FNT", ".FON", ".WOFF", ".WOFF2"], AssetMediaTypes.Font, indexedOnly);

        return values;

        static void Add(
            ICollection<AssetFormatCapability> target,
            IEnumerable<string> extensions,
            string mediaType,
            string limitationReason,
            bool canThumbnail = false,
            bool canAnalyze = false)
        {
            foreach (var extension in extensions)
                target.Add(new AssetFormatCapability(
                    extension,
                    mediaType,
                    canImport: true,
                    canIndex: true,
                    canThumbnail,
                    canView: false,
                    canAnalyze,
                    viewerProvider: null,
                    thumbnailProvider: canThumbnail ? WpfBitmapThumbnailProvider : null,
                    analysisProvider: canAnalyze ? WpfRasterAnalysisProvider : null,
                    limitationReason));
        }
    }
}
