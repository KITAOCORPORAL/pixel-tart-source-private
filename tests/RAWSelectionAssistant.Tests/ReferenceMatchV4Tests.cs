using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ReferenceMatchV4Tests
{
    [TestMethod]
    public void CpuFoundationIsDeterministicAndUsesBoundedSamples()
    {
        var source = Fixture(96, 64, 35, 70, 120); var reference = Fixture(96, 64, 180, 125, 70);
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 64, SinkhornIterations: 8, ResidualIterations: 1);
        var first = new ReferenceMatchV4Engine().Match(source, reference, settings: settings, preferGpu: false);
        var second = new ReferenceMatchV4Engine().Match(source, reference, settings: settings, preferGpu: false);
        CollectionAssert.AreEqual(first.Pixels.Rgb24.ToArray(), second.Pixels.Rgb24.ToArray());
        Assert.IsLessThanOrEqualTo(64, first.RepresentativeSourceCount);
        Assert.AreEqual(ReferenceMatchV4BackendKind.Cpu, first.Backend);
    }

    [TestMethod]
    public void UnavailableGpuFallsBackToCpuAndCacheKeyTracksSettings()
    {
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 32);
        var result = new ReferenceMatchV4Engine(gpu: new UnavailableGpu()).Match(Fixture(12, 8, 20, 20, 20), Fixture(12, 8, 220, 180, 100),
            sourceIdentity: "a", referenceIdentity: "b", settings: settings, preferGpu: true);
        Assert.AreEqual(ReferenceMatchV4BackendKind.Cpu, result.Backend);
        Assert.IsTrue(result.UsedCpuFallback);
        StringAssert.Contains(result.CacheKey, "a|b|v4|");
        Assert.AreNotEqual(result.CacheKey, ReferenceMatchV4Cache.CreateKey("a", "b", settings with { TileSize = 256 }));
        Assert.AreNotEqual(result.CacheKey, ReferenceMatchV4Cache.CreateKey("a", "b", settings with { ComputeQuality = ReferenceMatchV4ComputeQuality.High }));
    }

    [TestMethod]
    public void CancellationStopsOtAndTilePlannerProvidesOverlap()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => new ReferenceMatchV4Engine().Match(Fixture(64, 64, 1, 2, 3), Fixture(64, 64, 220, 200, 180), token: cancellation.Token));
        var tiles = ColorMatchTilePlanner.Plan(1024, 768, new ReferenceMatchV4Settings(TileSize: 256, TileOverlap: 8));
        Assert.IsGreaterThan(1, tiles.Count); Assert.IsTrue(tiles.Any(tile => tile.SourceX < tile.X || tile.SourceY < tile.Y));
    }

    [TestMethod]
    public void ProtectionAndGamutMappingStayFinite()
    {
        var result = new ReferenceMatchV4Engine().Match(Fixture(32, 32, 128, 128, 128), Fixture(32, 32, 255, 0, 255),
            settings: new ReferenceMatchV4Settings(MaximumChromaDisplacement: .02, SinkhornIterations: 4), preferGpu: false);
        Assert.IsTrue(result.Pixels.Rgb24.ToArray().All(value => value <= 255));
        Assert.IsLessThanOrEqualTo(2, result.ResidualIterations);
        Assert.HasCount(32 * 32 * 3, result.Pixels.Rgb24.ToArray());
    }

    [TestMethod]
    public void CapabilityDetectionRequiresValidatedSmoke()
    {
        var capability = GpuCapabilityDetector.Detect();
        Assert.AreEqual(capability.SmokeTestPassed, capability.BackendAvailable);
        Assert.AreEqual(capability.SmokeTestPassed, capability.DeviceCreated);
        Assert.AreEqual(capability.SmokeTestPassed, string.IsNullOrWhiteSpace(capability.FailureReason));
    }

    [TestMethod]
    public void MemoryTiersOnlyChangeBudgetNotAlgorithmSemantics()
    {
        var low = GpuMemoryBudget.FromBytes(8L * 1024 * 1024 * 1024 - 1);
        var ultra = GpuMemoryBudget.FromBytes(16L * 1024 * 1024 * 1024);
        Assert.AreEqual("STANDARD", low.Tier);
        Assert.AreEqual("ULTRA", ultra.Tier);
        Assert.IsGreaterThan(low.RepresentativeSampleBudget, ultra.RepresentativeSampleBudget);
        Assert.AreEqual(ReferenceMatchV4ComputeQuality.Ultra, ReferenceMatchV4Settings.ForQuality(ReferenceMatchV4ComputeQuality.Ultra, ultra).ComputeQuality);
    }

    [TestMethod]
    public void CpuGpuParityOnSyntheticScene()
    {
        if (!GpuCapabilityDetector.Detect().BackendAvailable) Assert.Inconclusive("No validated GPU compute device on this host.");
        var source = Fixture(64, 48, 72, 98, 130); var reference = Fixture(64, 48, 190, 140, 82);
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 64, SinkhornIterations: 16, ResidualIterations: 0);
        var cpu = new ReferenceMatchV4Engine().Match(source, reference, settings: settings, preferGpu: false);
        var gpu = new ReferenceMatchV4Engine().Match(source, reference, settings: settings, preferGpu: true);
        Assert.AreEqual(ReferenceMatchV4BackendKind.Gpu, gpu.Backend);
        var cpuBytes = cpu.Pixels.Rgb24.Span; var gpuBytes = gpu.Pixels.Rgb24.Span;
        var maximum = 0; var sum = 0d;
        for (var i = 0; i < cpuBytes.Length; i++) { var delta = Math.Abs(cpuBytes[i] - gpuBytes[i]); maximum = Math.Max(maximum, delta); sum += delta; }
        Assert.IsLessThanOrEqualTo(3, maximum);
        Assert.IsLessThanOrEqualTo(.3, sum / cpuBytes.Length);
    }

    [TestMethod]
    public void CpuGpuParityAcrossEightSceneClasses()
    {
        if (!GpuCapabilityDetector.Detect().BackendAvailable) Assert.Inconclusive("No validated GPU compute device on this host.");
        var classes = new (byte R, byte G, byte B)[]
        {
            (128, 128, 128), (190, 130, 105), (240, 240, 235), (18, 22, 31),
            (245, 25, 110), (80, 90, 85), (50, 110, 210), (120, 170, 55)
        };
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 96, SinkhornIterations: 24, ResidualIterations: 1);
        var rgbMax = 0; var rgbSum = 0d; var oklabMax = 0d; var oklabSum = 0d; var channels = 0; var pixels = 0;
        foreach (var scene in classes)
        {
            var source = Fixture(48, 32, scene.R, scene.G, scene.B);
            var reference = Fixture(48, 32, (byte)(255 - scene.R), (byte)(255 - scene.G), (byte)(255 - scene.B));
            var cpu = new ReferenceMatchV4Engine().Match(source, reference, settings: settings, preferGpu: false);
            var gpu = new ReferenceMatchV4Engine().Match(source, reference, settings: settings, preferGpu: true);
            Assert.AreEqual(ReferenceMatchV4BackendKind.Gpu, gpu.Backend);
            var maximum = 0; var sum = 0d;
            for (var i = 0; i < cpu.Pixels.Rgb24.Length; i++)
            {
                var delta = Math.Abs(cpu.Pixels.Rgb24.Span[i] - gpu.Pixels.Rgb24.Span[i]); maximum = Math.Max(maximum, delta); sum += delta;
            }
            rgbMax = Math.Max(rgbMax, maximum); rgbSum += sum; channels += cpu.Pixels.Rgb24.Length;
            for (var i = 0; i < cpu.Pixels.PixelCount; i++)
            {
                var o = i * 3; var a = OklabColorSpace.FromSrgb(new(cpu.Pixels.Rgb24.Span[o], cpu.Pixels.Rgb24.Span[o + 1], cpu.Pixels.Rgb24.Span[o + 2]));
                var b = OklabColorSpace.FromSrgb(new(gpu.Pixels.Rgb24.Span[o], gpu.Pixels.Rgb24.Span[o + 1], gpu.Pixels.Rgb24.Span[o + 2]));
                var distance = Math.Sqrt(Math.Pow(a.L - b.L, 2) + Math.Pow(a.A - b.A, 2) + Math.Pow(a.B - b.B, 2));
                oklabMax = Math.Max(oklabMax, distance); oklabSum += distance; pixels++;
            }
            Assert.IsLessThanOrEqualTo(3, maximum);
            Assert.IsLessThanOrEqualTo(.3, sum / cpu.Pixels.Rgb24.Length);
        }
        Console.WriteLine($"V4_PARITY RGB_MAX={rgbMax} RGB_MEAN={rgbSum / channels:R} OKLAB_MAX={oklabMax:R} OKLAB_MEAN={oklabSum / pixels:R}");
    }

    [TestMethod]
    public void GpuExecutionFailureFallsBackToCpu()
    {
        var engine = new ReferenceMatchV4Engine(gpu: new ThrowingGpu());
        var result = engine.Match(Fixture(24, 16, 40, 70, 110), Fixture(24, 16, 180, 120, 80),
            settings: new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 32), preferGpu: true);
        Assert.AreEqual(ReferenceMatchV4BackendKind.Cpu, result.Backend);
        Assert.IsTrue(result.UsedCpuFallback);
        Assert.AreEqual(GpuFailureReason.ExecutionFailure, result.GpuFailure);
        Assert.AreEqual(GpuFallbackStage.Dispatch, result.FallbackStage);
    }

    private static VisualPixelBuffer Fixture(int width, int height, byte r, byte g, byte b)
    {
        var bytes = new byte[width * height * 3];
        for (var i = 0; i < bytes.Length; i += 3) { bytes[i] = (byte)Math.Clamp(r + (i / 3) % 17, 0, 255); bytes[i + 1] = (byte)Math.Clamp(g + (i / 5) % 13, 0, 255); bytes[i + 2] = (byte)Math.Clamp(b + (i / 7) % 11, 0, 255); }
        return new(width, height, bytes);
    }

    private sealed class UnavailableGpu : IColorMatchComputeBackend
    {
        public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Gpu;
        public bool IsAvailable => false;
        public IReadOnlyList<OklabColor> Map(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference, ReferenceMatchV4Settings settings, CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingGpu : IColorMatchComputeBackend
    {
        public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Gpu;
        public bool IsAvailable => true;
        public IReadOnlyList<OklabColor> Map(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference, ReferenceMatchV4Settings settings, CancellationToken token = default) => throw new InvalidOperationException("synthetic device lost");
    }
}
