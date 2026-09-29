using System.Windows;
using System.Windows.Controls;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class UiGeometryGuardianTests
{
    [TestMethod]
    public void PeerButtonOverlap_IsP0Violation_ButParentChildIsAllowed()
    {
        var failure = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var root = new Grid { Width = 240, Height = 100 };
                var first = new Button { Width = 90, Height = 40, Content = "A", Margin = new Thickness(10, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                var second = new Button { Width = 90, Height = 40, Content = "B", Margin = new Thickness(50, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                root.Children.Add(first); root.Children.Add(second);
                var host = new Window { Content = root, Width = 240, Height = 100, ShowInTaskbar = false };
                host.Show(); host.UpdateLayout();
                var violations = StudioVisualEvidence.FindGeometryViolations(root);
                Assert.IsTrue(violations.Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.ControlOverlap && v.Severity == "P0"));
                var parent = new Button { Width = 100, Height = 40, Content = new TextBlock { Text = "icon" } };
                var valid = new Grid { Width = 120, Height = 50 }; valid.Children.Add(parent); var validHost = new Window { Content = valid, Width = 120, Height = 50, ShowInTaskbar = false }; validHost.Show(); validHost.UpdateLayout();
                Assert.IsFalse(StudioVisualEvidence.FindGeometryViolations(valid).Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.ControlOverlap));
                host.Close(); validHost.Close();
                failure.SetResult(null);
            }
            catch (Exception ex) { failure.SetResult(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10))); if (failure.Task.GetAwaiter().GetResult() is {} error) throw error;
    }

    [TestMethod]
    public void ViolationKinds_ExposeGuardianContract()
    {
        var names = Enum.GetNames<StudioVisualEvidence.GeometryViolationKind>();
        CollectionAssert.IsSubsetOf(new[] { "TextClipped", "TextOverflow", "ControlOverlap", "OutsideRoot", "HeaderCollision", "ButtonTooSmall", "InputTooSmall", "PopupClipped", "CanvasStarved" }, names);
    }

    [TestMethod]
    public void ScrollViewerOffscreenChild_IsNotOutsideRootP0()
    {
        RunSta(() =>
        {
            var scroll = new ScrollViewer { Width = 100, Height = 40, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel { Children = { new Border { Height = 120, Width = 80 } } } };
            var host = new Window { Content = scroll, Width = 100, Height = 40, ShowInTaskbar = false }; host.Show(); host.UpdateLayout();
            Assert.IsFalse(StudioVisualEvidence.FindGeometryViolations(scroll).Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.OutsideRoot)); host.Close();
        });
    }

    [TestMethod]
    public void HeaderButtonOutsideRoot_IsP0()
    {
        RunSta(() =>
        {
            var root = new Grid { Width = 100, Height = 40 }; var button = new Button { Width = 40, Height = 36, Margin = new Thickness(90, 0, -30, 0), HorizontalAlignment = HorizontalAlignment.Left }; root.Children.Add(button);
            var host = new Window { Content = root, Width = 100, Height = 40, ShowInTaskbar = false }; host.Show(); host.UpdateLayout();
            Assert.IsTrue(StudioVisualEvidence.FindGeometryViolations(root).Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.OutsideRoot && v.Severity == "P0")); host.Close();
        });
    }

    [TestMethod]
    public void InteractivePeerOverlap_RemainsP0()
    {
        RunSta(() =>
        {
            var root = new Grid { Width = 180, Height = 70 }; root.Children.Add(new Button { Width = 90, Height = 40, HorizontalAlignment = HorizontalAlignment.Left }); root.Children.Add(new Button { Width = 90, Height = 40, Margin = new Thickness(50, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left });
            var host = new Window { Content = root, Width = 180, Height = 70, ShowInTaskbar = false }; host.Show(); host.UpdateLayout();
            Assert.IsTrue(StudioVisualEvidence.FindGeometryViolations(root).Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.ControlOverlap && v.Severity == "P0")); host.Close();
        });
    }

    [TestMethod]
    public void IntentionalGridOverlay_IsNotPeerOverlap()
    {
        RunSta(() =>
        {
            var root = new Grid { Width = 180, Height = 70 }; var button = new Button { Width = 90, Height = 40, Content = new TextBlock { Text = "icon" }, HorizontalAlignment = HorizontalAlignment.Left }; root.Children.Add(button);
            var host = new Window { Content = root, Width = 180, Height = 70, ShowInTaskbar = false }; host.Show(); host.UpdateLayout();
            Assert.IsFalse(StudioVisualEvidence.FindGeometryViolations(root).Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.ControlOverlap)); host.Close();
        });
    }

    [TestMethod]
    public void ScrollViewerVisibleClippedControl_IsP0WhenIncorrect()
    {
        RunSta(() =>
        {
            var root = new Grid { Width = 120, Height = 50 };
            var scroll = new ScrollViewer { Width = 120, Height = 50, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, ClipToBounds = false };
            scroll.Content = new Button { Width = 80, Height = 40, Margin = new Thickness(110, 0, 0, 0) };
            root.Children.Add(scroll);
            using var host = new TestWindow(root, 120, 50); host.Show(); host.UpdateLayout();
            Assert.IsTrue(StudioVisualEvidence.FindGeometryViolations(root).Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.OutsideRoot && v.Severity == "P0"));
        });
    }

    [TestMethod]
    public void ScrollableLongInspector_IsReachableNotP0()
    {
        RunSta(() =>
        {
            var content = new StackPanel(); content.Children.Add(new Border { Height = 600, Width = 100 });
            var scroll = new ScrollViewer { Width = 120, Height = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content };
            using var host = new TestWindow(scroll, 120, 80); host.Show(); host.UpdateLayout();
            Assert.IsFalse(StudioVisualEvidence.FindGeometryViolations(scroll).Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.OutsideRoot && v.Severity == "P0"));
        });
    }

    [TestMethod]
    public void TextInsideHorizontalScroll_IsNotFalseOverflow()
    {
        RunSta(() =>
        {
            var text = new TextBlock { Text = "A very long project name that is intentionally scrollable", Width = 80, TextWrapping = TextWrapping.NoWrap };
            var scroll = new ScrollViewer { Width = 80, Height = 36, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = text };
            using var host = new TestWindow(scroll, 80, 36); host.Show(); host.UpdateLayout();
            Assert.IsFalse(StudioVisualEvidence.FindGeometryViolations(scroll).Any(v => v.Kind == StudioVisualEvidence.GeometryViolationKind.TextOverflow && v.Severity == "P0"));
        });
    }

    [TestMethod]
    public void ButtonTextActuallyClipped_IsP0()
    {
        RunSta(() =>
        {
            var root = new Grid { Width = 120, Height = 50, ClipToBounds = true };
            var label = new TextBlock { Text = "This label is definitely clipped", Width = 140, Height = 30, Margin = new Thickness(-30, 0, 0, 0), ClipToBounds = true, FontSize = 20, TextWrapping = TextWrapping.NoWrap, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            root.Children.Add(label);
            using var host = new TestWindow(root, 120, 50); host.Show(); host.UpdateLayout();
            Assert.IsTrue(StudioVisualEvidence.FindGeometryViolations(root).Any(v => v.Severity == "P0" && (v.Kind == StudioVisualEvidence.GeometryViolationKind.TextClipped || v.Kind == StudioVisualEvidence.GeometryViolationKind.TextOverflow || v.Kind == StudioVisualEvidence.GeometryViolationKind.OutsideRoot)));
        });
    }

    private sealed class TestWindow : Window, IDisposable
    {
        public TestWindow(FrameworkElement content, double width, double height) { Content = content; Width = width; Height = height; ShowInTaskbar = false; }
        public void Dispose() => Close();
    }

    private static void RunSta(Action action)
    {
        Exception? error = null; var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10))); if (error is not null) throw error;
    }
}
