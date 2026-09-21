using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class GlobalCloseSafeAreaTests
{
    [TestMethod]
    public void ShellReservesCloseAreaStructurally()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/MainWindow.xaml"));
        StringAssert.Contains(source, "ShellSurfaceCloseReservedWidth"); StringAssert.Contains(source, "ShellCloseSafeAreaColumn");
        StringAssert.Contains(source, "<GridLength x:Key=\"ShellSurfaceCloseReservedWidth\">56</GridLength>");
    }

    [TestMethod]
    public void RuntimeSafeAreaPreventsHeaderActionIntersectionAcrossMatrix()
    {
        Exception? failure = null; var thread = new Thread(() =>
        {
            try
            {
                var app = new App(); app.InitializeComponent();
                foreach (var width in new[] { 1180d, 1366d, 1600d, 1920d, 2560d }) foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
                {
                    var host = new Grid { Width = width / scale, Height = 720 / scale };
                    host.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); host.ColumnDefinitions.Add(new() { Width = new GridLength(56) });
                    var action = new Button { Width = 120, Height = 34, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
                    var close = new SurfaceCloseButton { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 0, 0) };
                    Grid.SetColumn(action, 0); Grid.SetColumn(close, 1); host.Children.Add(action); host.Children.Add(close); host.Measure(new(width / scale, 720 / scale)); host.Arrange(new Rect(0, 0, width / scale, 720 / scale));
                    var actionRect = action.TransformToAncestor(host).TransformBounds(new Rect(action.RenderSize)); var closeRect = close.TransformToAncestor(host).TransformBounds(new Rect(close.RenderSize));
                    Assert.IsFalse(actionRect.IntersectsWith(closeRect), $"{width}px @{scale:P0}"); Assert.AreEqual(40, close.ActualWidth, .1);
                }
            }
            catch (Exception ex) { failure = ex; }
        }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20))); if (failure is not null) throw failure;
    }

    [TestMethod]
    public void DialogHeadersUseDedicatedCloseColumns()
    {
        foreach (var path in new[] { "Views/ThemedMessageDialog.xaml", "Views/QuickBookingEditorView.xaml", "Views/ShootBookingEditorView.xaml", "Views/ShootBookingDetailsView.xaml" })
        {
            var source = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant", path));
            StringAssert.Contains(source, "ColumnDefinition Width=\"Auto\""); StringAssert.Contains(source, "SurfaceCloseButton");
        }
    }

    private static string Root() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}
