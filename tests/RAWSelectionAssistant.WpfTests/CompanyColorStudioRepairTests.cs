using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class CompanyColorStudioRepairTests
{
    [TestMethod]
    public Task CancelCurveTransactionRestoresStackAndExistingRedo() => RunSta(() =>
    {
        using var editor = new TetherReferenceModeViewModel();
        editor.ApplyTargetSnapshot(null, null, new ColorAdjustmentStack([]), false, Guid.NewGuid());
        editor.SetCurvePoints("rgb", [new(0, 0), new(.5, .7), new(1, 1)]);
        editor.UndoAdjustmentCommand.Execute(null);
        var before = System.Text.Json.JsonSerializer.Serialize(editor.AdjustmentStack);
        editor.BeginEditTransaction();
        editor.SetCurvePoints("rgb", [new(0, 0), new(.4, .8), new(1, 1)]);
        Assert.IsTrue(editor.CancelEditTransaction());
        Assert.AreEqual(before, System.Text.Json.JsonSerializer.Serialize(editor.AdjustmentStack));
        Assert.IsFalse(editor.UndoAdjustmentCommand.CanExecute(null));
        Assert.IsTrue(editor.RedoAdjustmentCommand.CanExecute(null));
        editor.RedoAdjustmentCommand.Execute(null);
        Assert.AreEqual(.7, ColorStudioToolProcessor.ReadCurve(editor.ToolNode(ColorStudioNodeType.Curve)!, "rgb")[1].Y);
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task NumericDraftCannotCommitToAnotherPhotograph() => RunSta(() =>
    {
        var target = Guid.NewGuid(); double value = .25; var commits = 0;
        var field = new StudioNumericEditor(() => value, v => { value = v; commits++; }, -5, 5, () => target);
        field.Text = "2";
        target = Guid.NewGuid(); value = -.5;
        Assert.IsFalse(field.CommitDraft());
        Assert.AreEqual(0, commits);
        Assert.AreEqual(-.5, value);
        Assert.AreEqual(value.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture), field.Text);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ToneEntryIsOutsideInnerScrollAndLeavesEditingSpace() => RunSta(() =>
    {
        EnsureTestApplication(); var view = new ReferenceColorWorkspaceView();
        foreach (var height in new[] { 480d, 650d, 900d })
        {
            view.Width = 1180; view.Height = height;
            view.Measure(new Size(1180, height)); view.Arrange(new Rect(0, 0, 1180, height)); view.UpdateLayout();
            var tone = (Expander)view.FindName("ToneZonesExpander"); var scroll = (ScrollViewer)view.FindName("AnalysisScroll");
            var rail = (FrameworkElement)view.FindName("EditingRail"); var tools = (ListBox)view.FindName("ToolModes");
            Assert.IsFalse(scroll.IsAncestorOf(tone));
            Assert.IsTrue(tone.TranslatePoint(new Point(), rail).Y + tone.ActualHeight < rail.ActualHeight);
            Assert.IsTrue(tools.TranslatePoint(new Point(), rail).Y + tools.ActualHeight < rail.ActualHeight - 50);
        }
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task FocusedNumericEscapeWinsBeforeShellNavigation() => RunSta(async () =>
    {
        EnsureTestApplication();
        var view = new ReferenceColorWorkspaceView();
        double committed = .75;
        var field = new StudioNumericEditor(() => committed, v => committed = v, -5, 5);
        ((StackPanel)view.FindName("ToolPages")).Children.Insert(0, field);
        var window = new Window { Content = view, Width = 1180, Height = 720, ShowInTaskbar = false, Left = -32000 };
        try
        {
            window.Show(); field.Focus(); System.Windows.Input.Keyboard.Focus(field);
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Input);
            Assert.IsTrue(field.IsKeyboardFocusWithin);
            field.Text = "abc";
            Assert.IsTrue(view.TryCancelNumericDraft());
            Assert.AreEqual(.75.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture), field.Text);
            Assert.AreEqual(.75, committed);
            Assert.IsTrue(view.TryClearTransientInspection(), "Idle Escape must not navigate Home.");
        }
        finally { window.Close(); }
    });
    [TestMethod]
    public Task ExpandedCloudUsesSameViewportInsideNonOverlappingColumns() => RunSta(() =>
    {
        EnsureTestApplication();
        var view = new ReferenceColorWorkspaceView();
        var cloud = (ColorSpace3DViewport)view.FindName("ColorSpaceViewport");
        var settings = cloud.ViewSettings;
        var pixels = new VisualPixelBuffer(1, 1, new byte[] { 70, 100, 130 });
        var samples = ColorSpaceProxyBuilder.Build(pixels, ColorSpaceSampling.Settings(ColorSpaceSamplingTier.Standard));
        cloud.SetModel(new ColorSpaceVisualizationModel(samples, samples, samples, [], ColorCloudMode.Source, ColorSpaceSamplingTier.Standard, samples.SourceFingerprint));
        cloud.State = cloud.State! with { Camera = ColorSpaceCamera.Default.Rotate(17, 23).Zoom(1.3), IsFit = false };
        var camera = cloud.State.Camera;
        foreach (var width in new[] { 950d, 1180d, 1760d })
        {
            view.Width = width; view.Height = 760;
            view.ToggleCloudDock();
            view.Measure(new Size(width, 760)); view.Arrange(new Rect(0, 0, width, 760)); view.UpdateLayout();
            var grid = (Grid)view.FindName("WorkspaceGrid");
            var left = (FrameworkElement)view.FindName("ContextRail"); var right = (FrameworkElement)view.FindName("EditingRail");
            Assert.AreSame(cloud, view.FindName("ColorSpaceViewport"));
            Assert.AreSame(settings, cloud.ViewSettings);
            Assert.AreEqual(camera, cloud.State!.Camera, "Expansion must not reset the shared camera.");
            Assert.IsTrue(grid.ColumnDefinitions[1].ActualWidth > 250);
            var leftBounds = left.TransformToAncestor(grid).TransformBounds(new Rect(left.RenderSize));
            var rightBounds = right.TransformToAncestor(grid).TransformBounds(new Rect(right.RenderSize));
            Assert.IsFalse(leftBounds.IntersectsWith(rightBounds), "The expanded dock must not cover the editor.");
            var photo = (FrameworkElement)view.FindName("PreviewCanvas");
            Assert.IsFalse(leftBounds.IntersectsWith(photo.TransformToAncestor(grid).TransformBounds(new Rect(photo.RenderSize))), "The expanded dock must not cover the photograph.");
            view.ToggleCloudDock();
            Assert.AreSame(cloud, view.FindName("ColorSpaceViewport"));
        }
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task NumericDraftRejectsInvalidValuesCancelsAndUsesImageHistory() => RunSta(() =>
    {
        using var editor = new TetherReferenceModeViewModel();
        editor.ApplyTargetSnapshot(null, null, new ColorAdjustmentStack([]), false, Guid.NewGuid());
        var parameter = ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p => p.Key == "exposure");
        var field = new StudioNumericEditor(() => editor.ToolValue(parameter), value => editor.SetToolParameter(parameter, value), parameter.Minimum, parameter.Maximum);
        foreach (var invalid in new[] { "abc", "", "NaN", "99999" })
        {
            field.Text = invalid;
            Assert.IsFalse(field.CommitDraft());
            Assert.AreEqual(0, editor.ToolValue(parameter));
            Assert.IsTrue(field.CancelDraft());
            Assert.AreEqual("0", field.Text);
            Assert.IsFalse(editor.UndoAdjustmentCommand.CanExecute(null));
        }
        field.Text = .75.ToString(System.Globalization.CultureInfo.CurrentCulture);
        Assert.IsTrue(field.CommitDraft());
        Assert.AreEqual(.75, editor.ToolValue(parameter));
        field.Text = "abc";
        field.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, 0, field, null) { RoutedEvent = System.Windows.Input.Keyboard.LostKeyboardFocusEvent });
        Assert.AreEqual(.75.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture), field.Text);
        Assert.AreEqual(.75, editor.ToolValue(parameter));
        editor.UndoAdjustmentCommand.Execute(null); Assert.AreEqual(0, editor.ToolValue(parameter));
        editor.RedoAdjustmentCommand.Execute(null); Assert.AreEqual(.75, editor.ToolValue(parameter));
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task EditingRailFitsItsActualColumnIncludingInspectorStyleMinimum() => RunSta(() =>
    {
        EnsureTestApplication();
        var view = new ReferenceColorWorkspaceView();
        // Studio viewport after shell/navigation: DIP, not simulated Windows DPI.
        foreach (var width in new[] { 950d, 1020d, 1180d, 1440d, 1760d })
        {
            view.Width = width; view.Height = 650;
            view.Measure(new Size(width, 650)); view.Arrange(new Rect(0, 0, width, 650)); view.UpdateLayout();
            var rail = (FrameworkElement)view.FindName("EditingRail");
            var grid = (Grid)view.FindName("WorkspaceGrid");
            var bounds = rail.TransformToAncestor(grid).TransformBounds(new Rect(rail.RenderSize));
            Assert.IsTrue(bounds.Right <= grid.ActualWidth + .1,
                $"Right rail escapes allocated viewport at {width} DIP: rail={bounds}, grid={grid.ActualWidth}");
            Assert.IsTrue(rail.ActualWidth + rail.Margin.Left <= grid.ColumnDefinitions[2].ActualWidth + .1);
            var modes = (ListBox)view.FindName("ToolModes");
            foreach (var item in modes.Items.OfType<ListBoxItem>())
            {
                var itemBounds = item.TransformToAncestor(rail).TransformBounds(new Rect(item.RenderSize));
                Assert.IsTrue(itemBounds.Right <= rail.ActualWidth + .1, $"Unreachable tab: {item.Content}");
            }
        }
        return Task.CompletedTask;
    });
}
