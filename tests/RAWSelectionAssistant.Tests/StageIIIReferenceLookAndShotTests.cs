using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Tethering;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ReferenceLookModelTests
{
    [TestMethod]
    public void MultipleReferenceWeightsAreNormalizedWithoutLosingSources()
    {
        var (look, _, _) = Fixtures.Look(40, 30, 20, 10);
        var normalized = look.Normalize();
        Assert.HasCount(4, normalized.ReferenceSources);
        Assert.AreEqual(1, normalized.ReferenceSources.Sum(source => source.Weight), 1e-12);
        CollectionAssert.AreEqual(new[] { .4, .3, .2, .1 }, normalized.ReferenceSources.Select(source => source.Weight).ToArray());
    }

    [TestMethod]
    public void InvalidStrengthIsRejected()
    {
        var (look, _, _) = Fixtures.Look(1);
        Assert.Throws<ArgumentException>(() => (look with { Parameters = look.Parameters with { MatchStrength = 101 } }).Normalize());
    }
}

[TestClass]
public sealed class ReferenceLookPersistenceTests
{
    [TestMethod]
    public async Task SavedLookAndProjectDefaultSurviveReopen()
    {
        var root = Fixtures.Temp();
        try
        {
            var (look, _, _) = Fixtures.Look(1); var store = new ReferenceLookStore(root);
            await store.SaveAsync(look, projectDefault: true);
            var reopened = await new ReferenceLookStore(root).LoadAsync();
            Assert.AreEqual(look.ReferenceLookId, reopened.Looks.Single().ReferenceLookId);
            Assert.AreEqual(look.ReferenceLookId, reopened.ProjectDefaults[look.ProjectId!.Value]);
        }
        finally { Directory.Delete(root, true); }
    }
}

[TestClass]
public sealed class ReferenceLookStrengthTests
{
    [TestMethod]
    public void ZeroMatchStrengthReturnsExactSourceBytes()
    {
        var (look, source, analysis) = Fixtures.Look(1);
        var result = new ReferenceLookMatcher().Match(source, analysis, look with { Parameters = look.Parameters with { MatchStrength = 0 } });
        CollectionAssert.AreEqual(source.Rgb24.ToArray(), result.Preview.Rgb24.ToArray());
    }

    [TestMethod]
    public void ZeroToneStrengthDoesNotApplyReferenceToneCurve()
    {
        var (look, source, analysis) = Fixtures.Look(1);
        var result = new ReferenceLookMatcher().Match(source, analysis, look with
        {
            Parameters = new(MatchStrength: 100, ToneStrength: 0, ColorStrength: 0, ContrastStrength: 100, SaturationStrength: 0)
        });
        CollectionAssert.AreEqual(source.Rgb24.ToArray(), result.Preview.Rgb24.ToArray());
    }
}

[TestClass]
public sealed class ReferenceColorMatchTests
{
    [TestMethod]
    public void MatchIsDeterministicAndDoesNotMutateInput()
    {
        var (look, source, analysis) = Fixtures.Look(1); var before = source.Rgb24.ToArray(); var matcher = new ReferenceLookMatcher();
        var first = matcher.Match(source, analysis, look).Preview.Rgb24.ToArray();
        var second = matcher.Match(source, analysis, look).Preview.Rgb24.ToArray();
        CollectionAssert.AreEqual(first, second);
        CollectionAssert.AreEqual(before, source.Rgb24.ToArray());
        Assert.IsTrue(first.Where((value, index) => value != before[index]).Any());
    }

    [TestMethod]
    public async Task SourceFileHashIsUnchangedByPreviewMatching()
    {
        var root = Fixtures.Temp(); var path = Path.Combine(root, "source.jpg");
        try
        {
            var (look, source, analysis) = Fixtures.Look(1); await File.WriteAllBytesAsync(path, source.Rgb24.ToArray());
            var before = SHA256.HashData(await File.ReadAllBytesAsync(path));
            _ = new ReferenceLookMatcher().Match(source, analysis, look);
            var after = SHA256.HashData(await File.ReadAllBytesAsync(path));
            CollectionAssert.AreEqual(before, after);
        }
        finally { Directory.Delete(root, true); }
    }
}

[TestClass]
public sealed class ShotPersistenceTests
{
    [TestMethod]
    public async Task ShotReferencesAndPoseStateSurviveReopen()
    {
        var root = Fixtures.Temp();
        try
        {
            var shot = Fixtures.Shot(); var store = new ProjectShotStore(root); await store.SaveAsync(shot);
            var loaded = (await new ProjectShotStore(root).LoadAsync(shot.ProjectId)).Shots.Single();
            Assert.AreEqual(ShotReferenceKind.Lighting, loaded.References[0].Kind);
            Assert.AreEqual(PoseExecutionStatus.Current, loaded.References[1].PoseStatus);
        }
        finally { Directory.Delete(root, true); }
    }
}

[TestClass]
public sealed class ShotSwitchAndPoseTests
{
    [TestMethod]
    public void SwitchingShotUpdatesLookAndFallbackIsExplicit()
    {
        var first = Fixtures.Shot(); var second = Fixtures.Shot(order: 2) with { ReferenceLookId = null }; var fallback = Guid.NewGuid();
        var execution = new ProjectShotExecution(); execution.Load([first, second]);
        Assert.AreEqual(first.ReferenceLookId, execution.EffectiveLookId(fallback));
        execution.Move(1);
        Assert.AreEqual(second.ShotId, execution.Current!.ShotId);
        Assert.AreEqual(fallback, execution.EffectiveLookId(fallback));
    }

    [TestMethod]
    public void CompletingPoseCanAdvanceAndReferencesCanBePinned()
    {
        var shot = Fixtures.Shot(); var poses = shot.References.Where(reference => reference.Kind == ShotReferenceKind.Pose).ToArray();
        var execution = new ProjectShotExecution(); execution.Load([shot]);
        execution.SetPoseStatus(poses[0].ReferenceId, PoseExecutionStatus.Completed, advance: true);
        Assert.AreEqual(PoseExecutionStatus.Completed, execution.Current!.References.Single(reference => reference.ReferenceId == poses[0].ReferenceId).PoseStatus);
        Assert.AreEqual(PoseExecutionStatus.Current, execution.Current.References.Single(reference => reference.ReferenceId == poses[1].ReferenceId).PoseStatus);
        execution.SetPinned(shot.References[0].ReferenceId, true);
        Assert.IsTrue(execution.Current.References[0].IsPinned);
    }
}

[TestClass]
public sealed class TetherCapabilityVisibilityTests
{
    [TestMethod]
    public void WatchFolderExposesNoCameraControlClaims()
    {
        ITetherCameraCapabilities capabilities = new DefaultCameraCapabilityService().GetCapabilities(CameraProviderType.WatchFolder);
        Assert.IsFalse(capabilities.CanRemoteCapture); Assert.IsFalse(capabilities.CanFocus); Assert.IsFalse(capabilities.CanReadExposure);
        Assert.IsFalse(capabilities.CanSetIso); Assert.IsFalse(capabilities.CanSetShutter); Assert.IsFalse(capabilities.CanSetAperture);
        Assert.IsFalse(capabilities.CanSetWhiteBalance); Assert.IsFalse(capabilities.CanReportBattery); Assert.IsFalse(capabilities.CanReportStorage);
    }
}

[TestClass]
public sealed class StageIIIReferenceStressTests
{
    [TestMethod]
    public void RapidLookAndShotStateTwentyRoundsRemainLatestAndDeterministic()
    {
        var (look, source, analysis) = Fixtures.Look(4, 3, 2, 1); var matcher = new ReferenceLookMatcher(); byte[]? last = null;
        var execution = new ProjectShotExecution(); var shots = Enumerable.Range(0, 5).Select(index => Fixtures.Shot(index)).ToArray(); execution.Load(shots);
        for (var round = 0; round < 20; round++)
        {
            var strength = round * 5d;
            last = matcher.Match(source, analysis, look with { Parameters = look.Parameters with { MatchStrength = strength } }).Preview.Rgb24.ToArray();
            execution.Select(shots[round % shots.Length].ShotId);
            execution.SetPinned(execution.Current!.References[0].ReferenceId, round % 2 == 0);
        }
        var expected = matcher.Match(source, analysis, look with { Parameters = look.Parameters with { MatchStrength = 95 } }).Preview.Rgb24.ToArray();
        CollectionAssert.AreEqual(expected, last);
        Assert.AreEqual(shots[4].ShotId, execution.Current!.ShotId);
    }
}

internal static class Fixtures
{
    public static string Temp() { var path = Path.Combine(Path.GetTempPath(), "PixelTart-StageIII", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    public static (ReferenceLook Look, VisualPixelBuffer Source, AssetVisualAnalysisResult Analysis) Look(params double[] weights)
    {
        var sourceBytes = Enumerable.Range(0, 256).SelectMany(value => new[] { (byte)value, (byte)Math.Clamp(value / 2 + 32, 0, 255), (byte)(255 - value) }).ToArray();
        var source = new VisualPixelBuffer(16, 16, sourceBytes); var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "source", source));
        var references = weights.Select((weight, index) =>
        {
            var bytes = Enumerable.Range(0, 256).SelectMany(value => new[] { (byte)Math.Clamp(220 - value / 2 + index, 0, 255), (byte)Math.Clamp(value + index, 0, 255), (byte)Math.Clamp(40 + value / 3, 0, 255) }).ToArray();
            var pixels = new VisualPixelBuffer(16, 16, bytes); var target = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), $"target-{index}", pixels));
            return new ReferenceLookSource(Guid.NewGuid(), target.AssetId, $"hash-{index}", $"source-{index}", $"hash-{index}", weight, target);
        }).ToArray();
        var project = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        return (new(Guid.NewGuid(), "Look", project, references, new(), now, now), source, analysis);
    }
    public static ProjectShot Shot(int order = 1, Guid? lookId = default)
    {
        var project = StableProject; var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), project, order, $"Shot {order}", ProjectShotStatus.NotStarted, "现场备注", lookId ?? Guid.NewGuid(),
        [
            new(Guid.NewGuid(), ShotReferenceKind.Lighting, Guid.NewGuid(), Guid.NewGuid(), Title: "灯位 A", Details: new Dictionary<string,string>{{"角度","45°"}}),
            new(Guid.NewGuid(), ShotReferenceKind.Pose, Guid.NewGuid(), Guid.NewGuid(), Title: "姿势 01", PoseStatus: PoseExecutionStatus.Current),
            new(Guid.NewGuid(), ShotReferenceKind.Pose, Guid.NewGuid(), Guid.NewGuid(), Title: "姿势 02")
        ], now, now);
    }
    private static readonly Guid StableProject = Guid.NewGuid();
}
