using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation;

namespace PixelTart.InstalledAcceptance;

// Separate process, no reference to any production assembly. --validate never touches UI.
internal static partial class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args is ["--self-test"]) return SelfTest();
        if (args.Length == 2 && args[0] == "--kit") return RunKit(args[1]);
        if (args.Length != 2 || args[0] is not ("--validate" or "--run"))
        { Console.Error.WriteLine("Usage: PixelTart.InstalledAcceptance --validate|--run plan.json"); return 2; }
        try
        {
            PlanDirectory = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
            var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(args[1]), Json) ?? throw new InvalidDataException("Empty plan");
            Validate(plan);
            if (args[0] == "--validate") { Console.WriteLine("Plan schema/path/hash validation PASS. NO UI executed."); return 0; }
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
            return new Runner(plan).Run();
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    internal static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    internal static string Child(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("Relative path required");
        var parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(parent, relative));
        if (!result.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escapes isolated root: " + relative);
        if ((File.Exists(result) || Directory.Exists(result)) && File.GetAttributes(result).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Reparse target forbidden");
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(result)!); dir is not null; dir = dir.Parent)
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Reparse path forbidden");
        return result;
    }
    private static void Validate(Plan p)
    {
        if (p.ProductSourceSha.Length != 40 || !p.ProductSourceSha.All(Uri.IsHexDigit)) throw new InvalidDataException("Expected full source SHA");
        var safeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelTart-TestAcceptance");
        var root = Child(safeRoot, p.RunDirectory);
        if (Path.GetFileName(root) != "InstalledAcceptance_" + p.ProductSourceSha[..7]) throw new InvalidDataException("Unexpected isolated run name");
        if (Hash(Child(PlanDirectory, p.InstallerPath)) != p.InstallerSha256) throw new InvalidDataException("Installer hash mismatch");
        if (p.Steps.Length == 0) throw new InvalidDataException("No steps");
        var known = new[] { "invoke", "setValue", "select", "expand", "focusKey", "assertPresent", "assertAbsent", "assertText", "assertDate", "capture", "close", "restart", "dump", "waitPresent", "waitAbsent", "assertEnabled", "assertSelected", "assertWindowTitle", "assertProcessAlive", "assertFileExists", "assertFileHash", "assertFileSize", "assertPdfPages", "assertScreenshot", "sleep", "fixture", "upgrade", "requestClose", "waitExit" };
        if (p.Steps.Select(s => s.Id).Distinct().Count() != p.Steps.Length) throw new InvalidDataException("Duplicate step IDs");
        foreach (var s in p.Steps)
        {
            if (!known.Contains(s.Action)) throw new InvalidDataException("Unknown action: " + s.Action);
            if (s.Action is "invoke" or "setValue" or "select" or "expand" or "focusKey" or "assertPresent" or "assertAbsent" or "waitPresent" or "waitAbsent" or "assertEnabled" or "assertSelected" or "assertText")
                if (s.IdSelector is null && s.AutomationId is null && s.Name is null && s.DescendantName is null) throw new InvalidDataException("Selector required: " + s.Id);
            if (s.Action == "focusKey" && s.Value is not ("{ESC}" or "+{F10}" or "{ENTER}" or "{TAB}" or "{HOME}" or "{LEFT}" or "{RIGHT}" or "{UP}" or "{DOWN}")) throw new InvalidDataException("Unsupported keyboard action");
            if (s.Action == "capture") Child(root, s.Path ?? s.Value ?? throw new InvalidDataException("Screenshot path missing"));
        }
        ValidateFull(p);
    }
    private sealed partial class Runner(Plan plan)
    {
        private readonly string root = Child(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelTart-TestAcceptance"), plan.RunDirectory + "_" + plan.Mode + "_" + Guid.NewGuid().ToString("N"));
        private readonly List<object> results = [];
        private readonly List<object> screenshots = [];
        private Process? process;
        private AutomationElement? window;
        private string title = "";
        private int? processId;
        private string exitResult = "NOT_TESTED";
        private string Exe => Child(root, "installed/PixelTart.exe");
        public int Run()
        {
            // Refuse existing roots: no overwrite of earlier evidence/data and no accidental upgrade.
            if (Directory.Exists(root)) throw new IOException("Run root already exists; preserve it and choose a separately reviewed plan.");
            Directory.CreateDirectory(root);
            var currentStep = "fresh-install";
            try
            {
                Directory.CreateDirectory(OutputRoot);
                File.WriteAllText(Out("environment.txt"), $"OS={Environment.OSVersion}\nRuntime={Environment.Version}\nArchitecture={RuntimeInformation.ProcessArchitecture}\nCulture={System.Globalization.CultureInfo.CurrentCulture.Name}\nVirtualScreen={System.Windows.Forms.SystemInformation.VirtualScreen}\nIsolation={root}\nSource={plan.ProductSourceSha}\n");
                Directory.CreateDirectory(Child(root, "fresh-data/exports"));
                Install(plan.Mode == "upgrade" ? plan.OldInstallerPath! : plan.InstallerPath, plan.Mode == "upgrade" ? plan.OldInstallerSha256! : plan.InstallerSha256);
                Record(currentStep, "PASS");
                currentStep = "startup"; Start(); Record(currentStep, "PASS");
                foreach (var step in plan.Steps)
                {
                    currentStep = step.Id;
                    Console.WriteLine("正在验收：" + step.Id);
                    if (step.Optional && !OptionalPresent(step)) { Record(currentStep, "SKIPPED_EXPECTED"); continue; }
                    Execute(step);
                    Record(currentStep, "PASS");
                }
                // Never infer complete acceptance from a partial declarative plan.
                if (exitResult != "PASS" || process is { HasExited: false }) throw new IOException("Final normal exit required");
                Save(plan.FullPlan ? "TECHNICAL_ACCEPTANCE_PASS" : "PARTIAL", "Plan completed; screenshot visual review remains pending.");
                return 0;
            }
            catch (Exception error)
            {
                Record(currentStep, "BLOCKED", error.ToString());
                try { Execute(new Step("failure", "dump")); } catch (Exception dumpError) { File.WriteAllText(Out("dump-error.txt"), dumpError.ToString()); }
                File.WriteAllText(Out("AUTOMATION_GAP_REPORT.md"), $"# Failure is not proof of a product gap\nStep: {currentStep}\n{error}\nSee failure.tree.json for Names, IDs, ControlTypes and Patterns.\n");
                Save("BLOCKED", error.ToString());
                Console.Error.WriteLine(error);
                // Leave the test app visible for diagnosis. Never kill it as a successful close.
                return 1;
            }
        }
        private void Record(string step, string status, string? error = null)
        {
            var item = new { Step = step, Status = status, Error = error, Utc = DateTimeOffset.UtcNow };
            results.Add(item); File.AppendAllText(Out("uia.jsonl"), JsonSerializer.Serialize(item) + Environment.NewLine);
        }
        private void Start()
        {
            var version = FileVersionInfo.GetVersionInfo(Child(root, "installed/PixelTart.dll")).ProductVersion ?? "";
            if (!version.Contains(CurrentSource, StringComparison.Ordinal)) throw new InvalidDataException("Installed product source SHA mismatch");
            var start = new ProcessStartInfo(Exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Exe)! };
            start.Environment["PIXEL_TART_HUMAN_ACCEPTANCE"] = "1";
            start.Environment["PIXEL_TART_ACCEPTANCE_ROOT"] = Child(root, "fresh-data");
            process = Process.Start(start) ?? throw new IOException("App did not start"); processId = process.Id;
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(30))
            {
                if (process.HasExited) throw new IOException("App exited before MainWindow");
                var candidates = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id)).Cast<AutomationElement>()
                    .Where(e => e.Current.ControlType == ControlType.Window && e.Current.Name.Contains(CurrentSource[..7], StringComparison.Ordinal)).ToArray();
                if (candidates.Length == 1) { window = candidates[0]; break; }
                Thread.Sleep(200);
            }
            if (window is null) throw new TimeoutException("Unique PID-bound MainWindow unavailable");
            title = window.Current.Name;
            while (watch.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(100);
            process.Refresh();
            if (process.HasExited || !process.Responding || window.Current.IsOffscreen) throw new IOException("Startup survival/visibility gate failed");
            if (!string.Equals(process.MainModule?.FileName, Exe, StringComparison.OrdinalIgnoreCase)) throw new IOException("Wrong executable path");
            ((WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern)).SetWindowVisualState(WindowVisualState.Normal);
        }
        private AutomationElement[] Find(Step step)
        {
            // A DatePicker and its adjacent Text label can share a name. Require
            // the value provider for date operations instead of guessing a peer type.
            var found = FindScoped(step);
            return step.Action == "assertDate" || (step.Action == "setValue" && step.Name == "拍摄日期")
                ? found.Where(e => e.TryGetCurrentPattern(ValuePattern.Pattern, out _)).ToArray() : found;
        }
        private AutomationElement One(Step step)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < step.TimeoutMs)
            {
                var found = Find(step);
                if (found.Length == 1) return found[0];
                if (found.Length > 1) throw new InvalidOperationException("Ambiguous selector; refusing arbitrary match: " + step.Id);
                Thread.Sleep(200);
            }
            throw new TimeoutException((step.ExternalDialog ? "EXTERNAL_DIALOG_BLOCKED: " : "No unique element: ") + step.Id);
        }
        private void Execute(Step s)
        {
            if (ExecuteExtended(s)) return;
            if (s.Action == "capture") { CaptureExtended(s); return; }
            if (s.Action == "restart") { if (process is { HasExited: false }) throw new IOException("Close first"); window = null; Start(); return; }
            if (s.Action == "close")
            {
                ((WindowPattern)window!.GetCurrentPattern(WindowPattern.Pattern)).Close(); WaitExit(s.TimeoutMs); return;
            }
            if (s.Action == "dump")
            {
                var nodes = Roots().SelectMany(r => r.FindAll(TreeScope.Subtree, Condition.TrueCondition).Cast<AutomationElement>()).Select(e => new { e.Current.Name, e.Current.AutomationId, e.Current.HelpText, Type = e.Current.ControlType.ProgrammaticName, e.Current.IsOffscreen, Patterns = e.GetSupportedPatterns().Select(p => p.ProgrammaticName).ToArray() });
                File.WriteAllText(Out(s.Id + ".tree.json"), JsonSerializer.Serialize(nodes, Json)); return;
            }
            if (s.Action == "assertAbsent")
            { Until(() => Find(s).Length == 0, s.TimeoutMs); return; }
            var element = One(s);
            switch (s.Action)
            {
                case "invoke": ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke(); break;
                case "setValue": ((ValuePattern)element.GetCurrentPattern(ValuePattern.Pattern)).SetValue(Expand(s.Value)); break;
                case "select": ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Select(); break;
                case "expand": ((ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand(); break;
                case "focusKey":
                    element.SetFocus();
                    Until(() => { GetWindowThreadProcessId(GetForegroundWindow(), out var pid); return pid == process!.Id && AutomationElement.FocusedElement.Current.ProcessId == process.Id; }, s.TimeoutMs);
                    System.Windows.Forms.SendKeys.SendWait(s.Value!); break;
                case "assertText":
                    var value = element.TryGetCurrentPattern(ValuePattern.Pattern, out var vp) ? ((ValuePattern)vp).Current.Value : element.TryGetCurrentPattern(TextPattern.Pattern, out var tp) ? ((TextPattern)tp).DocumentRange.GetText(-1) : element.Current.Name;
                    if (!value.Contains(Expand(s.Value), StringComparison.Ordinal)) throw new InvalidDataException("Expected text missing: " + s.Id); break;
                case "assertPresent": break;
                default: throw new InvalidOperationException(s.Action);
            }
            Thread.Sleep(100); // next step re-resolves; waits are condition based
        }
        private void Capture(string relative)
        {
            window!.SetFocus(); Thread.Sleep(200);
            GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundPid);
            if (foregroundPid != process!.Id) throw new IOException("Test window is not foreground; refuse desktop capture");
            var b = window.Current.BoundingRectangle;
            var rectangle = new Rectangle((int)Math.Floor(b.X), (int)Math.Floor(b.Y), (int)Math.Ceiling(b.Width), (int)Math.Ceiling(b.Height));
            if (window.Current.IsOffscreen || rectangle.Width <= 0 || rectangle.Height <= 0 || !System.Windows.Forms.SystemInformation.VirtualScreen.Contains(rectangle)) throw new IOException("Window clipped or invisible");
            using var bitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(rectangle.Location, Point.Empty, rectangle.Size);
            var min = 255; var max = 0;
            for (var y = 0; y < bitmap.Height; y += 16) for (var x = 0; x < bitmap.Width; x += 16)
            { var c = bitmap.GetPixel(x, y); var v = Math.Max(c.R, Math.Max(c.G, c.B)); min = Math.Min(min, v); max = Math.Max(max, v); }
            if (max < 8 || max - min < 8) throw new IOException("Blank/black screenshot");
            var path = Child(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); bitmap.Save(path, ImageFormat.Png);
            screenshots.Add(new { Path = path, SHA256 = Hash(path), bitmap.Width, bitmap.Height, VisualReview = "PENDING", PopupCompleteness = "PENDING" });
        }
        private void Save(string status, string detail) => File.WriteAllText(Out("acceptance.json"), JsonSerializer.Serialize(new {
            Status = status, Detail = detail, plan.ProductSourceSha, plan.InstallerSha256, InstalledExePath = Exe,
            InstalledExeSha256 = File.Exists(Exe) ? Hash(Exe) : null, ProcessId = processId, WindowTitle = title,
            AutomationTechnology = "System.Windows.Automation (external PID-bound UIA)", Steps = results, Screenshots = screenshots,
            RunStarted = runStarted, RunEnded = DateTimeOffset.UtcNow, ExportedPdf = pdfPath, ExportedPdfSha256 = pdfPath is null ? null : Hash(pdfPath), ExportedPdfPages = pdfPages,
            ExitResult = exitResult, UpgradeResult = plan.Mode == "upgrade" && status == "TECHNICAL_ACCEPTANCE_PASS" ? "PASS" : "NOT_VERIFIED", VisualReviewStatus = "NOT_REVIEWED", UserVisualAcceptance = "PENDING_USER"
        }, Json));
        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out int processId);
    }
    internal sealed record Plan(string ProductSourceSha, string InstallerPath, string InstallerSha256, string RunDirectory, Step[] Steps, string Mode = "fresh", bool FullPlan = false, string? OldInstallerPath = null, string? OldInstallerSha256 = null, string? OldSourceSha = null);
    internal sealed record Step(string Id, string Action, string? IdSelector = null, string? Name = null, string? ControlType = null, string? Value = null,
        string? AutomationId = null, string? AncestorAutomationId = null, string? AncestorName = null, string? AncestorControlType = null,
        string? ScopeAnchorName = null, string? DescendantName = null, string? HelpText = null, bool Optional = false, bool IncludeOffscreen = false,
        string? Path = null, string? ExpectedHash = null, long Minimum = 1, int TimeoutMs = 10000, int Milliseconds = 100,
        string? Coverage = null, bool ExternalDialog = false, string[]? MustContain = null);
}
