using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using static RAWSelectionAssistant.WpfTests.RuntimeUserFindingsBatchATests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class RuntimeUserFindingsBatchBTests
{
    [TestMethod]
    public Task InteractivePopupContentRetainsEditorWithoutMenuChrome() => RunSta(async () =>
    {
        var editor = new TextBox { Text = "标签" };
        var host = PixelTart.Modules.AssetLibrary.AssetLibraryPage.PopupContent(editor);
        Assert.IsTrue(host.StaysOpenOnClick);
        Assert.IsFalse(host.Focusable);
        Assert.AreSame(editor, host.Header);
        host.ApplyTemplate(); host.Measure(new Size(400, 600));
        Assert.IsLessThan(100d, host.DesiredSize.Width);
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task QuickFilterPinsSurviveReloadWithoutChangingTheQuery() => RunSta(async () =>
    {
        var root = await Fixture();
        var settings = new AssetLibraryWorkspaceSettings();
        await using var vm = new AssetLibraryViewModel(System.IO.Path.Combine(root, "assets.db"), new RAWSelectionAssistant.Core.Services.Tasks.TaskOperationBridge(), workspaceSettings: settings);
        await vm.InitializeAsync();
        var before = vm.P3QueryRoot.ToModel();
        vm.SetFilterPinned(AssetQueryField.Width, true);
        vm.SetFilterPinned(AssetQueryField.Rating, false);
        Assert.AreEqual(JsonSerializer.Serialize(before), JsonSerializer.Serialize(vm.P3QueryRoot.ToModel()));
        var restored = JsonSerializer.Deserialize<AssetLibraryWorkspaceSettings>(JsonSerializer.Serialize(settings))!;
        restored.Normalize();
        Assert.Contains(AssetQueryField.Width, restored.PinnedFilterFields);
        Assert.DoesNotContain(AssetQueryField.Rating, restored.PinnedFilterFields);
        vm.EditQuickFilter(AssetQueryField.Width);
        Assert.IsTrue(vm.P3QueryPanelOpen);
        var width = vm.P3QueryRoot.Children.Single();
        Assert.IsTrue(width.IsNumericEditor || width.IsNumericRangeEditor);
        width.ValueText = "48";
        vm.SubmitP3SearchCommand.Execute(null); await vm.SubmitP3SearchCommand.ExecutionTask;
        Assert.HasCount(2, vm.AssetCards);
    });

    [TestMethod]
    public Task ColorPickerUsesExistingColorStateAndAcceptsIncompleteHex() => RunSta(async () =>
    {
        var root = await Fixture(); await using var vm = ViewModel(root);
        var picker = new AssetColorFilterPicker { DataContext = vm };
        picker.Measure(new Size(300, 600)); picker.Arrange(new Rect(0, 0, 248, 500));
        vm.TargetColor = "#0000FF";
        Assert.AreEqual(240d, vm.ColorHue, .001);
        Assert.AreEqual(1d, vm.ColorSaturation, .001);
        Assert.AreEqual(1d, vm.ColorBrightness, .001);
        var converter = new HexToBrushConverter();
        foreach (var value in new[] { "", "#", "#12", "#GGGGGG" })
            Assert.AreSame(Brushes.Transparent, converter.Convert(value, typeof(Brush), null, CultureInfo.InvariantCulture));
        Assert.IsLessThanOrEqualTo(248d, picker.DesiredSize.Width);
    });
}
