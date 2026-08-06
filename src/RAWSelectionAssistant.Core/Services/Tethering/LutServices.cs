using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tethering;

public interface ILutParser
{
    Task<LutParseResult> ParseAsync(string path, CancellationToken cancellationToken = default);
    Task<LutParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}

public interface ILutValidator
{
    LutParseResult Validate(LutDefinition definition);
}

public interface ILutProcessor
{
    LutRgb Apply(LutDefinition definition, LutRgb input, float strength = 1);
}

public interface ILutPresetStore
{
    Task<TetherColorSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(TetherColorSettings settings, CancellationToken cancellationToken = default);
    Task<LutPresetReference> ImportAsync(string path, LutInputInterpretation interpretation = LutInputInterpretation.Unknown, CancellationToken cancellationToken = default);
    Task<LutPresetReference> RelocateAsync(Guid presetId, string path, CancellationToken cancellationToken = default);
    Task RemoveReferenceAsync(Guid presetId, CancellationToken cancellationToken = default);
}

public interface ILutCacheService
{
    string CreateOpaqueKey(LutCacheKey key);
    string? Resolve(string opaqueKey);
    Task StoreAsync(string opaqueKey, Stream content, CancellationToken cancellationToken = default);
    Task TrimAsync(CancellationToken cancellationToken = default);
    Task InvalidateAsync(Func<string, bool> predicate, CancellationToken cancellationToken = default);
}

public sealed class LutRenderRequestCoordinator : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _current;
    private long _version;
    public (long Version, CancellationToken Token) Begin(CancellationToken lifetime = default)
    {
        lock (_sync)
        {
            _current?.Cancel(); _current?.Dispose();
            _current = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            return (++_version, _current.Token);
        }
    }
    public bool IsCurrent(long version) { lock (_sync) return version == _version && _current is { IsCancellationRequested: false }; }
    public void CancelCurrent() { lock (_sync) { _current?.Cancel(); _current?.Dispose(); _current = null; _version++; } }
    public void Dispose() { lock (_sync) { _current?.Cancel(); _current?.Dispose(); _current = null; } }
}

public sealed class CubeLutValidator : ILutValidator
{
    public const int MinimumSize = 2;
    public const int Maximum1DSize = 65536;
    public const int Maximum3DSize = 65;

    public LutParseResult Validate(LutDefinition definition)
    {
        var maximum = definition.Kind == LutKind.OneDimensional ? Maximum1DSize : Maximum3DSize;
        if (definition.Size is < MinimumSize || definition.Size > maximum)
            return new(false, null, "LutSizeOutOfRange", $"LUT尺寸必须在{MinimumSize}至{maximum}之间。");
        var expected = definition.Kind == LutKind.OneDimensional ? definition.Size : checked(definition.Size * definition.Size * definition.Size);
        if (definition.Values.Count != expected)
            return new(false, null, "LutDataCountMismatch", "LUT数据数量与声明尺寸不一致。");
        if (!Finite(definition.DomainMin) || !Finite(definition.DomainMax) || definition.Values.Any(value => !Finite(value)))
            return new(false, null, "LutNonFiniteValue", "LUT包含NaN或Infinity。");
        if (definition.DomainMax.Red <= definition.DomainMin.Red || definition.DomainMax.Green <= definition.DomainMin.Green || definition.DomainMax.Blue <= definition.DomainMin.Blue)
            return new(false, null, "LutInvalidDomain", "LUT输入范围无效。");
        return new(true, definition);
    }

    private static bool Finite(LutRgb value) => float.IsFinite(value.Red) && float.IsFinite(value.Green) && float.IsFinite(value.Blue);
}

public sealed class CubeLutParser(ILutValidator? validator = null, long maximumFileBytes = 32L * 1024 * 1024) : ILutParser
{
    private readonly ILutValidator _validator = validator ?? new CubeLutValidator();

    public async Task<LutParseResult> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Path.GetExtension(path), ".cube", StringComparison.OrdinalIgnoreCase)) return new(false, null, "LutExtensionInvalid", "仅支持.cube LUT。");
        if (!File.Exists(path)) return new(false, null, "LutMissing", "LUT文件不存在或暂时不可访问。");
        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > maximumFileBytes) return new(false, null, "LutFileSizeInvalid", "LUT文件为空或超过安全大小上限。");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, true);
        return await ParseAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LutParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (!stream.CanRead) return new(false, null, "LutUnreadable", "LUT文件不可读。");
        if (stream.CanSeek && (stream.Length <= 0 || stream.Length > maximumFileBytes)) return new(false, null, "LutFileSizeInvalid", "LUT文件为空或超过安全大小上限。");
        try
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, 65536, leaveOpen: true);
            string? title = null;
            int? oneDSize = null;
            int? threeDSize = null;
            var domainMin = new LutRgb(0, 0, 0);
            var domainMax = new LutRgb(1, 1, 1);
            var values = new List<LutRgb>();
            var headerOpen = true;
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } raw)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = StripComment(raw).Trim();
                if (line.Length == 0) continue;
                if (line.Any(character => character == '\0' || (char.IsControl(character) && character != '\t')))
                    return new(false, null, "LutBinaryContent", "文件包含非文本内容，已拒绝解析。");
                var firstSpace = line.IndexOfAny([' ', '\t']);
                var keyword = (firstSpace < 0 ? line : line[..firstSpace]).ToUpperInvariant();
                var remainder = firstSpace < 0 ? string.Empty : line[(firstSpace + 1)..].Trim();
                if (keyword == "TITLE") { if (!headerOpen) return Unexpected(); title = ParseTitle(remainder); continue; }
                if (keyword == "LUT_1D_SIZE") { if (!headerOpen || oneDSize.HasValue || threeDSize.HasValue || !TryInteger(remainder, out var size)) return Unexpected(); oneDSize = size; continue; }
                if (keyword == "LUT_3D_SIZE") { if (!headerOpen || oneDSize.HasValue || threeDSize.HasValue || !TryInteger(remainder, out var size)) return Unexpected(); threeDSize = size; continue; }
                if (keyword == "DOMAIN_MIN") { if (!headerOpen || !TryRgb(remainder, out domainMin)) return Unexpected(); continue; }
                if (keyword == "DOMAIN_MAX") { if (!headerOpen || !TryRgb(remainder, out domainMax)) return Unexpected(); continue; }
                var dataParts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if ((oneDSize.HasValue || threeDSize.HasValue) && dataParts.Length == 3 && !TryRgb(line, out _))
                    return new(false, null, "LutDataInvalid", "LUT数据行必须包含三个有限数字。");
                if (char.IsLetter(keyword[0]) || keyword[0] == '_') return Unexpected();
                headerOpen = false;
                if (!TryRgb(line, out var value)) return new(false, null, "LutDataInvalid", "LUT数据行必须包含三个有限数字。");
                values.Add(value);
                var declaredSize = oneDSize ?? threeDSize;
                if (!declaredSize.HasValue) return new(false, null, "LutSizeMissing", "LUT缺少尺寸声明。");
                var maximumCount = oneDSize.HasValue ? Math.Min(oneDSize.Value, CubeLutValidator.Maximum1DSize) : SafeCube(Math.Min(threeDSize!.Value, CubeLutValidator.Maximum3DSize));
                if (values.Count > maximumCount) return new(false, null, "LutDataCountMismatch", "LUT包含超出声明数量的数据。");
            }
            if (oneDSize.HasValue == threeDSize.HasValue) return new(false, null, "LutUnsupportedCombination", "必须且只能声明纯1D或纯3D LUT；阶段D不支持Shaper与3D组合。");
            var definition = new LutDefinition(title, oneDSize.HasValue ? LutKind.OneDimensional : LutKind.ThreeDimensional, oneDSize ?? threeDSize!.Value, domainMin, domainMax, values);
            return _validator.Validate(definition);
        }
        catch (DecoderFallbackException) { return new(false, null, "LutBinaryContent", "文件不是有效UTF-8文本，已拒绝解析。"); }
        catch (IOException) { return new(false, null, "LutUnreadable", "LUT文件暂时不可访问。"); }
        catch (OverflowException) { return new(false, null, "LutSizeOutOfRange", "LUT尺寸超过安全范围。"); }
    }

    private static int SafeCube(int value) => checked(value * value * value);
    private static LutParseResult Unexpected() => new(false, null, "LutUnsupportedDirective", "LUT包含不支持或位置无效的指令。");
    private static string StripComment(string value) { var index = value.IndexOf('#'); return index < 0 ? value : value[..index]; }
    private static string? ParseTitle(string value) { var title = value.Trim(); if (title.Length >= 2 && title[0] == '"' && title[^1] == '"') title = title[1..^1]; return title.Length == 0 ? null : title[..Math.Min(title.Length, 200)]; }
    private static bool TryInteger(string value, out int result) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    private static bool TryRgb(string value, out LutRgb result)
    {
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var red) && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var green) && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var blue) && float.IsFinite(red) && float.IsFinite(green) && float.IsFinite(blue)) { result = new(red, green, blue); return true; }
        result = default; return false;
    }
}

public sealed class CpuLutProcessor : ILutProcessor
{
    public LutRgb Apply(LutDefinition definition, LutRgb input, float strength = 1)
    {
        strength = Math.Clamp(strength, 0, 1);
        if (strength == 0) return input;
        var normalized = Normalize(input, definition.DomainMin, definition.DomainMax);
        var transformed = definition.Kind == LutKind.OneDimensional ? ApplyOneDimensional(definition, normalized) : ApplyThreeDimensional(definition, normalized);
        return LutRgb.Lerp(input, transformed, strength);
    }

    private static LutRgb ApplyOneDimensional(LutDefinition definition, LutRgb value) => new(
        InterpolateChannel(definition.Values, definition.Size, value.Red, static item => item.Red),
        InterpolateChannel(definition.Values, definition.Size, value.Green, static item => item.Green),
        InterpolateChannel(definition.Values, definition.Size, value.Blue, static item => item.Blue));

    private static float InterpolateChannel(IReadOnlyList<LutRgb> values, int size, float value, Func<LutRgb, float> selector)
    {
        var scaled = Math.Clamp(value, 0, 1) * (size - 1);
        var lower = (int)MathF.Floor(scaled);
        var upper = Math.Min(lower + 1, size - 1);
        var amount = scaled - lower;
        return selector(values[lower]) + ((selector(values[upper]) - selector(values[lower])) * amount);
    }

    private static LutRgb ApplyThreeDimensional(LutDefinition definition, LutRgb value)
    {
        var size = definition.Size;
        var red = Axis(value.Red, size); var green = Axis(value.Green, size); var blue = Axis(value.Blue, size);
        LutRgb At(int r, int g, int b) => definition.Values[r + (g * size) + (b * size * size)];
        var c00 = LutRgb.Lerp(At(red.Lower, green.Lower, blue.Lower), At(red.Upper, green.Lower, blue.Lower), red.Amount);
        var c10 = LutRgb.Lerp(At(red.Lower, green.Upper, blue.Lower), At(red.Upper, green.Upper, blue.Lower), red.Amount);
        var c01 = LutRgb.Lerp(At(red.Lower, green.Lower, blue.Upper), At(red.Upper, green.Lower, blue.Upper), red.Amount);
        var c11 = LutRgb.Lerp(At(red.Lower, green.Upper, blue.Upper), At(red.Upper, green.Upper, blue.Upper), red.Amount);
        return LutRgb.Lerp(LutRgb.Lerp(c00, c10, green.Amount), LutRgb.Lerp(c01, c11, green.Amount), blue.Amount);
    }

    private static (int Lower, int Upper, float Amount) Axis(float value, int size)
    {
        var scaled = Math.Clamp(value, 0, 1) * (size - 1); var lower = (int)MathF.Floor(scaled);
        return (lower, Math.Min(lower + 1, size - 1), scaled - lower);
    }

    private static LutRgb Normalize(LutRgb value, LutRgb minimum, LutRgb maximum) => new(
        Math.Clamp((value.Red - minimum.Red) / (maximum.Red - minimum.Red), 0, 1),
        Math.Clamp((value.Green - minimum.Green) / (maximum.Green - minimum.Green), 0, 1),
        Math.Clamp((value.Blue - minimum.Blue) / (maximum.Blue - minimum.Blue), 0, 1));
}

public sealed class JsonLutPresetStore(string root, ILutParser parser) : ILutPresetStore
{
    private readonly string _path = Path.Combine(root, "tether-color-settings.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<TetherColorSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path)) return new([]);
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<TetherColorSettings>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? new([]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new([]); }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(TetherColorSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp-" + Guid.NewGuid().ToString("N");
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken).ConfigureAwait(false); await stream.FlushAsync(cancellationToken).ConfigureAwait(false); stream.Flush(true); }
            File.Move(temporary, _path, true);
        }
        finally { _gate.Release(); }
    }

    public async Task<LutPresetReference> ImportAsync(string path, LutInputInterpretation interpretation = LutInputInterpretation.Unknown, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var parsed = await parser.ParseAsync(fullPath, cancellationToken).ConfigureAwait(false);
        if (!parsed.Success || parsed.Definition is null) throw new InvalidDataException(parsed.Message ?? "LUT验证失败。");
        var settings = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var existing = settings.LutPresets.FirstOrDefault(item => string.Equals(item.NormalizedPath, Normalize(fullPath), StringComparison.OrdinalIgnoreCase));
        var definition = parsed.Definition;
        var preset = new LutPresetReference(existing?.Id ?? Guid.NewGuid(), definition.Title ?? Path.GetFileNameWithoutExtension(fullPath), fullPath, Normalize(fullPath), await FingerprintAsync(fullPath, cancellationToken).ConfigureAwait(false), definition.Kind, definition.Size, definition.DomainMin, definition.DomainMax, existing?.IsFavorite ?? false, DateTimeOffset.UtcNow, LutValidationStatus.Valid, interpretation, DateTimeOffset.UtcNow);
        await SaveAsync(settings with { LutPresets = [.. settings.LutPresets.Where(item => item.Id != preset.Id), preset] }, cancellationToken).ConfigureAwait(false);
        return preset;
    }

    public async Task<LutPresetReference> RelocateAsync(Guid presetId, string path, CancellationToken cancellationToken = default)
    {
        var settings = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var previous = settings.LutPresets.FirstOrDefault(item => item.Id == presetId) ?? throw new KeyNotFoundException();
        var imported = await ImportAsync(path, previous.InputInterpretation, cancellationToken).ConfigureAwait(false);
        var relocated = imported with { Id = previous.Id, DisplayName = previous.DisplayName, IsFavorite = previous.IsFavorite };
        var updated = (await LoadAsync(cancellationToken).ConfigureAwait(false)).LutPresets.Where(item => item.Id != imported.Id && item.Id != previous.Id).Append(relocated).ToArray();
        await SaveAsync(settings with { LutPresets = updated }, cancellationToken).ConfigureAwait(false);
        return relocated;
    }

    public async Task RemoveReferenceAsync(Guid presetId, CancellationToken cancellationToken = default)
    {
        var settings = await LoadAsync(cancellationToken).ConfigureAwait(false);
        await SaveAsync(settings with { LutPresets = settings.LutPresets.Where(item => item.Id != presetId).ToArray(), ProjectDefaultLutId = settings.ProjectDefaultLutId == presetId ? null : settings.ProjectDefaultLutId, SessionDefaultLutId = settings.SessionDefaultLutId == presetId ? null : settings.SessionDefaultLutId }, cancellationToken).ConfigureAwait(false);
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
    public static async Task<string> FingerprintAsync(string path, CancellationToken cancellationToken = default) { await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, true); return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant(); }
}
