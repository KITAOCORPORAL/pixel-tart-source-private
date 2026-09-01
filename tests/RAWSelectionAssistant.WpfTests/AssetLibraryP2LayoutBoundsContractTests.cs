#if ASSET_LIBRARY_P2_AUTOMATED_ACCEPTANCE
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP2LayoutBoundsContractTests
{
    [TestMethod]
    public async Task MustFitClassificationKeepsScrollableButtonsEvidenceOnly()
    {
        var driverType = AcceptanceDriverType();
        if (driverType is null)
        {
            Assert.Inconclusive("The P2 acceptance driver is only present in the acceptance build.");
            return;
        }

        await RunSta(() =>
        {
            var method = driverType.GetMethod("IsMustFitElement", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method);

            var headerButton = new Button { Content = "header" };
            Assert.IsTrue((bool)method!.Invoke(null, [headerButton, "AssetLibraryImport"])!,
                "A non-scrollable header button must remain a hard layout-fit requirement.");

            var structural = new Grid();
            Assert.IsTrue((bool)method.Invoke(null, [structural, "AssetLibraryThreePaneWorkspace"])!,
                "Structural workspace elements must always remain hard layout-fit requirements.");

            var host = new Grid { Width = 200, Height = 80 };
            var scrollButton = new Button { Content = "scroll content" };
            var scrollViewer = new ScrollViewer
            {
                Content = scrollButton,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            host.Children.Add(scrollViewer);
            host.Measure(new Size(200, 80));
            host.Arrange(new Rect(0, 0, 200, 80));
            host.UpdateLayout();
            Assert.IsTrue(HasVisualParent<ScrollViewer>(scrollButton), "The test button must be inside a real ScrollViewer visual tree.");
            Assert.IsFalse((bool)method.Invoke(null, [scrollButton, "ScrollableButton"])!,
                "A button inside a ScrollViewer must be evidence-only and allowed to be clipped by scrolling.");
        });
    }

    [TestMethod]
    public void LayoutOverflowRemainsFailClosedForMustFitRowsAndInvalidViewports()
    {
        var driverType = AcceptanceDriverType();
        if (driverType is null)
        {
            Assert.Inconclusive("The P2 acceptance driver is only present in the acceptance build.");
            return;
        }

        var boundsType = driverType.Assembly.GetType("PixelTart.Modules.AssetLibrary.AssetLibraryP2AutomatedElementBounds");
        Assert.IsNotNull(boundsType);
        var constructor = boundsType!.GetConstructors().Single();

        object Row(string identity, bool clipped, bool mustFit, double x = 0d, double y = 0d) => constructor.Invoke([
            identity, "Button", "AssetLibraryPage", 1, "Visible",
            x, y, 80d, 30d, x, y, 80d, 30d,
            clipped, false, true, true, mustFit]);

        var overflowMethod = driverType.GetMethod("HasLayoutOverflow", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(overflowMethod);
        var safeScrollable = Array.CreateInstance(boundsType, 1);
        safeScrollable.SetValue(Row("scroll-button", clipped: true, mustFit: false), 0);
        Assert.IsFalse((bool)overflowMethod!.Invoke(null, [safeScrollable, 100d, 100d])!,
            "Clipping a scrollable-content row must not fail the structural layout contract.");

        var clippedStructural = Array.CreateInstance(boundsType, 1);
        clippedStructural.SetValue(Row("structural", clipped: true, mustFit: true), 0);
        Assert.IsTrue((bool)overflowMethod.Invoke(null, [clippedStructural, 100d, 100d])!,
            "A clipped structural row must still fail closed.");

        var outsideNonScrollable = Array.CreateInstance(boundsType, 1);
        outsideNonScrollable.SetValue(Row("header", clipped: false, mustFit: true, x: 101d), 0);
        Assert.IsTrue((bool)overflowMethod.Invoke(null, [outsideNonScrollable, 100d, 100d])!,
            "A non-scrollable row outside the viewport must still fail closed.");

        foreach (var invalid in new object[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var exception = Assert.ThrowsExactly<TargetInvocationException>(() =>
                overflowMethod.Invoke(null, [safeScrollable, invalid, 100d]));
            Assert.IsInstanceOfType<ArgumentOutOfRangeException>(exception.InnerException);
        }

        var malformedGeometry = Array.CreateInstance(boundsType, 1);
        malformedGeometry.SetValue(Row("malformed", clipped: false, mustFit: false, x: double.NaN), 0);
        Assert.IsTrue((bool)overflowMethod.Invoke(null, [malformedGeometry, 100d, 100d])!,
            "Non-finite captured geometry must fail closed even for evidence-only rows.");
    }

    private static Type? AcceptanceDriverType() =>
        typeof(PixelTart.Modules.AssetLibrary.AssetLibraryPage).Assembly
            .GetType("PixelTart.Modules.AssetLibrary.AssetLibraryP2AutomatedAcceptanceDriver");

    private static bool HasVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(child); current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is T) return true;
        return false;
    }

    private static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
#endif
