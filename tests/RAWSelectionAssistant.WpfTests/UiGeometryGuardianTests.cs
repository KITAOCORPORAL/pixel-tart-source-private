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
}
