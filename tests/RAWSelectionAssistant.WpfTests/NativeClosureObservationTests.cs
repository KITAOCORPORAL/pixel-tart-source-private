using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

// Automated instrumentation contracts only: these tests never count as Native Pointer PASS.
[TestClass]
public sealed class NativeClosureObservationTests
{
    [TestMethod]
    public void LockedObserverResponsePreservesPreviousEvidenceAndRetriesWithoutThrowing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pixel-tart-observer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "response.json");
        var method = typeof(RAWSelectionAssistant.Services.ColorStudioAcceptanceFixture)
            .GetMethod("TryPublishNativeObservation", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            File.WriteAllText(path, "{\"Nonce\":\"before\"}");
            using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.IsFalse((bool)method.Invoke(null, [path, "{\"Nonce\":\"after\"}"])!);
                Assert.AreEqual("{\"Nonce\":\"before\"}", File.ReadAllText(path));
            }
            Assert.IsTrue((bool)method.Invoke(null, [path, "{\"Nonce\":\"after\"}"])!);
            Assert.AreEqual("{\"Nonce\":\"after\"}", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void ThreeDObservationIsReadOnlyAndReportsRealRenderWork() => Sta(() =>
    {
        var cloud = new ColorSpaceCloud(1, 1, 1, 1, [new(new(.5, 0, 0), new VisualRgb24(128, 128, 128), 0, 0)], "fixture", new());
        var model = new ColorSpaceVisualizationModel(cloud, cloud, cloud, [], ColorCloudMode.Overlay, ColorSpaceSamplingTier.Preview, "fixture");
        var viewport = new ColorSpace3DViewport { Width = 320, Height = 220 };
        viewport.SetModel(model);
        var state = viewport.State;
        viewport.Measure(new Size(320, 220)); viewport.Arrange(new Rect(0, 0, 320, 220)); viewport.UpdateLayout();
        var target = new RenderTargetBitmap(320, 220, 96, 96, PixelFormats.Pbgra32); target.Render(viewport);
        var method = typeof(ColorSpace3DViewport).GetMethod("ReadNativeEvidence", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var observed = JsonDocument.Parse(JsonSerializer.Serialize(method.Invoke(viewport, null)));
        Assert.AreSame(state, viewport.State, "Reading evidence must not change the camera/model.");
        Assert.IsTrue(observed.RootElement.GetProperty("ModelLoaded").GetBoolean());
        Assert.IsGreaterThan(0, observed.RootElement.GetProperty("SampleCount").GetInt32());
        Assert.IsGreaterThan(0L, observed.RootElement.GetProperty("RenderCount").GetInt64());
        Assert.IsGreaterThanOrEqualTo(0d, observed.RootElement.GetProperty("LastRenderMilliseconds").GetDouble());
    });

    [TestMethod]
    public void ObserverRequiresOptInAndSynchronizesIdsNotNames()
    {
        var observer = Read("src/RAWSelectionAssistant/Services/ColorStudioAcceptanceFixture.cs");
        StringAssert.Contains(observer, "Requested && Environment.GetCommandLineArgs().Contains(\"--native-evidence-observer\")");
        StringAssert.Contains(observer, "SequenceEqual(editor.NativeRenderedNodeIds)");
        StringAssert.Contains(observer, "ModelRenderSynchronized = editor.IsSettled && !editor.HasError");
        StringAssert.Contains(observer, "Viewport = view?.ReadNativeViewportEvidence()");
        var view = Read("src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs");
        StringAssert.Contains(view, "OrderBy(row => row!.Y)");
        StringAssert.Contains(view, "if (row is null || !row.IsVisible) return null;");
        var sample = Read("src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.NativeEvidence.cs");
        foreach (var field in new[] { "InputTimestamp", "ImageRect", "Valid = imagePoint is not null", "Value = value", "_nativeSamples.Count > 128" })
            StringAssert.Contains(sample, field);
    }

    [TestMethod]
    public void SynchronizedReaderNeverUsesFixedDelayAsSuccess()
    {
        var reader = Read("scripts/read-native-closure-observation.ps1");
        foreach (var field in new[] { "ModelRenderSynchronized", "VisualModelSynchronized", "-ceq ($modelIds -join ',')", "Native observation timeout", "$last.Nonce -ne $nonce" })
            StringAssert.Contains(reader, field);
        foreach (var forbidden in new[] { "SendInput", "InvokePattern", ".Execute(", "RaiseEvent" })
            Assert.IsFalse(reader.Contains(forbidden, StringComparison.Ordinal));
    }

    private static string Read(string path)
    {
        for (var root = new DirectoryInfo(AppContext.BaseDirectory); root is not null; root = root.Parent)
            if (File.Exists(Path.Combine(root.FullName, "RAWSelectionAssistant.sln"))) return File.ReadAllText(Path.Combine(root.FullName, path));
        throw new DirectoryNotFoundException();
    }
    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception failure) { error = failure; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "Bounded observation test.");
        if (error is not null) throw error;
    }
}
