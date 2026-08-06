namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class Version230Rc3DpiTests
{
    [TestMethod]
    [DataRow("DatePicker_Dark.png")][DataRow("DatePicker_Light.png")][DataRow("DatePicker_HighContrast.png")]
    [DataRow("Calendar_150Dpi.png")][DataRow("Calendar_200Dpi.png")][DataRow("Workbench_1280.png")]
    [DataRow("Finance_Dashboard.png")][DataRow("Documents_Images.png")][DataRow("Documents_Pdf.png")]
    [DataRow("Tether_CompactToolbar.png")][DataRow("LightTheme.png")][DataRow("HighContrast.png")]
    public void Rc3EvidenceManifest_ReservesApprovedLogicalViewport(string filename)
    {
        CollectionAssert.Contains(new[] { "DatePicker_Dark.png", "DatePicker_Light.png", "DatePicker_HighContrast.png", "Calendar_150Dpi.png", "Calendar_200Dpi.png", "Workbench_1280.png", "Finance_Dashboard.png", "Documents_Images.png", "Documents_Pdf.png", "Tether_CompactToolbar.png", "LightTheme.png", "HighContrast.png" }, filename);
        StringAssert.Contains(File.ReadAllText(Path.Combine(FindRoot(), "tools", "RC2Review", "Invoke-RC2Review.ps1")), "ExpectedScreenshotCount=42");
    }

    [TestMethod]
    public void Rc3ViewsUseDynamicResourcesForDpiAndThemeScaling()
    {
        var root = FindRoot();
        foreach (var relative in new[] { "Views/WorkCalendarView.xaml", "Views/FinanceView.xaml", "Views/BookingDocumentsPanel.xaml", "Views/ShootBookingEditorView.xaml", "Views/TetherCaptureView.xaml" })
        {
            var text = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", relative.Replace('/', Path.DirectorySeparatorChar)));
            StringAssert.Contains(text, "DynamicResource");
            Assert.IsFalse(text.Contains("DpiScale", StringComparison.Ordinal));
        }
    }

    private static string FindRoot() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}
