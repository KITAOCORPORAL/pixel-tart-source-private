using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ToolProgressBindingSafetyTests
{
    [TestMethod]
    [DataRow("src/RAWSelectionAssistant/Views/RawToJpegModal.xaml")]
    [DataRow("src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml")]
    public void ReadOnlyProgress_UsesExplicitOneWayBinding(string relativePath)
    {
        var xaml = Read(relativePath);
        StringAssert.Contains(xaml, "Value=\"{Binding Progress, Mode=OneWay}\"");
        Assert.IsFalse(xaml.Contains("Value=\"{Binding Progress}\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("Value=\"{Binding Progress, Mode=TwoWay}\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("Value=\"{Binding Progress, Mode=OneWayToSource}\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FullPlanningReview_RejectsMissingDocumentsViewModel()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.AutomatedDpiAcceptance.cs");
        StringAssert.Contains(source, "BookingFullPlanning requires a live BookingDocumentsViewModel.");
        StringAssert.Contains(source, "editor.Documents is null");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
