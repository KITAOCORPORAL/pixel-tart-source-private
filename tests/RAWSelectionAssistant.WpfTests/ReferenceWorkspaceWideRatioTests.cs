using System.IO;
using System.Windows;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ReferenceWorkspaceWideRatioTests
{
    [TestMethod]
    public Task ColorStudioProductionNodeControlsAndSchemeNavigationTests() => RuntimeCorrectionWpfTests.RunSta(async () =>
    {
        RuntimeCorrectionWpfTests.EnsureTestApplication();
        using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var editor = workspace.Editor; editor.WorkspaceMode = "专业";
        var view = new ReferenceColorWorkspaceView { DataContext = workspace };
        editor.ContextRailOpen = true;
        ((System.Windows.Controls.ListBox)view.FindName("AuxModes")).SelectedIndex = 3;
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
        ((System.Windows.Controls.ListBox)view.FindName("AuxModes")).SelectedIndex = 2; Arrange(view, 1180, 720);
        Assert.AreEqual(Visibility.Collapsed, ((FrameworkElement)view.FindName("NodesPage")).Visibility);
        Assert.AreEqual(Visibility.Visible, ((FrameworkElement)view.FindName("PresetsPage")).Visibility);
        foreach (var label in new[] { "当前色彩方案", "我的方案" })
            Assert.IsTrue(Descendants<System.Windows.Controls.TextBlock>(view).Any(t => t.Text == label));
        await Task.CompletedTask;
    });

    [TestMethod]
    public void MatchV4ProductSelectorDefaultsToStableAndExposesAutoCpu()
    {
        using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var editor = workspace.Editor;
        Assert.AreEqual(ColorStudioMatchEngine.Stable, editor.MatchEngine);
        CollectionAssert.Contains(editor.MatchEngines.ToArray(), ColorStudioMatchEngine.MatchV4Beta);
        CollectionAssert.Contains(editor.MatchV4ExecutionModes.ToArray(), MatchV4ExecutionMode.Auto);
        CollectionAssert.Contains(editor.MatchV4ExecutionModes.ToArray(), MatchV4ExecutionMode.Cpu);
        editor.MatchEngine = ColorStudioMatchEngine.MatchV4Beta;
        Assert.IsTrue(editor.IsMatchV4Beta);
        StringAssert.Contains(editor.MatchV4Status, "CPU");
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
    public Task ReferenceIsLeftAndTwoHistogramsStayAboveRightTools() => RuntimeCorrectionWpfTests.RunSta(async () =>
    {
        RuntimeCorrectionWpfTests.EnsureTestApplication();
        using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var view = new ReferenceColorWorkspaceView { DataContext = workspace };
        Arrange(view, 1920, 1080);
        workspace.Editor.ContextRailOpen = true; Arrange(view,1920,1080);
        var context = (FrameworkElement)view.FindName("ContextRail");
        var editing = (FrameworkElement)view.FindName("EditingRail");
        var analysis = (FrameworkElement)view.FindName("CentralAnalysis");
        var photo = (FrameworkElement)view.FindName("PreviewCanvas");
        var rgb = (FrameworkElement)view.FindName("RgbHistogram");
        var luma = (FrameworkElement)view.FindName("LumaHistogram");
        var tools = (System.Windows.Controls.ListBox)view.FindName("ToolModes");
        var aux = (System.Windows.Controls.ListBox)view.FindName("AuxModes");
        Assert.AreEqual(0, System.Windows.Controls.Grid.GetColumn(context));
        Assert.AreEqual(2, System.Windows.Controls.Grid.GetColumn(editing));
        Assert.IsTrue(context.TranslatePoint(new Point(),view).X < photo.TranslatePoint(new Point(),view).X);
        Assert.IsTrue(photo.TranslatePoint(new Point(photo.ActualWidth,0),view).X <= analysis.TranslatePoint(new Point(),view).X);
        Assert.IsTrue(analysis.ActualWidth <= editing.ActualWidth);
        Assert.IsTrue(rgb.TranslatePoint(new Point(0,rgb.ActualHeight),view).Y <= luma.TranslatePoint(new Point(),view).Y);
        Assert.IsTrue(luma.TranslatePoint(new Point(0,luma.ActualHeight),view).Y <= tools.TranslatePoint(new Point(),view).Y);
        Assert.HasCount(4, aux.Items); Assert.HasCount(7, tools.Items);
        foreach (var (id, page) in new[] { (0,"ReferencePage"), (1,"SpacePage"), (2,"PresetsPage"), (3,"NodesPage") })
        { aux.SelectedIndex=id; Arrange(view,1920,1080); Assert.AreEqual(Visibility.Visible,((FrameworkElement)view.FindName(page)).Visibility); }
        foreach (var (id, page) in new[] { (0,"SpaceToolPage"), (1,"ColorToolPage"), (2,"LevelsToolPage"), (3,"CurveToolPage"), (4,"DetailsToolPage"), (5,"FilmToolPage"), (6,"CreativeToolPage") })
        { tools.SelectedIndex=id; Arrange(view,1920,1080); Assert.AreEqual(Visibility.Visible,((FrameworkElement)view.FindName(page)).Visibility); Assert.IsTrue(((FrameworkElement)view.FindName(page)).ActualHeight>0); }
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task CompactLayoutCollapsesContextUntilUserOpensIt() => RuntimeCorrectionWpfTests.RunSta(async () =>
    {
        RuntimeCorrectionWpfTests.EnsureTestApplication();
        using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs());
        var view = new ReferenceColorWorkspaceView { DataContext = workspace };
        var window = new Window { Content = view, Width = 1600, Height = 920, ShowInTaskbar = false, ShowActivated = false, Left = -32000 };
        try
        {
            window.Show(); Drain(); window.UpdateLayout();
            Assert.IsTrue(workspace.Editor.ContextRailOpen);
            window.Width = 1050; Drain(); window.UpdateLayout();
            Assert.IsFalse(workspace.Editor.ContextRailOpen);
            workspace.Editor.ContextRailOpen = true; Drain(); window.UpdateLayout();
            Assert.AreEqual(Visibility.Visible, ((FrameworkElement)view.FindName("ContextRail")).Visibility);
            Assert.IsTrue(((FrameworkElement)view.FindName("PreviewCanvas")).ActualWidth > 350);
        }
        finally { window.Close(); }
        await Task.CompletedTask;
    });

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
    public Task RuntimeGeometry_UsesWideRatioAndResponsiveRails() => RuntimeCorrectionWpfTests.RunSta(async () =>
    {
        RuntimeCorrectionWpfTests.EnsureTestApplication();
        using var workspace = new ReferenceColorWorkspaceViewModel(new TestDialogs()); var editor = workspace.Editor; var view = new ReferenceColorWorkspaceView { DataContext = workspace };
        Arrange(view, 1920, 900); editor.ContextRailOpen=true; Arrange(view,1920,900); var columns = FindColumns(view); Assert.AreEqual(290, columns[0], .5); Assert.IsGreaterThan(columns[0] * 2, columns[1]); Assert.IsGreaterThanOrEqualTo(224, columns[2]);
        var targetWidth = columns[1];
        editor.ContextRailOpen = false; Arrange(view, 1920, 900);
        Assert.IsGreaterThan(targetWidth, FindColumns(view)[1], "Hiding reference must reclaim space for the target photograph.");
        editor.ContextRailOpen = true; Arrange(view, 1920, 900);
        Assert.AreEqual(targetWidth, FindColumns(view)[1], .5);
        // Loaded-window automatic collapse is exercised separately above. Here measure both explicit rail states.
        editor.ContextRailOpen = false; Arrange(view, 1000, 900); var context = FindNamed<FrameworkElement>(view, "ContextRail"); Assert.AreEqual(Visibility.Collapsed, context.Visibility);
        editor.ContextRailOpen = true; Arrange(view, 1200, 800); Assert.AreEqual(Visibility.Visible, context.Visibility); Assert.IsTrue(context.ActualWidth is >= 230 and <= 300, $"rail width {context.ActualWidth}");
        Arrange(view, 739, 760); var edit = FindNamed<FrameworkElement>(view, "EditingRail"); Assert.AreEqual(Visibility.Visible, edit.Visibility); Assert.AreEqual(Visibility.Collapsed, context.Visibility);
        Arrange(view, 1180, 720); Assert.IsTrue(FindNamed<FrameworkElement>(view,"PreviewCanvas").ActualWidth>400);
        Assert.IsLessThanOrEqualTo(1180, FindNamed<FrameworkElement>(view, "WorkspaceGrid").ActualWidth);
        editor.FocusView = true; Arrange(view, 1600, 900); Assert.AreEqual(Visibility.Collapsed, edit.Visibility); Assert.AreEqual(Visibility.Collapsed, context.Visibility);
        await Task.CompletedTask;
    });

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
