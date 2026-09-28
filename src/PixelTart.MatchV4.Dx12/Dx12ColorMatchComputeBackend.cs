using ComputeSharp;
using RAWSelectionAssistant.Core.Services.Projects;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace PixelTart.MatchV4.Dx12;

/// <summary>Windows-only adapter. ComputeSharp types stop at this project boundary.</summary>
[SupportedOSPlatform("windows6.2")]
public sealed partial class Dx12ColorMatchComputeBackend : IColorMatchComputeBackend, IMatchV4ComputeBackend
{
    private readonly Lazy<GpuCapabilityInfo> _capability = new(Detect);
    public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Gpu;
    public GpuCapabilityInfo Capability => _capability.Value;
    public bool IsAvailable => Capability.BackendAvailable;
    GpuCapabilityInfo IMatchV4ComputeBackend.Capability => Capability;

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
}
