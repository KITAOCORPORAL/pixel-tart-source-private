using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using Microsoft.Win32;

namespace PixelTart.InstalledAcceptance;

internal static partial class Program
{
    private static string PlanDirectory = AppContext.BaseDirectory;
    private static string? KitOutput;
    private sealed record KitFile(string RelativePath, long Size, string SHA256);
    private static void VerifyKitManifest()
    {
        var entries = JsonSerializer.Deserialize<KitFile[]>(File.ReadAllText(Child(PlanDirectory, "KIT_MANIFEST.json")), Json) ?? throw new InvalidDataException("Missing manifest");
        foreach (var required in new[] { "PixelTart.InstalledAcceptance.exe", "PixelTart.InstalledAcceptance.dll", "planning-full.plan.json", "upgrade-full.plan.json" })
            if (entries.Count(e => e.RelativePath == required) != 1) throw new InvalidDataException("Manifest missing/duplicate: " + required);
        if (entries.Select(e => e.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length) throw new InvalidDataException("Duplicate manifest paths");
        foreach (var entry in entries)
        {
            var path = Child(PlanDirectory, entry.RelativePath);
            if (!File.Exists(path) || new FileInfo(path).Length != entry.Size || Hash(path) != entry.SHA256) throw new InvalidDataException("Kit hash mismatch: " + entry.RelativePath);
        }
    }
    private static int RunKit(string directory)
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        PlanDirectory = Path.GetFullPath(directory);
        var output = Child(PlanDirectory, "acceptance-result/" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(output);
        var runs = new List<object>();
        var passed = false;
        try
        {
            VerifyKitManifest();
            var plans = new[] { "planning-full.plan.json", "upgrade-full.plan.json" }.Select(file => JsonSerializer.Deserialize<Plan>(File.ReadAllText(Child(PlanDirectory, file)), Json)!).ToArray();
            foreach (var p in plans) Validate(p);
            foreach (var p in plans)
            {
                KitOutput = Child(output, p.Mode);
                var code = new Runner(p).Run(); runs.Add(new { p.Mode, ExitCode = code });
                if (code != 0) return 1;
            }
            passed = true; return 0;
        }
        catch (Exception error) { File.WriteAllText(Child(output, "preflight-error.txt"), error.ToString()); Console.Error.WriteLine(error); return 1; }
        finally
        {
            File.WriteAllText(Child(output, "acceptance.json"), JsonSerializer.Serialize(new { Status = passed ? "TECHNICAL_ACCEPTANCE_PASS" : "BLOCKED", Runs = runs, VisualReviewStatus = "NOT_REVIEWED", UserVisualAcceptance = "PENDING_USER" }, Json));
            Console.WriteLine("验收运行结束，请把此 acceptance-result 子目录提供给 Codex：\n" + output); KitOutput = null;
        }
    }
    private static void ValidateFull(Plan p)
    {
        if (p.Mode is not ("fresh" or "upgrade")) throw new InvalidDataException("Mode");
        if (p.Mode == "upgrade")
        {
            if (p.OldSourceSha?.Length != 40 || Hash(Child(PlanDirectory, p.OldInstallerPath!)) != p.OldInstallerSha256) throw new InvalidDataException("Old installer identity");
        }
        foreach (var s in p.Steps.Concat(p.AuditCheckpoints?.Values.SelectMany(x => x) ?? []))
        {
            if (!Regex.IsMatch(s.Id, "^[a-z0-9-]+$")) throw new InvalidDataException("Step ID");
            if (s.TimeoutMs is < 100 or > 60000) throw new InvalidDataException("Timeout");
            if (s.Action == "sleep" && s.Milliseconds is < 1 or > 500) throw new InvalidDataException("Sleep bound");
            if (s.Optional && (s.Coverage is not null || s.Action != "invoke")) throw new InvalidDataException("Optional only for uncounted conditional invoke");
            if (s.Path is not null) Child(PlanDirectory, s.Path);
            if (s.Action is "assertFileExists" or "assertFileHash" or "assertFileSize" or "assertPdfPages" or "assertScreenshot" && string.IsNullOrWhiteSpace(s.Path)) throw new InvalidDataException("File path required");
            if (s.ControlType is not null && typeof(ControlType).GetField(s.ControlType) is null) throw new InvalidDataException("Unknown ControlType");
            foreach (var scope in s.ScopePath ?? [])
                if (scope.ControlType is null || typeof(ControlType).GetField(scope.ControlType) is null || (scope.Heading && scope.ControlType != "Text")) throw new InvalidDataException("Invalid selector scope: " + s.Id);
            if (s.Action == "assertFileHash" && (s.ExpectedHash?.Length != 64 || !s.ExpectedHash.All(Uri.IsHexDigit))) throw new InvalidDataException("Expected file SHA256 required");
            if (s.Action == "assertDate" && (s.Name is null || !DateTime.TryParseExact(s.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))) throw new InvalidDataException("Date selector/value required");
        }
        if (!p.FullPlan) return;
        if (Lint(p).Any(i => i.Severity == "ERROR")) throw new InvalidDataException("Selector lint has errors; run --lint-plan");
        foreach (var checkpoint in p.AuditCheckpoints ?? [])
            if (!p.Steps.Any(s => s.Id == checkpoint.Key) || checkpoint.Value.Any(s => s.Action != "assertPresent" || s.Optional)) throw new InvalidDataException("Invalid audit checkpoint");
        var required = p.Mode == "fresh" ? new[] { "onboarding", "planning", "create", "date", "text-edit", "text", "references", "moodboard", "shots", "lighting", "styling", "files", "persistence", "preview", "quick-preview", "context-menu", "booking", "pdf", "tether", "return-planning", "online", "normal-close" } : new[] { "old-create", "upgrade", "persistence", "text", "references", "moodboard", "shots", "lighting", "styling", "files", "preview", "normal-close" };
        foreach (var gate in required)
            if (!p.Steps.Any(s => s.Coverage == gate && !s.Optional && (s.Action.StartsWith("assert", StringComparison.Ordinal) || s.Action is "waitAbsent" or "waitPresent" or "waitExit" or "upgrade"))) throw new InvalidDataException("Coverage missing: " + gate);
        if (p.Mode == "fresh" && p.Steps.Count(s => s.Action == "capture") < 16) throw new InvalidDataException("16 captures required");
        foreach (var file in new[] { "poppler/pdfinfo.exe", "poppler/pdftoppm.exe" }) if (!File.Exists(Child(PlanDirectory, file))) throw new FileNotFoundException(file);
    }
    private static int SelfTest()
    {
        var tests = 0;
        foreach (var path in new[] { "../escape", "C:\\outside", "/absolute", "file:stream" })
        { try { Child(Path.GetTempPath(), path); throw new Exception("Unsafe path accepted"); } catch (InvalidDataException) { tests++; } }
        if (!Child(Path.GetTempPath(), "safe/file.json").EndsWith("file.json", StringComparison.Ordinal)) throw new Exception("Safe path rejected"); tests++;
        foreach (var file in new[] { "planning-full.plan.json", "upgrade-full.plan.json" })
        {
            var p = JsonSerializer.Deserialize<Plan>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file)), Json)!;
            PlanDirectory = AppContext.BaseDirectory; Validate(p); tests++;
            try { Validate(p with { Steps = p.Steps.Where(s => s.Coverage != "persistence").ToArray() }); throw new Exception("Missing gate accepted"); }
            catch (InvalidDataException) { tests++; }
            try { Validate(p with { Steps = p.Steps.Append(p.Steps[0]).ToArray() }); throw new Exception("Duplicate accepted"); }
            catch (InvalidDataException) { tests++; }
            try { Validate(p with { InstallerSha256 = new string('0',64) }); throw new Exception("Hash mismatch accepted"); }
            catch (InvalidDataException) { tests++; }
            foreach (var bad in new[] { new Step("bad", "sleep", Milliseconds: 501), new Step("bad", "assertPdfPages"), new Step("bad", "invoke", Name: "test", Optional: true, Coverage: "planning"), new Step("bad", "focusKey", Name: "test", Value: "^a"), new Step("bad", "assertPresent", Name: "test", ControlType: "Bogus") })
            { try { Validate(p with { Steps = p.Steps.Append(bad).ToArray() }); throw new Exception("Invalid step accepted"); } catch (InvalidDataException) { tests++; } }
        }
        Console.WriteLine($"Offline self-test: {tests} PASS, NO UI executed"); return 0;
    }
    private sealed partial class Runner
    {
        private readonly DateTimeOffset runStarted = DateTimeOffset.UtcNow;
        private bool upgraded;
        private string? pdfPath;
        private int? pdfPages;
        private string OutputRoot => KitOutput ?? Child(root, "acceptance-result");
        private string Out(string path) => Child(OutputRoot, path);
        private string CurrentSource => plan.Mode == "upgrade" && !upgraded ? plan.OldSourceSha! : plan.ProductSourceSha;
        private string Expand(string? value) => (value ?? "").Replace("{root}", root).Replace("{shortsha}", plan.ProductSourceSha[..7]).Replace("{project}", "安装验收策划");
        private static void Until(Func<bool> test, int timeout)
        { var timer = Stopwatch.StartNew(); do { try { if (test()) return; } catch (ElementNotAvailableException) { } Thread.Sleep(100); } while (timer.ElapsedMilliseconds < timeout); throw new TimeoutException("Condition timed out"); }
        private bool OptionalPresent(Step s) { if (process is { HasExited: true }) return false; try { One(s); return true; } catch (TimeoutException) { return false; } catch (IOException) when (process is { HasExited: true }) { return false; } }
        private bool IsAbsent(Step s) { try { return Find(s).Length == 0; } catch (MissingScopeException) { return true; } }
        private void Install(string path, string hash)
        {
            var safe = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelTart-TestAcceptance")) + Path.DirectorySeparatorChar;
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser }) foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var key = registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{3A5A5B1B-8A11-4E85-9C54-1B18D0E5D2F4}_is1");
                if (key is null) continue;
                var location = key.GetValue("InstallLocation") as string;
                if (string.IsNullOrWhiteSpace(location) || !Path.GetFullPath(location).StartsWith(safe, StringComparison.OrdinalIgnoreCase)) throw new IOException("INSTALLATION_CONFLICT：正式预览版共用安装注册标识；为保护它，请在独立测试机器或虚拟机运行。");
            }
            if (Process.GetProcessesByName("PixelTart").Length != 0) throw new IOException("请正常关闭已有 Pixel Tart；不会强制关闭任何进程。");
            var installerPath = Child(PlanDirectory, path); if (Hash(installerPath) != hash) throw new IOException("Installer hash changed");
            var arguments = new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/NOICONS", "/TASKS=", "/NOCLOSEAPPLICATIONS", "/DIR=" + Child(root, "installed"), "/LOG=" + Out(upgraded ? "upgrade-install.log" : "install.log") };
            using var installer = Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden, Arguments = string.Join(" ", arguments.Select(a => "\"" + a + "\"")) }) ?? throw new IOException("Installer failed to start");
            if (!installer.WaitForExit(180000) || installer.ExitCode != 0) throw new IOException("Installer failed/timed out");
        }
        private AutomationElement[] Roots()
        {
            if (process is null || process.HasExited) throw new IOException("Test PID not running");
            return AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id)).Cast<AutomationElement>().ToArray();
        }
        private AutomationElement[] In(AutomationElement parent, Step s)
        {
            var terms = new List<Condition>(); var id = s.AutomationId ?? s.IdSelector;
            if (id is not null) terms.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, id));
            if (s.Name is not null) terms.Add(new PropertyCondition(AutomationElement.NameProperty, Expand(s.Name)));
            if (s.ControlType is not null) terms.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, typeof(ControlType).GetField(s.ControlType)?.GetValue(null) ?? throw new InvalidDataException("ControlType " + s.ControlType)));
            if (s.HelpText is not null) terms.Add(new PropertyCondition(AutomationElement.HelpTextProperty, s.HelpText));
            var condition = terms.Count switch { 0 => Condition.TrueCondition, 1 => terms[0], _ => new AndCondition(terms.ToArray()) };
            return parent.FindAll(TreeScope.Subtree, condition).Cast<AutomationElement>().Where(e => e.Current.ProcessId == process!.Id && (s.IncludeOffscreen || !e.Current.IsOffscreen) && (s.DescendantName is null || e.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, Expand(s.DescendantName))).Count == 1)).ToArray();
        }
        private AutomationElement[] FindScoped(Step s)
        {
            var requested = s;
            s = ResolvePreset(s);
            var roots = Roots();
            if(s.ScopePreset == "MainWindow") roots = [window ?? throw new InvalidOperationException("Scope resolution failed: MainWindow unavailable")];
            foreach (var scope in s.ScopePath ?? [])
            {
                var candidates = roots.SelectMany(r => In(r, new Step(s.Id, "scope", AutomationId: scope.AutomationId, Name: scope.Name, ControlType: scope.ControlType))).Distinct(AutomationElementIdentity.Instance).ToArray();
                if (candidates.Length != 1) { Diagnose(requested, candidates, "scope-path"); RequireScope(candidates, s.Id); }
                var selected = RequireScope(candidates, s.Id);
                roots = scope.Heading ? [TreeWalker.RawViewWalker.GetParent(selected) ?? throw new InvalidOperationException("Heading parent missing")] : [selected];
            }
            if (s.AncestorAutomationId is not null || s.AncestorName is not null || s.AncestorControlType is not null)
            {
                roots = roots.SelectMany(r => In(r, new Step(s.Id, "scope", AutomationId: s.AncestorAutomationId, Name: s.AncestorName, ControlType: s.AncestorControlType))).Distinct(AutomationElementIdentity.Instance).ToArray();
                if (roots.Length != 1) { Diagnose(requested, roots, "ancestor-scope"); RequireScope(roots, s.Id); }
            }
            if (s.ScopeAnchorName is not null)
            {
                var anchors = roots.SelectMany(r => In(r, new Step(s.Id, "anchor", Name: s.ScopeAnchorName, ControlType: "Text"))).ToArray();
                if (anchors.Length != 1) { Diagnose(requested, anchors, "modal-heading-scope"); RequireScope(anchors, s.Id); }
                for (var parent = TreeWalker.RawViewWalker.GetParent(RequireUnique(anchors, s.Id)); parent is not null && parent.Current.ProcessId == process!.Id; parent = TreeWalker.RawViewWalker.GetParent(parent))
                { var found = In(parent, s); if (found.Length > 0) return found; }
                return [];
            }
            return roots.SelectMany(r => In(r, s)).Distinct(AutomationElementIdentity.Instance).ToArray();
        }
        private void WaitExit(int timeout)
        {
            if (!process!.WaitForExit(timeout)) throw new IOException("Normal exit timed out; confirmation unresolved");
            exitResult = process.ExitCode == 0 ? "PASS" : "FAIL"; if (exitResult != "PASS") throw new IOException("Nonzero natural exit");
            var logs = Child(root, "fresh-data/Logs");
            if (Directory.Exists(logs)) foreach (var file in Directory.EnumerateFiles(logs, "*.log"))
                if (Regex.IsMatch(File.ReadAllText(file), @"(?i)Unhandled Exception|Startup Failure|STARTUP_FAILED|\bFATAL\b")) throw new IOException("Fatal/startup failure in application log");
        }
        private bool ExecuteExtended(Step s)
        {
            switch (s.Action)
            {
                case "fixture":
                    Directory.CreateDirectory(Child(root, "fixtures"));
                    using (var bitmap = new Bitmap(1200, 800)) { using var g = Graphics.FromImage(bitmap); g.Clear(Color.DarkSlateGray); g.FillEllipse(Brushes.Coral, 100, 100, 400, 400); g.FillRectangle(Brushes.Gold, 600, 150, 350, 450); bitmap.Save(Child(root, "fixtures/验收参考.png"), ImageFormat.Png); } return true;
                case "sleep": Thread.Sleep(s.Milliseconds); return true;
                case "upgrade": if (process is { HasExited: false }) throw new IOException("Close before upgrade"); upgraded = true; Install(plan.InstallerPath, plan.InstallerSha256); return true;
                case "requestClose": VerifyOwnedProcess(); ((WindowPattern)window!.GetCurrentPattern(WindowPattern.Pattern)).Close(); return true;
                case "waitExit": WaitExit(s.TimeoutMs); return true;
                case "assertProcessAlive": process!.Refresh(); if (process.HasExited || !process.Responding) throw new IOException("Process not responding"); return true;
                case "assertWindowTitle": if (!window!.Current.Name.Contains(Expand(s.Value), StringComparison.Ordinal)) throw new IOException("Window title mismatch"); return true;
                case "waitPresent": One(s); return true;
                case "waitAbsent": Until(() => IsAbsent(s), s.TimeoutMs); return true;
                case "assertEnabled": if (!One(s).Current.IsEnabled) throw new IOException("Element disabled"); return true;
                case "assertSelected": if (!((SelectionItemPattern)One(s).GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected) throw new IOException("Element not selected"); return true;
                case "assertDate":
                    var dateValue = ((ValuePattern)One(s).GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
                    if (!DateTime.TryParse(dateValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date) || date.Date != DateTime.ParseExact(s.Value!, "yyyy-MM-dd", CultureInfo.InvariantCulture)) throw new IOException("UI date mismatch: " + dateValue); return true;
                case "assertFileExists": case "assertFileHash": case "assertFileSize":
                    var path = Child(root, s.Path!); Until(() => File.Exists(path) && new FileInfo(path).Length >= s.Minimum, s.TimeoutMs);
                    if (s.Action == "assertFileHash" && Hash(path) != s.ExpectedHash) throw new IOException("File hash mismatch"); return true;
                case "assertPdfPages": Pdf(Child(root, s.Path!)); return true;
                case "assertScreenshot": ImageCheck(Out(s.Path!)); return true;
                default: return false;
            }
        }
        private void CaptureExtended(Step s)
        {
            if (s.MustContain is null || s.MustContain.Length == 0)
            { ((WindowPattern)window!.GetCurrentPattern(WindowPattern.Pattern)).SetWindowVisualState(WindowVisualState.Normal); window.SetFocus(); }
            Until(() => { GetWindowThreadProcessId(GetForegroundWindow(), out var pid); return pid == process!.Id; }, s.TimeoutMs);
            var bounds = window!.Current.BoundingRectangle;
            foreach (var top in Roots().Where(e => !e.Current.IsOffscreen)) bounds.Union(top.Current.BoundingRectangle);
            foreach (var name in s.MustContain ?? [])
            {
                var selectors = plan.Steps.Where(x => x.Name == name && x.Action is "assertPresent" or "waitPresent" or "invoke").ToArray();
                var distinct = selectors.Select(x => JsonSerializer.Serialize(x with { Id = "capture-bound", Action = "assertPresent", Coverage = null, Value = null })).Distinct().ToArray();
                var selector = distinct.Length == 1 ? JsonSerializer.Deserialize<Step>(distinct.Single(), Json)! : new Step(s.Id, "assertPresent", Name: name, ControlType: name == "选择拍摄日期" ? "Button" : null);
                bounds.Union(One(selector).Current.BoundingRectangle);
            }
            var rect = new Rectangle((int)Math.Floor(bounds.X), (int)Math.Floor(bounds.Y), (int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
            if (rect.Width <= 0 || rect.Height <= 0 || window.Current.IsOffscreen || !System.Windows.Forms.SystemInformation.VirtualScreen.Contains(rect)) throw new IOException("Clipped/hidden window or popup");
            var path = Out(s.Path ?? s.Value!); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var image = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb)) { using var g = Graphics.FromImage(image); g.CopyFromScreen(rect.Location, Point.Empty, rect.Size); image.Save(path, ImageFormat.Png); }
            ImageCheck(path); screenshots.Add(new { Path = path, SHA256 = Hash(path), rect.Width, rect.Height, VisualReview = "PENDING", OcclusionCheck = "MANUAL_REVIEW_REQUIRED", PopupBoundsChecked = s.MustContain });
        }
        private static void ImageCheck(string path)
        {
            using var bitmap = new Bitmap(path); var min = 255; var max = 0; var alpha = 0;
            for (var y = 0; y < bitmap.Height; y += 16) for (var x = 0; x < bitmap.Width; x += 16)
            { var c = bitmap.GetPixel(x,y); var v = Math.Max(c.R, Math.Max(c.G,c.B)); min = Math.Min(min,v); max = Math.Max(max,v); alpha = Math.Max(alpha,c.A); }
            if (bitmap.Width <= 0 || bitmap.Height <= 0 || max < 8 || max - min < 8 || alpha == 0) throw new IOException("Blank/black/transparent screenshot");
        }
        private string PdfTool(string tool, params string[] args)
        {
            var start = new ProcessStartInfo(Child(PlanDirectory, "poppler/" + tool + ".exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var p = Process.Start(start)!; var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(60000)) throw new TimeoutException("Poppler timeout");
            if (p.ExitCode != 0) throw new IOException(stderr.GetAwaiter().GetResult()); return stdout.GetAwaiter().GetResult();
        }
        private void Pdf(string path)
        {
            Until(() => File.Exists(path) && new FileInfo(path).Length > 0, 30000);
            var info = PdfTool("pdfinfo", path); File.WriteAllText(Out("pdfinfo.txt"), info);
            var match = Regex.Match(info, @"(?m)^Pages:\s+(\d+)"); if (!match.Success || !int.TryParse(match.Groups[1].Value, out var pages) || pages < 1) throw new IOException("No PDF pages");
            var details = PdfTool("pdfinfo", "-f", "1", "-l", pages.ToString(CultureInfo.InvariantCulture), "-box", path); File.WriteAllText(Out("pdf-pages.txt"), details);
            var sizes = Regex.Matches(details, @"(?m)^Page\s+(?:\d+\s+)?size:\s+([\d.]+)\s+x\s+([\d.]+)");
            if (sizes.Count != pages || sizes.Cast<Match>().Any(m => double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture) <= 0 || double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture) <= 0)) throw new IOException("Zero/unknown page dimensions");
            Directory.CreateDirectory(Out("pdf-pages")); PdfTool("pdftoppm", "-scale-to", "1000", "-png", path, Out("pdf-pages/page"));
            var images = Directory.GetFiles(Out("pdf-pages"), "page-*.png"); if (images.Length != pages) throw new IOException("Page render count mismatch");
            foreach (var image in images) ImageCheck(image);
            pdfPath = Out("策划案-安装验收.pdf"); File.Copy(path, pdfPath, false); pdfPages = pages;
        }
    }
}
