using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class TetherSplitCompareTests
{
    [TestMethod]
    public void SplitAccepts25_50_75AndClampsBounds()
    {
        OnSta(() =>
        {
            var control = new TetherReferenceSplitView();
            foreach (var value in new[] { .25, .5, .75 }) { control.SplitPosition = value; Assert.AreEqual(value, control.SplitPosition, 1e-12); }
            control.SplitPosition = -1; Assert.AreEqual(0, control.SplitPosition);
            control.SplitPosition = 2; Assert.AreEqual(1, control.SplitPosition);
        });
    }

    [TestMethod]
    public void SplitUsesGeometryAndSharedParentTransform()
    {
        var root = RepoRoot();
        var control = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "Views", "TetherReferenceSplitView.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "Views", "TetherCaptureView.xaml"));
        StringAssert.Contains(control, "RectangleGeometry"); StringAssert.Contains(control, "SplitPosition = .5");
        StringAssert.Contains(xaml, "TetherReferenceSplitView");
        Assert.IsTrue(Regex.IsMatch(xaml, @"<Grid\.RenderTransform>.*?<ScaleTransform.*?<TranslateTransform.*?</Grid\.RenderTransform>.*?<views:TetherReferenceSplitView", RegexOptions.Singleline));
    }

    private static T OnSta<T>(Func<T> action)
    {
        T? value = default; Exception? error = null; var thread = new Thread(() => { try { value = action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (error is not null) throw error; return value!;
    }
    private static void OnSta(Action action) => OnSta(() => { action(); return true; });
    private static string RepoRoot()
    {
        var path = AppContext.BaseDirectory; while (path is not null && !Directory.Exists(Path.Combine(path, "src"))) path = Directory.GetParent(path)?.FullName;
        return path ?? throw new DirectoryNotFoundException();
    }
}
