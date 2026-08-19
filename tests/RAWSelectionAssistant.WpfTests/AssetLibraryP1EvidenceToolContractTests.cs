using System.IO;
using System.Text;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP1EvidenceToolContractTests
{
    [TestMethod]
    public void CaptureHelperRequiresExactForegroundIdentityAndRecordsPhysicalDpiAndHashes()
    {
        var script = Read("tools/AssetLibraryP1Acceptance/Capture-AssetLibraryP1WindowEvidence.ps1");

        ContainsAll(script,
            "[int]$ProcessId",
            "[string]$ExecutablePath",
            "[string]$WindowTitle",
            "GetVisibleProcessWindows",
            "Expected one global",
            "Expected one visible top-level window with the exact title",
            "GetWindowClass",
            "SoPY_Status",
            "SoPY_Comp",
            "unexpected_auxiliary_window_count",
            "single_product_main_window_verified",
            "GetForegroundWindow",
            "The exact target window is not the foreground window",
            "GetWindowRect",
            "GetDpiForWindow",
            "System.Windows.Forms",
            "Screen]::FromHandle",
            "monitor_bounds_physical_pixels",
            "monitor_working_area_physical_pixels",
            "current_horizontal_resolution",
            "current_vertical_resolution",
            "PER_MONITOR_AWARE_V2",
            "System.Drawing.Graphics.CopyFromScreen",
            "ValidateSet('ScreenPixels', 'PrintWindow')",
            "PrintWindow(PW_RENDERFULLCONTENT)",
            "Win32.PrintWindow(PW_RENDERFULLCONTENT)",
            "physical_screen_pixels",
            "physical_window_pixels",
            "Get-FileHash -LiteralPath $screenshotPath -Algorithm SHA256",
            "Get-FileHash -LiteralPath $expectedExecutablePath -Algorithm SHA256",
            "[IO.FileMode]::CreateNew",
            "window_stable_during_capture",
            "exact_pid_path_title_verified");
    }

    [TestMethod]
    public void CaptureHelperCannotGenerateInputOrOverwriteEvidence()
    {
        var script = Read("tools/AssetLibraryP1Acceptance/Capture-AssetLibraryP1WindowEvidence.ps1");

        ContainsAll(script,
            "ui_input_generated = $false",
            "synthetic_ui_events_generated = $false",
            "Capture output already exists; choose a new CaptureName");

        foreach (var forbidden in new[]
                 {
                     "SendInput",
                     "mouse_event",
                     "keybd_event",
                     "SetCursorPos",
                     "PostMessage",
                     "SendMessage",
                     "System.Windows.Automation"
                 })
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ReadmeLimitsTheHelperToPostActionRealScreenEvidence()
    {
        var readme = Read("tools/AssetLibraryP1Acceptance/README.md");

        ContainsAll(readme,
            "after a human or Computer Use action has already occurred",
            "It never moves the pointer, sends keyboard input, invokes a command, or raises a synthetic UI event.",
            "default screenshot mode is the physical screen",
            "not a crop, repaint, synthetic UI event, or image post-processing step",
            "does not by itself prove that the preceding physical action or the visible product behavior passed");
    }

    private static void ContainsAll(string source, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(source, value);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
