using ComputeSharp;
using RAWSelectionAssistant.Core.Services.Projects;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace PixelTart.MatchV4.Dx12;

/// <summary>Windows-only adapter. ComputeSharp types stop at this project boundary.</summary>
[SupportedOSPlatform("windows6.2")]
public sealed partial class Dx12ColorMatchComputeBackend : IColorMatchComputeBackend, IMatchV4ComputeBackend, IMatchV4PixelBackend
{
    private readonly Lazy<GpuCapabilityInfo> _capability = new(Detect);
    private readonly int? _maximumTileEdge;
    public Dx12ColorMatchComputeBackend(int? maximumTileEdge = null)
    {
        if (maximumTileEdge is < 32) throw new ArgumentOutOfRangeException(nameof(maximumTileEdge));
        _maximumTileEdge = maximumTileEdge;
    }
    public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Gpu;
    public GpuCapabilityInfo Capability => _capability.Value;
    public bool IsAvailable => Capability.BackendAvailable;
    GpuCapabilityInfo IMatchV4ComputeBackend.Capability => Capability;

    Task<MatchV4PixelExecutionResult> IMatchV4PixelBackend.ExecuteAsync(
        RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer source,
        MatchV4ResolvedTransform transform,
        CancellationToken token)
    {
        transform = transform.Normalize();
        if (!IsAvailable) throw new NotSupportedException("DX12 device is not available.");
        token.ThrowIfCancellationRequested();
        var device = GraphicsDevice.GetDefault();
        // Never allocate an entire 102MP RGB source and output in VRAM. The transform is
        // pointwise, so tiles need no halo; only a completed image is published to callers.
        var edge = Math.Min(transform.Settings.TileSize, _maximumTileEdge ?? Capability.MemoryBudget.TileSize);
        var values = new float[source.Rgb32.Length];
        var upload = TimeSpan.Zero; var compute = TimeSpan.Zero; var readback = TimeSpan.Zero;
        var tiles = 0;
        for (var y = 0; y < source.Height;)
        {
            var height = Math.Min(edge, source.Height - y);
            var rowCompleted = false;
            while (!rowCompleted)
            {
                rowCompleted = true;
                for (var x = 0; x < source.Width;)
                {
                    token.ThrowIfCancellationRequested();
                    var width = Math.Min(edge, source.Width - x);
                    try
                    {
                        var input = new float[checked(width * height * 3)];
                        for (var row = 0; row < height; row++)
                            source.Rgb32.Span.Slice(((y + row) * source.Width + x) * 3, width * 3)
                                .CopyTo(input.AsSpan(row * width * 3));
                        var watch = Stopwatch.StartNew();
                        using var sourceBuffer = device.AllocateReadOnlyBuffer(input);
                        using var outputBuffer = device.AllocateReadWriteBuffer<float>(input.Length);
                        upload += watch.Elapsed;
                        watch.Restart();
                        var shader = new PixelTransformShader(sourceBuffer, outputBuffer,
                            (float)transform.GlobalDelta.L, (float)transform.GlobalDelta.A, (float)transform.GlobalDelta.B,
                            (float)transform.RegionDeltas[0].L, (float)transform.RegionDeltas[0].A, (float)transform.RegionDeltas[0].B,
                            (float)transform.RegionDeltas[1].L, (float)transform.RegionDeltas[1].A, (float)transform.RegionDeltas[1].B,
                            (float)transform.RegionDeltas[2].L, (float)transform.RegionDeltas[2].A, (float)transform.RegionDeltas[2].B,
                            (float)transform.Strength, transform.KeepLuminance,
                            (float)transform.Settings.MaximumLuminanceDisplacement, (float)transform.Settings.MaximumChromaDisplacement,
                            (float)transform.Settings.NeutralProtection, (float)transform.Settings.SkinProtection,
                            (float)transform.Settings.HighlightProtection, (float)transform.Settings.ShadowProtection);
                        device.For(width * height, shader);
                        compute += watch.Elapsed;
                        token.ThrowIfCancellationRequested();
                        watch.Restart();
                        outputBuffer.CopyTo(input);
                        readback += watch.Elapsed;
                        for (var row = 0; row < height; row++)
                            input.AsSpan(row * width * 3, width * 3)
                                .CopyTo(values.AsSpan(((y + row) * source.Width + x) * 3));
                        x += width; tiles++;
                    }
                    catch (OutOfMemoryException) when (edge > 32)
                    {
                        edge = Math.Max(32, edge / 2);
                        height = Math.Min(edge, source.Height - y);
                        rowCompleted = false;
                        // Restart the row so already copied tiles use one consistent tile geometry.
                        break;
                    }
                }
            }
            y += height;
        }
        var result = new RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer(source.Width, source.Height, values,
            source.SourceBitDepth, source.WorkingColorSpace, source.Orientation, source.Metadata);
        return Task.FromResult(new MatchV4PixelExecutionResult(result, upload, compute, readback, ReferenceMatchV4BackendKind.Gpu, TileCount: tiles));
    }

    public IReadOnlyList<OklabColor> Map(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference,
        ReferenceMatchV4Settings settings, CancellationToken token = default) =>
        ExecuteAsync(source, reference, settings, token).GetAwaiter().GetResult().MappedSamples;

    public Task<MatchV4BackendResult> ExecuteAsync(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference,
        ReferenceMatchV4Settings settings, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var upload = Stopwatch.StartNew();
        var device = GraphicsDevice.GetDefault();
        var sourceL = source.Select(c => (float)c.L).ToArray(); var sourceA = source.Select(c => (float)c.A).ToArray(); var sourceB = source.Select(c => (float)c.B).ToArray();
        var referenceL = reference.Select(c => (float)c.L).ToArray(); var referenceA = reference.Select(c => (float)c.A).ToArray(); var referenceB = reference.Select(c => (float)c.B).ToArray();
        using var sl = device.AllocateReadOnlyBuffer(sourceL); using var sa = device.AllocateReadOnlyBuffer(sourceA); using var sb = device.AllocateReadOnlyBuffer(sourceB);
        using var rl = device.AllocateReadOnlyBuffer(referenceL); using var ra = device.AllocateReadOnlyBuffer(referenceA); using var rb = device.AllocateReadOnlyBuffer(referenceB);
        using var kernel = device.AllocateReadWriteBuffer<float>(checked(source.Count * reference.Count));
        upload.Stop();
        var compute = Stopwatch.StartNew();
        device.For(checked(source.Count * reference.Count), new PairwiseKernelShader(sl, sa, sb, rl, ra, rb, kernel, reference.Count, (float)settings.Regularization));
        compute.Stop();
        var readback = Stopwatch.StartNew(); var matrix = new float[source.Count * reference.Count]; kernel.CopyTo(matrix); readback.Stop();
        token.ThrowIfCancellationRequested();
        var mapped = CpuMap(matrix, source, reference, settings, token);
        return Task.FromResult(new MatchV4BackendResult(mapped, upload.Elapsed, compute.Elapsed, readback.Elapsed));
    }

    private static GpuCapabilityInfo Detect()
    {
        try
        {
            var device = GraphicsDevice.GetDefault();
            float[] values = [2, 3, 5]; using var buffer = device.AllocateReadWriteBuffer(values); device.For(3, new AddOneShader(buffer)); buffer.CopyTo(values);
            var smoke = values.SequenceEqual(new float[] { 3, 4, 6 });
            var bytes = (long)device.DedicatedMemorySize;
            return new(device.Name, bytes, (long)device.SharedMemorySize, string.Empty, "DX12", "ComputeSharp-DX12", device.IsHardwareAccelerated && smoke, true, smoke,
                smoke ? null : "DX12 smoke test failed", GpuMemoryBudget.FromBytes(bytes, (long)device.SharedMemorySize));
        }
        catch (Exception ex) { return new("Unavailable", 0, 0, string.Empty, "DX12", "ComputeSharp-DX12", false, false, false, ex.GetType().Name + ": " + ex.Message, GpuMemoryBudget.FromBytes(0)); }
    }

    private static IReadOnlyList<OklabColor> CpuMap(float[] matrix, IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference, ReferenceMatchV4Settings settings, CancellationToken token)
    {
        var n = source.Count; var m = reference.Count; var u = new double[n]; var v = new double[m]; var a = 1d / n; var b = 1d / m;
        for (var iteration = 0; iteration < settings.SinkhornIterations; iteration++) { token.ThrowIfCancellationRequested(); for (var i = 0; i < n; i++) { var sum = 0d; for (var j = 0; j < m; j++) sum += matrix[i * m + j] * Math.Exp(v[j]); u[i] = a / Math.Max(1e-12, sum); } for (var j = 0; j < m; j++) { var sum = 0d; for (var i = 0; i < n; i++) sum += matrix[i * m + j] * u[i]; v[j] = Math.Log(b / Math.Max(1e-12, sum)); } }
        var mapped = new OklabColor[n]; for (var i = 0; i < n; i++) { token.ThrowIfCancellationRequested(); var total = 0d; var l = 0d; var aa = 0d; var bb = 0d; for (var j = 0; j < m; j++) { var weight = u[i] * matrix[i * m + j] * Math.Exp(v[j]); total += weight; l += weight * reference[j].L; aa += weight * reference[j].A; bb += weight * reference[j].B; } mapped[i] = total <= 1e-12 ? reference[i % m] : new(l / total, aa / total, bb / total); }
        return mapped;
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)] [GeneratedComputeShaderDescriptor] public readonly partial struct AddOneShader(ReadWriteBuffer<float> buffer) : IComputeShader { public void Execute() => buffer[ThreadIds.X] += 1; }
    [ThreadGroupSize(DefaultThreadGroupSizes.X)] [GeneratedComputeShaderDescriptor] public readonly partial struct PairwiseKernelShader(ReadOnlyBuffer<float> sl, ReadOnlyBuffer<float> sa, ReadOnlyBuffer<float> sb, ReadOnlyBuffer<float> rl, ReadOnlyBuffer<float> ra, ReadOnlyBuffer<float> rb, ReadWriteBuffer<float> output, int referenceCount, float regularization) : IComputeShader { public void Execute() { var index = ThreadIds.X; var i = index / referenceCount; var j = index % referenceCount; var dl = sl[i] - rl[j]; var da = sa[i] - ra[j]; var db = sb[i] - rb[j]; output[index] = Hlsl.Exp(-(dl * dl + 1.35f * (da * da + db * db)) / regularization); } }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct PixelTransformShader(
        ReadOnlyBuffer<float> source, ReadWriteBuffer<float> output,
        float globalL, float globalA, float globalB,
        float shadowL, float shadowA, float shadowB,
        float midL, float midA, float midB,
        float highlightL, float highlightA, float highlightB,
        float strength, bool keepLuminance, float maxLuma, float maxChroma,
        float neutralProtection, float skinProtection, float highlightProtection, float shadowProtection) : IComputeShader
    {
        public void Execute()
        {
            var pixel = ThreadIds.X; var offset = pixel * 3;
            var r = source[offset]; var g = source[offset + 1]; var b = source[offset + 2];
            var lr = Decode(r); var lg = Decode(g); var lb = Decode(b);
            var l = Cbrt(.4122214708f * lr + .5363325363f * lg + .0514459929f * lb);
            var m = Cbrt(.2119034982f * lr + .6806995451f * lg + .1073969566f * lb);
            var s = Cbrt(.0883024619f * lr + .2817188376f * lg + .6299787005f * lb);
            var L = .2104542553f * l + .7936177850f * m - .0040720468f * s;
            var A = 1.9779984951f * l - 2.4285922050f * m + .4505937099f * s;
            var B = .0259040371f * l + .7827717662f * m - .8086757660f * s;
            var zone = L < .3333333f ? 0 : L < .6666667f ? 1 : 2;
            var localL = zone == 0 ? shadowL : zone == 1 ? midL : highlightL;
            var localA = zone == 0 ? shadowA : zone == 1 ? midA : highlightA;
            var localB = zone == 0 ? shadowB : zone == 1 ? midB : highlightB;
            var weight = zone == 0 ? 1 - Hlsl.Clamp((L - .2f) / .2f, 0, 1) : zone == 2 ? Hlsl.Clamp((L - .6f) / .2f, 0, 1) : 1;
            var dL = globalL * (1 - weight) + localL * weight;
            var dA = globalA * (1 - weight) + localA * weight;
            var dB = globalB * (1 - weight) + localB * weight;
            var chroma = Hlsl.Sqrt(A * A + B * B);
            var neutral = Hlsl.Clamp(1 - chroma / .08f, 0, 1);
            var hue = Hlsl.Atan2(B, A) * 57.2957795f; if (hue < 0) hue += 360;
            var skin = L > .28f && L < .9f && hue > 25 && hue < 80 && chroma > .025f && chroma < .22f;
            var hi = Hlsl.Clamp((L - .82f) / .18f, 0, 1);
            var sh = Hlsl.Clamp((.2f - L) / .2f, 0, 1);
            var protection = Hlsl.Clamp(1 - neutral * neutralProtection - (skin ? skinProtection : 0) - hi * highlightProtection - sh * Hlsl.Clamp(chroma / .1f, 0, 1) * shadowProtection, .08f, 1);
            var amount = strength * protection;
            var outL = keepLuminance ? L : Hlsl.Clamp(L + Hlsl.Clamp(dL, -maxLuma, maxLuma) * amount, 0, 1);
            var outA = A + Hlsl.Clamp(dA, -maxChroma, maxChroma) * amount;
            var outB = B + Hlsl.Clamp(dB, -maxChroma, maxChroma) * amount;
            var ll = outL + .3963377774f * outA + .2158037573f * outB;
            var mm = outL - .1055613458f * outA - .0638541728f * outB;
            var ss = outL - .0894841775f * outA - 1.2914855480f * outB;
            ll *= ll * ll; mm *= mm * mm; ss *= ss * ss;
            var rr = 4.0767416621f * ll - 3.3077115913f * mm + .2309699292f * ss;
            var gg = -1.2684380046f * ll + 2.6097574011f * mm - .3413193965f * ss;
            var bb = -.0041960863f * ll - .7034186147f * mm + 1.7076147010f * ss;
            output[offset] = Encode(rr); output[offset + 1] = Encode(gg); output[offset + 2] = Encode(bb);
        }
        private static float Cbrt(float value) => value < 0 ? -Hlsl.Pow(-value, 0.33333334f) : Hlsl.Pow(value, 0.33333334f);
        private static float Decode(float value) => value <= .04045f ? value / 12.92f : Hlsl.Pow((value + .055f) / 1.055f, 2.4f);
        private static float Encode(float value) { value = Hlsl.Clamp(value, 0, 1); return value <= .0031308f ? 12.92f * value : 1.055f * Hlsl.Pow(value, .41666667f) - .055f; }
    }
}
