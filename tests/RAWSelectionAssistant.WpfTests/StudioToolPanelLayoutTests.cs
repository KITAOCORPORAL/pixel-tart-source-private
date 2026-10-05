using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StudioToolPanelLayoutTests
{
    [TestMethod]
    public Task CompactToolParentsKeepEnableResetAndNestedParametersReachable() => RunSta(async () =>
    {
        EnsureTestApplication(); using var editor = new TetherReferenceModeViewModel();
        var panel = new StudioToolPanel { NodeTypes = "WhiteBalance,BasicTone", DataContext = editor };
        void Arrange() { panel.Measure(new Size(300, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, 300, panel.DesiredSize.Height)); panel.UpdateLayout(); }
        Arrange(); Assert.HasCount(2, panel.Children);
        var white = (Expander)panel.Children[0]; var basic = (Expander)panel.Children[1];
        Assert.HasCount(0, Descendants<Expander>(white)); Assert.HasCount(3, Descendants<Expander>(basic));
        var header = (DockPanel)white.Header; var toggle = header.Children.OfType<CheckBox>().Single(); var reset = header.Children.OfType<Button>().Single();
        Assert.IsTrue(reset.ActualHeight <= 25); Assert.IsTrue(header.ActualHeight <= 30);
        var temperature = Descendants<Slider>(white).Single(s => Equals(s.Tag, "temperature"));
        temperature.Value = 35; Assert.AreEqual(35, editor.ToolValue(ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.WhiteBalance).Single(p => p.Key == "temperature")));
        toggle.IsChecked = false; Assert.IsFalse(editor.ToolNode(ColorStudioNodeType.WhiteBalance)!.Enabled);
        toggle.IsChecked = true; reset.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Assert.AreEqual(0, temperature.Value);
        var expandedHeight = white.ActualHeight; white.IsExpanded = false; Arrange(); Assert.IsTrue(white.ActualHeight < expandedHeight);
        white.IsExpanded = true; Arrange(); Assert.IsTrue(temperature.ActualHeight > 0);
        var exposure = Descendants<Slider>(basic).Single(s => Equals(s.Tag, "exposure")); exposure.Value = .8;
        Assert.AreEqual(.8, editor.ToolValue(ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p => p.Key == "exposure")));
        Arrange();
        Assert.IsTrue(exposure.TranslatePoint(new Point(), panel).Y < 260, $"WB and first exposure need compact layout: exposure Y={exposure.TranslatePoint(new Point(), panel).Y:F1}, WB height={white.ActualHeight:F1}, parent header={header.ActualHeight:F1}, basic Y={basic.TranslatePoint(new Point(), panel).Y:F1}.");
        await Task.CompletedTask;
    });
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
}
