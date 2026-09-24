using System.IO;
using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ReferenceWorkspaceWideRatioTests
{
    [TestMethod]
    public void ColorStudioProductionNodeControlsAndSchemeNavigationTests()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current is null) { var app = new App(); app.InitializeComponent(); }
                using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs());
                var editor = workspace.Editor; editor.WorkspaceMode = "专业";
                var view = new ReferenceColorWorkspaceView { DataContext = workspace };
                Arrange(view, 1180, 720);
                var list = (System.Windows.Controls.ListBox)view.FindName("AdjustmentNodeList");
                var first = editor.AdjustmentNodes[0]; list.SelectedItem = first;
                var row = (System.Windows.Controls.ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                var toggle = Descendants<System.Windows.Controls.CheckBox>(row).Single();
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Drain(); Arrange(view, 1180, 720);
                Assert.IsFalse(editor.AdjustmentNodes[0].Enabled);
                Assert.AreEqual(editor.SelectedAdjustmentNode, list.SelectedItem);
                row = (System.Windows.Controls.ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                var overflow = Descendants<System.Windows.Controls.Button>(row).Single();
                overflow.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                var menu = overflow.ContextMenu!;
                Assert.IsTrue(menu.IsOpen);
                Assert.IsFalse(menu.Items.OfType<System.Windows.Controls.MenuItem>().Single(i => Equals(i.Tag, "up")).IsEnabled);
                Assert.IsTrue(menu.Items.OfType<System.Windows.Controls.MenuItem>().Single(i => Equals(i.Tag, "down")).IsEnabled);
                Assert.AreSame(view.FindResource("PixelTart.Menu.Context"), menu.Style);
                menu.IsOpen = false;
                var border = Descendants<System.Windows.Controls.Border>(row).Single(x => x.Name == "NodeRow");
                Assert.AreEqual(36, border.Height); Assert.AreEqual(.48, border.Opacity);
                Assert.IsTrue(row.IsSelected, "Selection must survive replacing the stack.");
                Assert.IsNotNull(row.Background, "Selection wash must be present after replacing the stack.");
                editor.WorkspaceSection = "预设"; Arrange(view, 1180, 720);
                Assert.AreEqual(Visibility.Collapsed, ((FrameworkElement)list.Parent).Visibility);
                Assert.IsFalse(editor.IsNodeSection);
                foreach (var label in new[] { "当前色彩方案", "我的方案" })
                    Assert.IsTrue(Descendants<System.Windows.Controls.TextBlock>(view).Any(t => t.Text == label));
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "Bounded UI integration test.");
        if (failure is not null) throw failure;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Drain()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    [TestMethod]
    public void WideLayoutKeepsTwentyOneSixtyOneEighteenColumns()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs"));
        StringAssert.Contains(source, "new GridLength(320)");
        StringAssert.Contains(source, "new GridLength(.63, GridUnitType.Star)");
        StringAssert.Contains(source, "new GridLength(.18, GridUnitType.Star)");
        Assert.IsFalse(source.Contains("CenterColumn.Width = new GridLength(1, GridUnitType.Star)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CompactLayoutCollapsesContextUntilUserOpensIt()
    {
        var viewModel = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs"));
        var code = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs"));
        StringAssert.Contains(viewModel, "SetResponsiveContext(bool compact)");
        StringAssert.Contains(code, "if (compact && !_wasCompact) _editor?.SetResponsiveContext(true)");
        StringAssert.Contains(viewModel, "private bool _contextRailOpen = true");
    }

    [TestMethod]
    public void FilmSurfaceUsesChineseTextureGridAndProfiles()
    {
        var film = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant.Core/Services/Projects/PixelTartFilm.cs"));
        var view = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml"));
        foreach (var value in new[] { "中性胶片", "暖调柔化", "冷银灰", "细纤维", "纸面颗粒", "柔雾", "扫描细纹" }) StringAssert.Contains(film, value);
        StringAssert.Contains(view, "SelectedValuePath=\"Id\"");
        StringAssert.Contains(view, "DisplayName");
    }

    [TestMethod]
    public void RuntimeGeometry_UsesWideRatioAndResponsiveRails()
    {
        Exception? failure = null; var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current is null) { var app = new App(); app.InitializeComponent(); }
                using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs()); var editor = workspace.Editor; var view = new ReferenceColorWorkspaceView { DataContext = workspace };
                Arrange(view, 1920, 900); var columns = FindColumns(view); Assert.AreEqual(320, columns[0], .5); Assert.IsGreaterThan(columns[0] * 2, columns[1]); Assert.IsGreaterThanOrEqualTo(224, columns[2]);
                Arrange(view, 1439, 900); Assert.IsFalse(editor.ContextRailOpen); var right = FindNamed<FrameworkElement>(view, "RightRail"); Assert.IsFalse(right.IsVisible);
                editor.ContextRailOpen = true; Arrange(view, 1200, 800); Assert.AreEqual(Visibility.Visible, right.Visibility); Assert.IsTrue(right.ActualWidth is >= 280 and <= 340, $"drawer width {right.ActualWidth}");
                Arrange(view, 739, 760); var left = FindNamed<FrameworkElement>(view, "LeftRail"); Assert.IsFalse(left.IsVisible);
                Arrange(view, 1180, 720); Assert.IsTrue(right.ActualWidth is >= 280 and <= 340);
                Assert.IsLessThanOrEqualTo(1180, FindNamed<FrameworkElement>(view, "WorkspaceGrid").ActualWidth);
                editor.FocusView = true; Arrange(view, 1600, 900); Assert.IsFalse(left.IsVisible); Assert.IsFalse(right.IsVisible);
            }
            catch (Exception ex) { failure = ex; }
        }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20))); if (failure is not null) throw failure;
    }

    private static void Arrange(FrameworkElement view, double width, double height) { view.Width = width; view.Height = height; view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout(); }
    private static double[] FindColumns(ReferenceColorWorkspaceView view) { var grid = FindNamed<System.Windows.Controls.Grid>(view, "WorkspaceGrid"); return grid.ColumnDefinitions.Select(x => x.ActualWidth).ToArray(); }
    private static T FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement { if (root is T match && match.Name == name) return match; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { try { return FindNamed<T>(VisualTreeHelper.GetChild(root, i), name); } catch (InvalidOperationException) { } } throw new InvalidOperationException(name); }
    private sealed class TestDialogs : RAWSelectionAssistant.Services.IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect) => [];
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => currentToolIds;
        public void ShowInfo(string message) { } public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false;
        public RAWSelectionAssistant.Services.HelpAction ShowHelp() => RAWSelectionAssistant.Services.HelpAction.None;
        public void ShowFeedback() { }
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
