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
    public void WideLayoutKeepsTwentyOneSixtyOneEighteenColumns()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs"));
        StringAssert.Contains(source, "compact ? new GridLength(300) : new GridLength(.19, GridUnitType.Star)");
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
                Arrange(view, 1920, 900); var columns = FindColumns(view); var total = columns.Sum(); Assert.AreEqual(.19, columns[0] / total, .02); Assert.AreEqual(.63, columns[1] / total, .02); Assert.AreEqual(.18, columns[2] / total, .02);
                Arrange(view, 1439, 900); Assert.IsFalse(editor.ContextRailOpen); var right = FindNamed<FrameworkElement>(view, "RightRail"); Assert.IsFalse(right.IsVisible);
                editor.ContextRailOpen = true; Arrange(view, 1200, 800); Assert.AreEqual(Visibility.Visible, right.Visibility); Assert.IsTrue(right.ActualWidth is >= 280 and <= 340, $"drawer width {right.ActualWidth}");
                Arrange(view, 979, 760); var left = FindNamed<FrameworkElement>(view, "LeftRail"); Assert.IsFalse(left.IsVisible);
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
