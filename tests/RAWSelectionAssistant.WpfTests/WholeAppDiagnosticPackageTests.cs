using System.IO;
using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class WholeAppDiagnosticPackageTests
{
 [TestMethod]public Task FullShellKeepsPrimaryNavigationAndContentTemplates()=>RunSta(()=>
 {
  EnsureTestApplication();var window=new MainWindow();window.Measure(new System.Windows.Size(1600,920));window.Arrange(new System.Windows.Rect(0,0,1600,920));
  Assert.IsNotNull(window.Content);Assert.AreEqual(10,PrimaryNavigationPolicy.OrderedPages.Count);
  var root=TestResources.Root;var text=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/MainWindow.xaml"));
  foreach(var page in PrimaryNavigationPolicy.OrderedPages)Assert.IsTrue(text.Contains("CommandParameter=\""+page+"\""),page);
  Assert.IsTrue(text.Contains("PlanningCenterView"));Assert.IsTrue(text.Contains("ReferenceColorWorkspaceView"));return Task.CompletedTask;
 });
 [TestMethod]public Task SaveDiscardCancelRetainsThreeDistinctAccessibleActions()=>RunSta(()=>
 {
  EnsureTestApplication();var method=typeof(RAWSelectionAssistant.Views.ThemedMessageDialog).GetMethod("CreateSaveDiscardCancel",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
  var dialog=(RAWSelectionAssistant.Views.ThemedMessageDialog)method.Invoke(null,["切换方案","合成测试"] )!;
  var save=(System.Windows.Controls.Button)dialog.FindName("YesButton");var discard=(System.Windows.Controls.Button)dialog.FindName("DiscardButton");var cancel=(System.Windows.Controls.Button)dialog.FindName("NoButton");
  Assert.AreEqual("保存并切换",save.Content);Assert.AreEqual("放弃并切换",discard.Content);Assert.AreEqual("取消",cancel.Content);Assert.AreEqual(System.Windows.Visibility.Visible,discard.Visibility);
  var markup=File.ReadAllText(Path.Combine(TestResources.Root,"src/RAWSelectionAssistant/Views/ThemedMessageDialog.xaml"));Assert.IsTrue(markup.Contains("DiscardButton_Click"));return Task.CompletedTask;
 });
 [TestMethod]public void DiagnosticBuildRejectsFocusedShellAndGuardsDataBeforeMigration()
 {
  var root=TestResources.Root;var project=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"));var app=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/App.xaml.cs"));
  Assert.IsTrue(project.Contains("RejectMixedDiagnosticShell"));Assert.IsTrue(app.IndexOf("#if WHOLE_APP_DIAGNOSTIC")<app.IndexOf("new AppDataMigrationService"));
  Assert.IsTrue(app.Contains("完整范围诊断预览 · 未完成验证 / 不能发行"));
 }
}
