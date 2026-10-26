using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class UiCopyCleanupLayoutTests
{
    static IEnumerable<T> Walk<T>(DependencyObject root)where T:DependencyObject{if(root is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Walk<T>(VisualTreeHelper.GetChild(root,i)))yield return item;}
    [TestMethod]public Task PlanningSidebarUsesRowsOnePrimaryImportAndNoIdleCancelAtNarrowWidths()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var view=new PlanningCenterView{DataContext=f.Vm};
        foreach(var (width,scale) in new[]{(860d,1d),(1100d,1.25),(1280d,1.5)})
        {
            view.LayoutTransform=new ScaleTransform(scale,scale);view.Measure(new Size(width*scale,800*scale));view.Arrange(new Rect(0,0,width*scale,800*scale));view.UpdateLayout();var search=(TextBox)view.FindName("ProjectSearchBox");var pane=(Border)view.FindName("DocumentListPane");var import=Walk<Button>(pane).Single(b=>ReferenceEquals(b.Command,f.Vm.ImportCurrentProjectPackageCommand));var cancel=Walk<Button>(pane).Single(b=>ReferenceEquals(b.Command,f.Vm.CancelProjectPackageCommand));Assert.AreEqual(Visibility.Collapsed,cancel.Visibility);Assert.IsFalse(Walk<Button>(pane).Any(b=>ReferenceEquals(b.Command,f.Vm.ImportProjectPackageCommand)));Assert.IsFalse(Walk<Button>(pane).Any(b=>ReferenceEquals(b.Command,f.Vm.OpenImportedProjectCommand)));Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(import)));var point=import.TransformToAncestor(pane).Transform(new Point(0,import.ActualHeight));var searchPoint=search.TransformToAncestor(pane).Transform(new Point());Assert.IsTrue(searchPoint.Y>=point.Y-1,"Search must be below the import row, not overlaid");if(pane.ActualWidth>0){Assert.IsTrue(search.ActualWidth>100);Assert.IsTrue(import.ActualWidth<=pane.ActualWidth+1);}else Assert.IsTrue(((ColumnDefinition)view.FindName("DocumentListColumn")).Width.Value==0,"Responsive hidden sidebar is not a clipped visible surface");
        }
        var card=new PlanningProjectCard(Guid.NewGuid(),"同名",null,null,0,0,DateTimeOffset.UtcNow);Assert.IsTrue(card.ShortIdentity.Length>=9);view.DataContext=null;
    });
}
