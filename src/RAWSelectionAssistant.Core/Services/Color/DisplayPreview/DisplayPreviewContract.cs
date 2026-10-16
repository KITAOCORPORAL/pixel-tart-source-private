using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Services.Color.DisplayPreview;

/// <summary>Display-only experimental boundary. No editing buffer, recipe or export API accepts this type.</summary>
public sealed record DisplayPreviewContract(
    string Version = "DisplaySrgb16ToMonitorV1",
    string SourceDomain = "EncodedSrgbD65Rgb16Opaque",
    string DestinationDomain = "MonitorDeviceRgb16Opaque",
    int RenderingIntent = 1,
    bool BlackPointCompensation = false,
    double AdaptationState = 1,
    string OutOfRangePolicy = "RejectBeforeQuantization",
    string AlphaPolicy = "OpaqueOnly",
    string PhysicalAcceptance = "NOT_RUN_EXPERIMENTAL")
{
    public DisplayPreviewContract Validate()
    {
        if (Version != "DisplaySrgb16ToMonitorV1" || SourceDomain != "EncodedSrgbD65Rgb16Opaque" ||
            DestinationDomain != "MonitorDeviceRgb16Opaque" || RenderingIntent != 1 || BlackPointCompensation ||
            AdaptationState != 1 || OutOfRangePolicy != "RejectBeforeQuantization" || AlphaPolicy != "OpaqueOnly" ||
            PhysicalAcceptance != "NOT_RUN_EXPERIMENTAL")
            throw new NotSupportedException("Unreviewed experimental display contract; no silent fallback or precision reduction.");
        return this;
    }
}

public sealed record DisplayPreviewKey(string Device, string SourceProfileSha256, string MonitorProfileSha256,
    string SourcePixelsSha256, int Width, int Height, string EngineIdentity, DisplayPreviewContract Contract);
public sealed record DisplayPreviewResult(string State, ReadOnlyMemory<ushort> Pixels, DisplayPreviewKey? Key,
    string Scope = "Experimental display output only; never editing or export pixels");

public interface IDisplayPreviewTransform
{
    string EngineIdentity { get; }
    ushort[] Convert(ReadOnlySpan<ushort> encodedSrgb, ReadOnlySpan<byte> sourceProfile,
        ReadOnlySpan<byte> monitorProfile, DisplayPreviewContract contract, CancellationToken token);
}

/// <summary>Single bounded entry; immutable defensive copies and content identity prevent stale cross-monitor/tile reuse.</summary>
public sealed class DisplayPreviewCache(IDisplayPreviewTransform transform, long maximumBytes = 64L * 1024 * 1024)
{
    private readonly object gate = new();
    private DisplayPreviewKey? key;
    private ushort[]? pixels;
    public void Invalidate() { lock (gate) { key = null; pixels = null; } }
    public DisplayPreviewResult Convert(bool explicitExperimentalOptIn, string device, ReadOnlySpan<ushort> source,
        int width, int height, ReadOnlySpan<byte> sourceProfile, ReadOnlySpan<byte> monitorProfile,
        DisplayPreviewContract? contract = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!explicitExperimentalOptIn) return new("DisabledOriginalSrgbUnchanged", ReadOnlyMemory<ushort>.Empty, null);
        contract = (contract ?? new()).Validate();
        if (string.IsNullOrWhiteSpace(device)) return new("NoMonitorUncalibrated", ReadOnlyMemory<ushort>.Empty, null);
        if (sourceProfile.IsEmpty || monitorProfile.IsEmpty) return new("MissingProfileUncalibrated", ReadOnlyMemory<ushort>.Empty, null);
        if (width <= 0 || height <= 0 || (long)width * height * 3 != source.Length)
            throw new ArgumentException("Exact opaque RGB16 source geometry required.");
        if (maximumBytes < 0 || (long)source.Length * sizeof(ushort) > maximumBytes)
            return new("DisplayBudgetRefused", ReadOnlyMemory<ushort>.Empty, null);
        static string Hash(ReadOnlySpan<byte> value) => ConvertHex(SHA256.HashData(value));
        var next = new DisplayPreviewKey(device, Hash(sourceProfile), Hash(monitorProfile),
            Hash(System.Runtime.InteropServices.MemoryMarshal.AsBytes(source)), width, height, transform.EngineIdentity, contract);
        lock (gate)
        {
            token.ThrowIfCancellationRequested();
            if (key == next && pixels is not null) return new("ExperimentalCacheHit", pixels.ToArray(), key);
            // Never allow a backend to modify caller memory or to return an aliased source.
            var copiedSource = source.ToArray();
            var result = transform.Convert(copiedSource, sourceProfile.ToArray(), monitorProfile.ToArray(), contract, token);
            token.ThrowIfCancellationRequested();
            if (result.Length != source.Length) throw new InvalidDataException("Display transform returned incomplete pixels.");
            pixels = result.ToArray(); key = next;
            return new("ExperimentalConvertedNotPhysicallyAccepted", pixels.ToArray(), key);
        }
    }
    private static string ConvertHex(byte[] value) => System.Convert.ToHexString(value);
}
