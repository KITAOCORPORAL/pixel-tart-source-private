using System.Diagnostics;
using System.Windows.Automation;

namespace PixelTart.NativeAcceptance;

// No attach-by-PID/HWND or arbitrary executable API: only a process started by this host can be targeted.
internal sealed class PixelTartProcessHost : IDisposable
{
    private readonly Process _process;
    private readonly long _startTicks;
    public string ExpectedProcessPath { get; }
    public string RuntimeRoot { get; }
    public int Pid => _process.Id;
    public nint Hwnd { get; private set; }
    private PixelTartProcessHost(Process process, string path, string runtime)
    { _process = process; _startTicks = process.StartTime.ToUniversalTime().Ticks; ExpectedProcessPath = path; RuntimeRoot = runtime; }

    public static async Task<PixelTartProcessHost> StartAsync(string repository, CancellationToken token)
    {
        var root = Path.GetFullPath(repository);
        if (!File.Exists(Path.Combine(root, "RAWSelectionAssistant.sln"))) throw new DirectoryNotFoundException("Pixel Tart repository required.");
        var exe = Path.Combine(root, "src", "RAWSelectionAssistant", "bin", "x64", "Release", "net10.0-windows10.0.19041.0", "win-x64", "KitaoPhotoSelector.exe");
        if (!File.Exists(exe) || !File.Exists(Path.ChangeExtension(exe, ".dll"))) throw new FileNotFoundException("Build Production Release x64 first.", exe);
        var runtime = Path.Combine(root, "artifacts", "native-harness", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtime);
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        start.ArgumentList.Add("--acceptance-color-studio"); start.ArgumentList.Add("--studio-scenario=01"); start.ArgumentList.Add("--native-evidence-observer");
        start.Environment["PIXEL_TART_ISOLATED_RUNTIME"] = "1"; start.Environment["PIXEL_TART_ISOLATED_RUNTIME_ROOT"] = runtime;
        var host = new PixelTartProcessHost(Process.Start(start) ?? throw new InvalidOperationException("Production process did not start."), exe, runtime);
        try
        {
            host.Hwnd = await NativeWait.UntilAsync(_ =>
            {
                if (host._process.HasExited) throw new InvalidOperationException("Product exited during startup.");
                host._process.Refresh(); return Task.FromResult(host._process.MainWindowHandle);
            }, hwnd => hwnd != 0, TimeSpan.FromSeconds(45), token);
            return host;
        }
        catch { host.Dispose(); throw; }
    }
    public TargetIdentity Validate(ScreenPoint? point = null)
    {
        _process.Refresh();
        Win32.GetWindowThreadProcessId(Hwnd, out var windowPid);
        if (!Win32.GetWindowRect(Hwnd, out var bounds)) throw new InvalidOperationException("Cannot read target window bounds.");
        var snapshot = new TargetIdentity(Pid, Hwnd.ToInt64(), ExpectedProcessPath, _process.MainModule?.FileName ?? "",
            checked((int)windowPid), _process.StartTime.ToUniversalTime().Ticks, _startTicks, !_process.HasExited && Win32.IsWindow(Hwnd),
            Win32.GetForegroundWindow() == Hwnd, Win32.GetDpiForWindow(Hwnd), bounds.Bounds);
        TargetGuard.Validate(snapshot, point);
        if (point is { } p)
        {
            var hit = Win32.WindowFromPoint(new(p.X, p.Y));
            Win32.GetWindowThreadProcessId(hit, out var hitPid);
            if (hitPid != Pid || Win32.GetAncestor(hit, 2) != Hwnd) throw new InvalidOperationException("Pointer is occluded or outside owned Pixel Tart HWND.");
        }
        return snapshot;
    }
    public void Dispose()
    {
        if (!_process.HasExited && _process.StartTime.ToUniversalTime().Ticks == _startTicks &&
            string.Equals(_process.MainModule?.FileName, ExpectedProcessPath, StringComparison.OrdinalIgnoreCase))
            _process.Kill(); // only the process this host created, never a supplied PID or another application
        _process.Dispose();
    }
}

internal sealed class PixelTartWindowLocator(PixelTartProcessHost host)
{
    // UIA only locates. No InvokePattern/SelectionPattern or desktop RootElement traversal.
    public ScreenBounds Find(string automationId)
    {
        using var dpi = new PhysicalDpiScope();
        host.Validate();
        var root = AutomationElement.FromHandle(host.Hwnd);
        if (root.Current.ProcessId != host.Pid) throw new InvalidOperationException("UIA root PID differs from owned process.");
        var matches = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId)).Cast<AutomationElement>().ToArray();
        if (matches.Any(e => e.Current.ProcessId != host.Pid)) throw new InvalidOperationException("UIA element PID differs from owned process.");
        var found = matches.Where(e => !e.Current.IsOffscreen).ToArray();
        if (found.Length != 1) throw new InvalidOperationException($"Expected one visible target {automationId}, got {found.Length}.");
        var rect = found[0].Current.BoundingRectangle;
        var bounds = new ScreenBounds(rect.X, rect.Y, rect.Width, rect.Height);
        host.Validate(bounds.Point(.5, .5)); return bounds;
    }
}
