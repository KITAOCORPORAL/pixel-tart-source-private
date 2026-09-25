using ComputeSharp;
using System.Runtime.Versioning;

namespace RAWSelectionAssistant.Core.Services.Projects;

[SupportedOSPlatform("windows6.2")]
internal static partial class GpuColorMatchCompute
{
    public static bool TrySmoke(out string? failure)
    {
        try
        {
            var device = GraphicsDevice.GetDefault();
            float[] values = [2, 3, 5];
            using var buffer = device.AllocateReadWriteBuffer(values);
            device.For(values.Length, new AddOneShader(buffer));
            buffer.CopyTo(values);
            var passed = values.SequenceEqual(new float[] { 3, 4, 6 });
            failure = passed ? null : "GPU smoke output mismatch";
            return passed;
        }
        catch (Exception ex) { failure = ex.GetType().Name + ": " + ex.Message; return false; }
    }

    public static (string Name, long DedicatedBytes, long SharedBytes, bool Hardware) DeviceInfo()
    {
        var device = GraphicsDevice.GetDefault();
        return (device.Name, (long)device.DedicatedMemorySize, (long)device.SharedMemorySize, device.IsHardwareAccelerated);
    }

    public static IReadOnlyList<OklabColor> Map(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference,
        ReferenceMatchV4Settings settings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var device = GraphicsDevice.GetDefault();
        var n = source.Count; var m = reference.Count;
        float[] sourceL = source.Select(c => (float)c.L).ToArray();
        float[] sourceA = source.Select(c => (float)c.A).ToArray();
        float[] sourceB = source.Select(c => (float)c.B).ToArray();
        float[] referenceL = reference.Select(c => (float)c.L).ToArray();
        float[] referenceA = reference.Select(c => (float)c.A).ToArray();
        float[] referenceB = reference.Select(c => (float)c.B).ToArray();
        using var sl = device.AllocateReadOnlyBuffer(sourceL); using var sa = device.AllocateReadOnlyBuffer(sourceA); using var sb = device.AllocateReadOnlyBuffer(sourceB);
        using var rl = device.AllocateReadOnlyBuffer(referenceL); using var ra = device.AllocateReadOnlyBuffer(referenceA); using var rb = device.AllocateReadOnlyBuffer(referenceB);
        using var kernel = device.AllocateReadWriteBuffer<float>(checked(n * m));
        device.For(n * m, new PairwiseKernelShader(sl, sa, sb, rl, ra, rb, kernel, m, (float)settings.Regularization));
        var matrix = new float[n * m]; kernel.CopyTo(matrix);
        token.ThrowIfCancellationRequested();
        var u = new double[n]; var v = new double[m]; var a = 1d / n; var b = 1d / m;
        for (var iteration = 0; iteration < settings.SinkhornIterations; iteration++)
        {
            token.ThrowIfCancellationRequested();
            for (var i = 0; i < n; i++) { var sum = 0d; for (var j = 0; j < m; j++) sum += matrix[i * m + j] * Math.Exp(v[j]); u[i] = a / Math.Max(1e-12, sum); }
            for (var j = 0; j < m; j++) { var sum = 0d; for (var i = 0; i < n; i++) sum += matrix[i * m + j] * u[i]; v[j] = Math.Log(b / Math.Max(1e-12, sum)); }
        }
        var mapped = new OklabColor[n];
        for (var i = 0; i < n; i++)
        {
            token.ThrowIfCancellationRequested(); var total = 0d; var l = 0d; var aa = 0d; var bb = 0d;
            for (var j = 0; j < m; j++) { var weight = u[i] * matrix[i * m + j] * Math.Exp(v[j]); total += weight; l += weight * reference[j].L; aa += weight * reference[j].A; bb += weight * reference[j].B; }
            mapped[i] = total <= 1e-12 ? reference[i % m] : new(l / total, aa / total, bb / total);
        }
        return mapped;
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AddOneShader(ReadWriteBuffer<float> buffer) : IComputeShader
    { public void Execute() => buffer[ThreadIds.X] += 1; }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct PairwiseKernelShader(
        ReadOnlyBuffer<float> sourceL, ReadOnlyBuffer<float> sourceA, ReadOnlyBuffer<float> sourceB,
        ReadOnlyBuffer<float> referenceL, ReadOnlyBuffer<float> referenceA, ReadOnlyBuffer<float> referenceB,
        ReadWriteBuffer<float> kernel, int referenceCount, float regularization) : IComputeShader
    {
        public void Execute()
        {
            var index = ThreadIds.X; var i = index / referenceCount; var j = index % referenceCount;
            var dl = sourceL[i] - referenceL[j]; var da = sourceA[i] - referenceA[j]; var db = sourceB[i] - referenceB[j];
            kernel[index] = Hlsl.Exp(-(dl * dl + 1.35f * (da * da + db * db)) / regularization);
        }
    }
}
