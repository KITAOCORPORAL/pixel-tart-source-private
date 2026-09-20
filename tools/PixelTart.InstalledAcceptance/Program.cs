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
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("--validate" or "--run"))
        { Console.Error.WriteLine("Usage: PixelTart.InstalledAcceptance --validate|--run plan.json"); return 2; }
        try
        {
            var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(args[1]), Json) ?? throw new InvalidDataException("Empty plan");
            Validate(plan);
            if (args[0] == "--validate") { Console.WriteLine("Plan schema/path/hash validation PASS. NO UI executed."); return 0; }
            return new Runner(plan).Run();
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    internal static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    internal static string Child(string root, string relative)
    {
        var parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(parent, relative));
        if (!result.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escapes isolated root: " + relative);
        return result;
    }
    private static void Validate(Plan p)
    {
        if (p.ProductSourceSha.Length != 40 || !p.ProductSourceSha.All(Uri.IsHexDigit)) throw new InvalidDataException("Expected full source SHA");
        var safeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelTart-TestAcceptance");
        var root = Child(safeRoot, p.RunDirectory);
        if (Path.GetFileName(root) != "InstalledAcceptance_" + p.ProductSourceSha[..7]) throw new InvalidDataException("Unexpected isolated run name");
        if (!File.Exists(p.InstallerPath) || Hash(p.InstallerPath) != p.InstallerSha256) throw new InvalidDataException("Installer hash mismatch");
        if (p.Steps.Length == 0) throw new InvalidDataException("No steps");
        var known = new[] { "invoke", "setValue", "select", "expand", "focusKey", "assertPresent", "assertAbsent", "assertText", "capture", "close", "restart", "dump" };
        if (p.Steps.Select(s => s.Id).Distinct().Count() != p.Steps.Length) throw new InvalidDataException("Duplicate step IDs");
        foreach (var s in p.Steps)
        {
            if (!known.Contains(s.Action)) throw new InvalidDataException("Unknown action: " + s.Action);
            if (s.Action is not ("capture" or "close" or "restart" or "dump") && s.IdSelector is null && s.Name is null) throw new InvalidDataException("Selector required: " + s.Id);
            if (s.Action == "focusKey" && s.Value is not ("{ESC}" or "+{F10}" or "{ENTER}" or "{TAB}")) throw new InvalidDataException("Unsupported keyboard action");
            if (s.Action == "capture") Child(root, s.Value ?? throw new InvalidDataException("Screenshot path missing"));
        }
    }
    private sealed class Runner(Plan plan)
    {
        private readonly string root = Child(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelTart-TestAcceptance"), plan.RunDirectory);
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
                var setup = new ProcessStartInfo(plan.InstallerPath) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var arg in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/NOICONS", "/TASKS=", "/CURRENTUSER", "/CLOSEAPPLICATIONS=NO", "/DIR=" + Child(root, "installed"), "/LOG=" + Child(root, "install.log") }) setup.ArgumentList.Add(arg);
                using (var installer = Process.Start(setup) ?? throw new IOException("Installer did not start"))
                { if (!installer.WaitForExit(180000) || installer.ExitCode != 0) throw new IOException("Install failed or timed out; inspect install.log"); }
                Record(currentStep, "PASS");
                currentStep = "startup"; Start(); Record(currentStep, "PASS");
                foreach (var step in plan.Steps)
                {
                    currentStep = step.Id;
                    Execute(step);
                    Record(currentStep, "PASS");
                }
                // Never infer complete acceptance from a partial declarative plan.
                Save("PARTIAL", "Runner plan finished; full workflow, PDF, upgrade and visual review require separate evidence.");
                return 0;
            }
            catch (Exception error)
            {
                Record(currentStep, "BLOCKED", error.ToString());
                Save("BLOCKED", error.ToString());
                Console.Error.WriteLine(error);
                // Leave the test app visible for diagnosis. Never kill it as a successful close.
                return 1;
            }
        }
        private void Record(string step, string status, string? error = null)
        {
            var item = new { Step = step, Status = status, Error = error, Utc = DateTimeOffset.UtcNow };
            results.Add(item); File.AppendAllText(Child(root, "uia.jsonl"), JsonSerializer.Serialize(item) + Environment.NewLine);
        }
        private void Start()
        {
            var version = FileVersionInfo.GetVersionInfo(Child(root, "installed/PixelTart.dll")).ProductVersion ?? "";
            if (!version.Contains(plan.ProductSourceSha, StringComparison.Ordinal)) throw new InvalidDataException("Installed product source SHA mismatch");
            var start = new ProcessStartInfo(Exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Exe)! };
            start.Environment["PIXEL_TART_HUMAN_ACCEPTANCE"] = "1";
            start.Environment["PIXEL_TART_ACCEPTANCE_ROOT"] = Child(root, "fresh-data");
            process = Process.Start(start) ?? throw new IOException("App did not start"); processId = process.Id;
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(30))
            {
                if (process.HasExited) throw new IOException("App exited before MainWindow");
                var candidates = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id)).Cast<AutomationElement>()
                    .Where(e => e.Current.ControlType == ControlType.Window && e.Current.Name.Contains(plan.ProductSourceSha[..7], StringComparison.Ordinal)).ToArray();
                if (candidates.Length == 1) { window = candidates[0]; break; }
                Thread.Sleep(200);
            }
            if (window is null) throw new TimeoutException("Unique PID-bound MainWindow unavailable");
            title = window.Current.Name;
            while (watch.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(100);
            process.Refresh();
            if (process.HasExited || !process.Responding || window.Current.IsOffscreen) throw new IOException("Startup survival/visibility gate failed");
            if (!string.Equals(process.MainModule?.FileName, Exe, StringComparison.OrdinalIgnoreCase)) throw new IOException("Wrong executable path");
        }
        private AutomationElement[] Find(Step step)
        {
            if (process is null || process.HasExited) throw new IOException("Test process not running");
            Condition selector = new PropertyCondition(step.IdSelector is null ? AutomationElement.NameProperty : AutomationElement.AutomationIdProperty, step.IdSelector ?? step.Name!);
            var matches = new List<AutomationElement>();
            // Include owned dialogs/popups, but never other processes.
            foreach (AutomationElement top in AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id)))
                matches.AddRange(top.FindAll(TreeScope.Subtree, selector).Cast<AutomationElement>().Where(e => !e.Current.IsOffscreen && (step.ControlType is null || e.Current.ControlType.ProgrammaticName == "ControlType." + step.ControlType)));
            return matches.ToArray();
        }
        private AutomationElement One(Step step)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                var found = Find(step);
                if (found.Length == 1) return found[0];
                if (found.Length > 1) throw new InvalidOperationException("Ambiguous selector; refusing arbitrary match: " + step.Id);
                Thread.Sleep(200);
            }
            throw new TimeoutException("No visible unique element: " + step.Id);
        }
        private void Execute(Step s)
        {
            if (s.Action == "capture") { Capture(s.Value!); return; }
            if (s.Action == "restart") { if (process is { HasExited: false }) throw new IOException("Close first"); window = null; Start(); return; }
            if (s.Action == "close")
            {
                ((WindowPattern)window!.GetCurrentPattern(WindowPattern.Pattern)).Close();
                if (!process!.WaitForExit(15000)) throw new IOException("Normal close did not finish; inspect confirmation dialog");
                exitResult = process.ExitCode == 0 ? "PASS" : "FAIL";
                if (exitResult != "PASS") throw new IOException("Nonzero natural process exit"); return;
            }
            if (s.Action == "dump")
            {
                var nodes = window!.FindAll(TreeScope.Subtree, Condition.TrueCondition).Cast<AutomationElement>().Select(e => new { e.Current.Name, e.Current.AutomationId, Type = e.Current.ControlType.ProgrammaticName, e.Current.IsOffscreen, Patterns = e.GetSupportedPatterns().Select(p => p.ProgrammaticName).ToArray() });
                File.WriteAllText(Child(root, s.Id + ".tree.json"), JsonSerializer.Serialize(nodes, Json)); return;
            }
            if (s.Action == "assertAbsent")
            { if (Find(s).Length != 0) throw new InvalidOperationException("Element still visible: " + s.Id); return; }
            var element = One(s);
            switch (s.Action)
            {
                case "invoke": ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke(); break;
                case "setValue": ((ValuePattern)element.GetCurrentPattern(ValuePattern.Pattern)).SetValue(s.Value ?? ""); break;
                case "select": ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Select(); break;
                case "expand": ((ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand(); break;
                case "focusKey":
                    element.SetFocus();
                    GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundPid);
                    if (foregroundPid != process!.Id || AutomationElement.FocusedElement.Current.ProcessId != process.Id) throw new IOException("Input focus escaped test PID");
                    System.Windows.Forms.SendKeys.SendWait(s.Value!); break;
                case "assertText":
                    var value = element.TryGetCurrentPattern(ValuePattern.Pattern, out var vp) ? ((ValuePattern)vp).Current.Value : element.TryGetCurrentPattern(TextPattern.Pattern, out var tp) ? ((TextPattern)tp).DocumentRange.GetText(-1) : element.Current.Name;
                    if (!value.Contains(s.Value ?? "", StringComparison.Ordinal)) throw new InvalidDataException("Expected text missing: " + s.Id); break;
                case "assertPresent": break;
                default: throw new InvalidOperationException(s.Action);
            }
            Thread.Sleep(400); // bounded settling; next step re-resolves elements, never caches indexes
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
        private void Save(string status, string detail) => File.WriteAllText(Child(root, "acceptance.json"), JsonSerializer.Serialize(new {
            Status = status, Detail = detail, plan.ProductSourceSha, plan.InstallerSha256, InstalledExePath = Exe,
            InstalledExeSha256 = File.Exists(Exe) ? Hash(Exe) : null, ProcessId = processId, WindowTitle = title,
            AutomationTechnology = "System.Windows.Automation (external PID-bound UIA)", Steps = results, Screenshots = screenshots,
            ExportedPdf = (string?)null, ExitResult = exitResult, UpgradeResult = "NOT_TESTED", UserVisualAcceptance = "PENDING_USER"
        }, Json));
        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out int processId);
    }
    internal sealed record Plan(string ProductSourceSha, string InstallerPath, string InstallerSha256, string RunDirectory, Step[] Steps);
    internal sealed record Step(string Id, string Action, string? IdSelector = null, string? Name = null, string? ControlType = null, string? Value = null);
}
