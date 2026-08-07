using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc2CalendarTetherTests
{
    [TestMethod]
    public void WorkbenchCalendarSharesTheFormalCalendarViewModel()
    {
        var main = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        var calendar = Text("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml");
        StringAssert.Contains(main, "DataContext=\"{Binding WorkCalendarPage}\"");
        StringAssert.Contains(calendar, "ItemsSource=\"{Binding Month.Days}\"");
        StringAssert.Contains(calendar, "DaySchedule.Bookings");
        StringAssert.Contains(calendar, "NewBookingCommand");
        StringAssert.Contains(calendar, "<conv:ZeroIntToVisibilityConverter x:Key=\"ZeroIntToVisibilityConverter\" />");
        var fullCalendar = Text("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml");
        StringAssert.Contains(fullCalendar, "DataContext.IsMonthView, RelativeSource={RelativeSource AncestorType={x:Type views:WorkCalendarView}}");
        StringAssert.Contains(fullCalendar, "DataContext.IsDetailsOpen, RelativeSource={RelativeSource AncestorType={x:Type views:WorkCalendarView}}");
    }

    [TestMethod]
    public void CompactCalendarSupportsMouseKeyboardContextAndVisibleMarkers()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml");
        var code = Text("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml.cs");
        foreach (var token in new[] { "DayCell_MouseLeftButtonDown", "DayCell_PreviewKeyDown", "CreateBooking_Click", "BookingCountText", "WorkflowSegments", "ConflictGlyph", "WeatherGlyph" })
            StringAssert.Contains(xaml + code, token);
        foreach (var token in new[] { "Key.Left", "Key.Right", "Key.Up", "Key.Down", "Key.Enter", "Key.Space" })
            StringAssert.Contains(code, token);
    }

    [TestMethod]
    public void CompletionUsesFormalStateAndAtomicRepositoryOperation()
    {
        var viewModel = Text("src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs");
        var repository = Text("src/RAWSelectionAssistant.Core/Services/Database/SqliteShootBookingRepository.cs");
        StringAssert.Contains(viewModel, "CompleteCommand");
        StringAssert.Contains(viewModel, "ShootBookingStatus.Completed");
        StringAssert.Contains(repository, "BeginTransactionAsync");
        StringAssert.Contains(repository, "Status='Completed'");
        StringAssert.Contains(repository, "BookingReminders SET IsEnabled=0");
        Assert.DoesNotContain("DELETE FROM BookingReminders", repository, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void TetherWorkspaceUsesCompactToolbarBrowserCanvasAndGroupedInspector()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        foreach (var token in new[] { "ToggleBrowser_Click", "BrowserSplitterColumn", "PreviewViewport", "InspectorTemplate", "拍摄会话", "直方图", "拍摄信息", "标记与备注", "LUT 与色彩", "客户监看", "文件处理", "问题与恢复" })
            StringAssert.Contains(xaml, token);
        StringAssert.Contains(xaml, "UserFacingProviderText");
        Assert.DoesNotContain("Text=\"{Binding ProviderText}\"", xaml, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Rc2ReviewUsesRealWpfIsolatedProfileAndExactFortyTwoFrames()
    {
        var script = Text("tools/RC2Review/Invoke-RC2Review.ps1");
        var controller = Text("src/RAWSelectionAssistant/MainWindow.AutomatedDpiAcceptance.cs");
        StringAssert.Contains(script, "KitaoPhotoSelector.UiReview.exe");
        StringAssert.Contains(script, "real-wpf-render-target-capture");
        StringAssert.Contains(script, "ExpectedScreenshotCount=42");
        StringAssert.Contains(script, "UniqueScreenshotHashes");
        StringAssert.Contains(script, "PhysicalSecondMonitorTested=$false");
        Assert.AreEqual(42, Count(script, ".png','"), "The scenario matrix must contain exactly 42 screenshot entries.");
        StringAssert.Contains(controller, "CreateBookingEditorReviewStateAsync");
        StringAssert.Contains(controller, "ApplyCalendarReviewState(state)");
    }

    [TestMethod]
    public void Rc2InstallerHasIndependentCandidateNameAndPreservesRc1Gate()
    {
        var installer = Text("installer/RAWSelectionAssistant.iss");
        var release = Text("tools/RC2Release/Invoke-RC2Release.ps1");
        StringAssert.Contains(installer, "CandidateRc2");
        StringAssert.Contains(release, "_Setup_2.3.0_RC2_x64.exe");
        StringAssert.Contains(release, "_Setup_2.3.0_RC1_x64.exe");
        StringAssert.Contains(release, "RC1 installer changed while producing RC2");
        StringAssert.Contains(release, "PhysicalSecondMonitorTested=$false");
        StringAssert.Contains(release, "NoSyntheticAssets=$true");
    }

    [TestMethod]
    public void UserFacingOptionsAndDatePickerNeverFallBackToObjectNamesOrWhiteSurface()
    {
        var calendar = Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");
        var editor = Text("src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs");
        var documents = Text("src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs");
        var inputs = Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml");
        Assert.IsGreaterThanOrEqualTo(3, Count(calendar, "public override string ToString() => Label;"));
        Assert.IsGreaterThanOrEqualTo(3, Count(editor, "public override string ToString() => Label;") + Count(editor, "public override string ToString() => Name;"));
        StringAssert.Contains(documents, "public override string ToString() => Label;");
        StringAssert.Contains(inputs, "<Style TargetType=\"DatePickerTextBox\">");
        StringAssert.Contains(inputs, "Background\" Value=\"{DynamicResource InputBackgroundBrush}");
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static int Count(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
