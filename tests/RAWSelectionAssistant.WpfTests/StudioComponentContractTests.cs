using System.IO;
using System.Xml.Linq;
namespace RAWSelectionAssistant.WpfTests;
internal static class StudioTestSource
{
    internal static string Read(string file) {
        for(var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if(File.Exists(Path.Combine(d.FullName, "RAWSelectionAssistant.sln")))
                return File.ReadAllText(Path.Combine(d.FullName,"src/RAWSelectionAssistant/Resources/DesignSystem",file));
        throw new DirectoryNotFoundException();
    }
    internal static void Contains(string file, params string[] values) { var source=Read(file); XDocument.Parse(source); foreach(var value in values) StringAssert.Contains(source,value); }
}
[TestClass] public sealed class StudioColorsTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Colors.Dark.xaml","AppBackgroundColor","SurfaceElevatedColor","TextPrimaryColor","PrimaryColor"); }
[TestClass] public sealed class StudioRadiusTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Studio.Controls.xaml","RadiusCompact","RadiusControl","RadiusCard","RadiusPanel","RadiusPopup","RadiusDialog","RadiusLarge"); }
[TestClass] public sealed class StudioButtonStateTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Components.Foundation.xaml","PixelTart.Button.Primary","PixelTart.Button.Secondary","PixelTart.Button.Ghost","PixelTart.Button.Danger","IsMouseOver","IsPressed","IsEnabled","IsKeyboardFocused"); }
[TestClass] public sealed class StudioIconButtonTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Studio.Controls.xaml","PixelTart.Button.Icon","Value=\"30\""); }
[TestClass] public sealed class StudioSliderTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Controls.Inputs.xaml","PART_Track","Track.Thumb","Height=\"4\"","Width=\"14\""); }
[TestClass] public sealed class StudioToggleTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Studio.Controls.xaml","PixelTart.Toggle","IsChecked","Width=\"30\" Height=\"18\"","Width=\"14\" Height=\"14\""); }
[TestClass] public sealed class StudioSegmentedTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Studio.Controls.xaml","PixelTart.Segmented","ListBoxItem","IsSelected"); }
[TestClass] public sealed class StudioPopupTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Components.Foundation.xaml","PixelTart.Menu.Context","RadiusPopup","MenuPopupBackgroundBrush"); }
[TestClass] public sealed class StudioLoadingTests {
    [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Studio.Controls.xaml","PixelTart.LoadingRing","PixelTart.ProgressBar","PixelTart.LoadingOverlay");
    [TestMethod] public void FeedbackThresholds_DoNotFlashForShortOperations() {
        Assert.AreEqual(0, PixelTart.Modules.AssetLibrary.StudioBusyFeedback.StageForElapsed(TimeSpan.FromMilliseconds(299)));
        Assert.AreEqual(1, PixelTart.Modules.AssetLibrary.StudioBusyFeedback.StageForElapsed(TimeSpan.FromMilliseconds(300)));
        Assert.AreEqual(1, PixelTart.Modules.AssetLibrary.StudioBusyFeedback.StageForElapsed(TimeSpan.FromMilliseconds(1499)));
        Assert.AreEqual(2, PixelTart.Modules.AssetLibrary.StudioBusyFeedback.StageForElapsed(TimeSpan.FromMilliseconds(1500)));
    }
    [TestMethod] public Task DelayedFeedback_ResetsOnCompletionAndUnload() => AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async () => {
        var element = new System.Windows.Controls.Border();
        var window = new System.Windows.Window { Content = element, Width = 200, Height = 100 };
        window.Show();
        try {
            PixelTart.Modules.AssetLibrary.StudioBusyFeedback.SetIsBusy(element, true);
            Assert.IsFalse(PixelTart.Modules.AssetLibrary.StudioBusyFeedback.GetIsVisible(element));
            PixelTart.Modules.AssetLibrary.StudioBusyFeedback.SetIsBusy(element, false);
            await Task.Delay(350);
            Assert.IsFalse(PixelTart.Modules.AssetLibrary.StudioBusyFeedback.GetIsVisible(element));
            PixelTart.Modules.AssetLibrary.StudioBusyFeedback.SetIsBusy(element, true);
            await Task.Delay(450);
            Assert.IsTrue(PixelTart.Modules.AssetLibrary.StudioBusyFeedback.GetIsVisible(element));
            Assert.IsFalse(PixelTart.Modules.AssetLibrary.StudioBusyFeedback.GetIsLongRunning(element));
            await Task.Delay(1200);
            Assert.IsTrue(PixelTart.Modules.AssetLibrary.StudioBusyFeedback.GetIsLongRunning(element));
            PixelTart.Modules.AssetLibrary.StudioBusyFeedback.SetIsBusy(element, false);
            Assert.IsFalse(PixelTart.Modules.AssetLibrary.StudioBusyFeedback.GetIsVisible(element));
            Assert.IsFalse(PixelTart.Modules.AssetLibrary.StudioBusyFeedback.GetIsLongRunning(element));
        } finally { window.Close(); }
    });
}
[TestClass] public sealed class StudioFocusTests { [TestMethod] public void ProductionResources_ContainRequiredContract() => StudioTestSource.Contains("Studio.Controls.xaml","PixelTart.Focus","AccentBrush","FocusVisualStyle"); }
[TestClass] public sealed class StudioSymbolTests {
    [TestMethod] public void Symbols_AreDistinctAuthoredVectorGeometry() {
        var x = XDocument.Parse(StudioTestSource.Read("Studio.Symbols.xaml"));
        XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml";
        var paths=x.Descendants().Where(e=>e.Name.LocalName=="Geometry" && ((string?)e.Attribute(ns+"Key"))?.StartsWith("PixelTart.Symbol.")==true).ToArray();
        Assert.HasCount(36,paths); Assert.AreEqual(paths.Length,paths.Select(e=>e.Value).Distinct().Count());
        foreach(var p in paths) { var g=System.Windows.Media.Geometry.Parse(p.Value); Assert.IsFalse(g.Bounds.IsEmpty); Assert.IsTrue(g.Bounds.Left>=0 && g.Bounds.Right<=24 && g.Bounds.Bottom<=24); }
    }
}
[TestClass] public sealed class StudioNoWhiteSurfaceLeakTests {
    [TestMethod] public void SegmentedAndDatePicker_UseExplicitDarkTemplates() {
        StudioTestSource.Contains("Studio.Controls.xaml", "ControlTemplate TargetType=\"ListBox\"", "TargetType=\"DatePicker\" BasedOn=\"{StaticResource ProposalDatePicker}\"");
    }
    [TestMethod] public void StudioTemplates_DoNotHardcodeWhiteSurfaces() {
        var x=XDocument.Parse(StudioTestSource.Read("Studio.Controls.xaml"));
        foreach(var a in x.Descendants().Attributes().Where(a=>a.Name.LocalName=="Background"))
            Assert.IsFalse(new[]{"White","#FFF","#FFFFFF"}.Contains(a.Value,StringComparer.OrdinalIgnoreCase));
    }
}
