using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tethering;

namespace RAWSelectionAssistant.Services;

public sealed class WindowsDisplayTopologyService : IDisplayTopologyService, IDisposable
{
    private IReadOnlyList<MonitorDisplayInfo> _last = [];
    private readonly System.Windows.Threading.DispatcherTimer? _timer;
    public WindowsDisplayTopologyService(bool watchChanges = true)
    {
        _last = Enumerate();
        if (!watchChanges || System.Windows.Application.Current is null) return;
        _timer = new() { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += CheckChanged;
        _timer.Start();
    }
    public event EventHandler? TopologyChanged;
    public IReadOnlyList<MonitorDisplayInfo> GetDisplays() => Enumerate();
    public MonitorDisplayInfo? FindByStableKey(string stableKey) => GetDisplays().FirstOrDefault(item => string.Equals(item.StableKey, stableKey, StringComparison.Ordinal));
    public void Dispose() => _timer?.Stop();
    private void CheckChanged(object? sender, EventArgs e) { var current = Enumerate(); var before = string.Join('|', _last.Select(Key)); var after = string.Join('|', current.Select(Key)); if (before == after) return; _last = current; TopologyChanged?.Invoke(this, EventArgs.Empty); }
    private static string Key(MonitorDisplayInfo item) => $"{item.StableKey}:{item.Left}:{item.Top}:{item.Width}:{item.Height}:{item.DpiX}:{item.DpiY}";
    private static IReadOnlyList<MonitorDisplayInfo> Enumerate()
    {
        var result = new List<MonitorDisplayInfo>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new Native.MONITORINFOEX { cbSize = Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (!Native.GetMonitorInfo(monitor, ref info)) return true;
            var device = new Native.DISPLAY_DEVICE { cb = Marshal.SizeOf<Native.DISPLAY_DEVICE>() };
            Native.EnumDisplayDevices(info.szDevice, 0, ref device, 0);
            var dpiX = 96u; var dpiY = 96u;
            try { _ = Native.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY); } catch (DllNotFoundException) { }
            var bounds = info.rcMonitor;
            var friendly = string.IsNullOrWhiteSpace(device.DeviceString) ? info.szDevice : device.DeviceString;
            var id = device.DeviceID ?? string.Empty;
            result.Add(new(DisplayStableKey.Create(info.szDevice, id), friendly, info.szDevice, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, (info.dwFlags & 1) != 0, dpiX, dpiY, id));
            return true;
        }, IntPtr.Zero);
        return result.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.Left).ThenBy(item => item.Top).ToArray();
    }

    private static class Native
    {
        internal delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MONITORINFOEX { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct DISPLAY_DEVICE { public int cb; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString; public uint StateFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey; }
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumDisplayDevices(string device, uint number, ref DISPLAY_DEVICE displayDevice, uint flags);
        [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
    }
}

public sealed class WindowsDisplayProfileService : IDisplayProfileService
{
    public Task<DisplayColorProfile> GetProfileAsync(MonitorDisplayInfo display, CancellationToken cancellationToken = default) => Task.Run(() => Resolve(display, cancellationToken), cancellationToken);
    private static DisplayColorProfile Resolve(MonitorDisplayInfo display, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IntPtr dc = IntPtr.Zero;
        try
        {
            dc = Native.CreateDC("DISPLAY", display.DeviceName, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) return Fallback(display, DisplayProfileStatus.Unsupported);
            uint length = 0;
            _ = Native.GetICMProfile(dc, ref length, null);
            if (length == 0) return Fallback(display, DisplayProfileStatus.NotConfigured);
            var buffer = new char[length];
            if (!Native.GetICMProfile(dc, ref length, buffer)) return Fallback(display, DisplayProfileStatus.NotConfigured);
            var path = new string(buffer).TrimEnd('\0');
            if (!File.Exists(path)) return Fallback(display, DisplayProfileStatus.Missing, path);
            using var stream = File.OpenRead(path);
            var fingerprint = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            var hint = ProfileHint(Path.GetFileNameWithoutExtension(path));
            return new(display.StableKey, display.FriendlyName, display.DeviceName, path, Path.GetFileName(path), DisplayProfileStatus.Detected, true, DateTimeOffset.UtcNow, hint, fingerprint);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or NotSupportedException) { return Fallback(display, DisplayProfileStatus.Corrupt); }
        finally { if (dc != IntPtr.Zero) Native.DeleteDC(dc); }
    }
    private static DisplayColorProfile Fallback(MonitorDisplayInfo display, DisplayProfileStatus reason, string? path = null) => new(display.StableKey, display.FriendlyName, display.DeviceName, path, "sRGB（安全回退）", reason == DisplayProfileStatus.Unsupported ? reason : DisplayProfileStatus.FallbackSrgb, false, DateTimeOffset.UtcNow, "sRGB", null);
    private static string ProfileHint(string name) { if (name.Contains("P3", StringComparison.OrdinalIgnoreCase)) return "Display P3"; if (name.Contains("Adobe", StringComparison.OrdinalIgnoreCase)) return "Adobe RGB"; if (name.Contains("sRGB", StringComparison.OrdinalIgnoreCase)) return "sRGB"; return "自定义ICC"; }
    private static class Native
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr CreateDC(string driver, string device, string? output, IntPtr initData);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetICMProfile(IntPtr dc, ref uint size, [Out] char[]? filename);
    }
}
