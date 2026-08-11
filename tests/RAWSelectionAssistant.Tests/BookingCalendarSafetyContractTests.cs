namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class BookingCalendarSafetyContractTests
{
    [TestMethod]
    public void EditorLifecycle_UsesOneDiscardGateForCancelReplacementAndWindowClose()
    {
        var source = NormalizeNewlines(Text("src/RAWSelectionAssistant/MainWindow.xaml.cs"));

        StringAssert.Contains(source, "private bool TryCloseBookingEditorVisual()");
        StringAssert.Contains(source, "!_activeBookingEditor.WasSaved && !_activeBookingEditor.ConfirmDiscardChanges()");
        StringAssert.Contains(source, "if (!TryCloseBookingEditorVisual()) return;");
        StringAssert.Contains(source, "if (!TryCloseBookingEditorVisual())\n        {\n            e.Cancel = true;");
        StringAssert.Contains(source, "private void ActiveBookingEditor_CloseRequested(object? sender, EventArgs e)\n    {\n        TryCloseBookingEditorVisual();");
    }

    [TestMethod]
    public void CalendarNewBookingCommand_UsesExplicitRefreshWithoutCommandManager()
    {
        var source = NormalizeNewlines(Text("src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs"));

        Assert.IsGreaterThanOrEqualTo(7, Count(source, "RefreshNewBookingCommandState();"));
        StringAssert.Contains(source, "(NewBookingCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged()");
        StringAssert.Contains(source, "await _availabilityStore.SetClosedAsync(date, closed).ConfigureAwait(true);\n        Month.Configure(SelectedDate, Month.AllItems, SelectedDate, _currentWeather);\n        RefreshNewBookingCommandState();");
        Assert.IsFalse(source.Contains("CommandManager", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BookingEditor_DocumentDraftOperationsAreUnsavedChanges()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/BookingEditorViewModels.cs");

        StringAssert.Contains(source, "public bool HasUnsavedChanges => Documents?.HasDraftOperations == true ||");
        StringAssert.Contains(source, "if (!HasUnsavedChanges) return true;");
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) count++;
        return count;
    }

    private static string NormalizeNewlines(string source) => source.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }
}
