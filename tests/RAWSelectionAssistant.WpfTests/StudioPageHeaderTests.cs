using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StudioPageHeaderTests
{
    [TestMethod]
    public Task NarrowHeader_WrapsActionsBelowTitleWithoutOverlap() => AssetLibraryP3PerformanceDiagnosticsTests.RunSta(() =>
    {
        foreach (var width in new[] { 460d, 720, 980, 1600 })
        {
            var header = new StudioPageHeader();
            var title = new TextBlock { Text = "摄影收支 · 当前项目", FontSize = 24 };
            var actions = new WrapPanel();
            for (var i = 0; i < 3; i++) actions.Children.Add(new Button { Content = "项目操作", Width = 110, Height = 34, MinHeight = 34 });
            header.Children.Add(title); header.Children.Add(actions);
            header.Measure(new Size(width, double.PositiveInfinity));
            header.Arrange(new Rect(0, 0, width, header.DesiredSize.Height));
            header.UpdateLayout();
            var titleBounds = title.TransformToAncestor(header).TransformBounds(new Rect(title.RenderSize));
            var actionBounds = actions.TransformToAncestor(header).TransformBounds(new Rect(actions.RenderSize));
            Assert.IsFalse(titleBounds.IntersectsWith(actionBounds), $"Header collision at {width}");
            Assert.IsLessThanOrEqualTo(width + .5, actionBounds.Right);
            Assert.IsLessThanOrEqualTo(35d, ((Button)actions.Children[0]).ActualHeight);
        }
        return Task.CompletedTask;
    });
}
