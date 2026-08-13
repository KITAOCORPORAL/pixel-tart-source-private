using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace PixelTart.UiAutomationValidation;

internal sealed class TargetWindow
{
    private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "iexplore", "vivaldi", "arc"
    };

    private TargetWindow(int processId, nint handle, string executableName, string windowClass)
    {
        ProcessId = processId;
        Handle = handle;
        ExecutableName = executableName;
        WindowClass = windowClass;
    }

    public int ProcessId { get; }
    public nint Handle { get; }
    public string ExecutableName { get; }
    public string WindowClass { get; }

    public static TargetWindow? WaitFor(int processId, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            using var process = GetProcess(processId);
            if (process is null || process.HasExited)
            {
                return null;
            }

            process.Refresh();
            var handle = process.MainWindowHandle;
            if (handle == nint.Zero || !NativeMethods.IsWindowVisible(handle))
            {
                handle = NativeMethods.FindBestTopLevelWindow(processId);
            }

            if (handle != nint.Zero)
            {
                var executableName = Safe.Text(process.ProcessName) + ".exe";
                return new TargetWindow(processId, handle, executableName, NativeMethods.WindowClass(handle));
            }

            Thread.Sleep(75);
        }
        while (stopwatch.Elapsed < timeout);

        return null;
    }

    public void EnsureAllowed()
    {
        var processName = Path.GetFileNameWithoutExtension(ExecutableName);
        if (BrowserProcessNames.Contains(processName) ||
            WindowClass.StartsWith("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) ||
            WindowClass.StartsWith("MozillaWindowClass", StringComparison.OrdinalIgnoreCase) ||
            WindowClass.StartsWith("IEFrame", StringComparison.OrdinalIgnoreCase))
        {
            throw new TargetRejectedException("Browser targets are not permitted.");
        }

        if (!processName.EndsWith(".Acceptance", StringComparison.OrdinalIgnoreCase))
        {
            throw new TargetRejectedException("Only a process whose name ends in '.Acceptance' may be inspected or controlled.");
        }
    }

    public object Summary()
    {
        var element = AutomationElement.FromHandle(Handle);
        var current = element.Current;
        return new
        {
            process_id = ProcessId,
            executable = ExecutableName,
            window_handle = $"0x{Handle.ToInt64():X}",
            window_class = Safe.Text(WindowClass),
            is_visible = NativeMethods.IsWindowVisible(Handle),
            is_enabled = current.IsEnabled,
            is_offscreen = current.IsOffscreen,
            bounding_rectangle = RectangleSnapshot.From(current.BoundingRectangle),
            control_type = current.ControlType?.ProgrammaticName.Replace("ControlType.", string.Empty, StringComparison.Ordinal) ?? "Unknown"
        };
    }

    private static Process? GetProcess(int processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

internal static class TargetSummary
{
    public static object ForProcess(int processId, string executableName) => new
    {
        process_id = processId,
        executable = Safe.Text(executableName),
        window_handle = string.Empty,
        window_class = string.Empty
    };
}

internal sealed record UiaMatch(bool Success, AutomationElement? Element, int MatchCount, object? Error)
{
    public static UiaMatch Found(AutomationElement element) => new(true, element, 1, null);

    public static UiaMatch NotFound() => new(false, null, 0,
        new { code = "control_not_found", message = "No exact UI Automation match was found before the timeout." });

    public static UiaMatch Ambiguous(int count) => new(false, null, count,
        new { code = "ambiguous_selector", message = "The selector matched more than one control." });
}

internal static class UiaQuery
{
    public static UiaMatch FindSingle(
        TargetWindow target,
        SelectorKind selectorKind,
        string selector,
        TimeSpan timeout)
    {
        var root = AutomationElement.FromHandle(target.Handle);
        var property = selectorKind == SelectorKind.AutomationId
            ? AutomationElement.AutomationIdProperty
            : AutomationElement.NameProperty;
        var condition = new PropertyCondition(property, selector, PropertyConditionFlags.None);
        var stopwatch = Stopwatch.StartNew();

        do
        {
            var matches = root.FindAll(TreeScope.Element | TreeScope.Descendants, condition);
            if (matches.Count == 1)
            {
                return UiaMatch.Found(matches[0]);
            }
            if (matches.Count > 1)
            {
                return UiaMatch.Ambiguous(matches.Count);
            }
            Thread.Sleep(75);
        }
        while (stopwatch.Elapsed < timeout);

        return UiaMatch.NotFound();
    }

    public static IReadOnlyList<AutomationElement> VisibleButtons(TargetWindow target)
    {
        var root = AutomationElement.FromHandle(target.Handle);
        var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
        var matches = root.FindAll(TreeScope.Descendants, condition);
        var visible = new List<AutomationElement>(matches.Count);
        foreach (AutomationElement element in matches)
        {
            try
            {
                var rectangle = element.Current.BoundingRectangle;
                if (!element.Current.IsOffscreen && !rectangle.IsEmpty && rectangle.Width > 0 && rectangle.Height > 0)
                {
                    visible.Add(element);
                }
            }
            catch (ElementNotAvailableException)
            {
            }
        }
        return visible;
    }
}

internal sealed record RectangleSnapshot(double X, double Y, double Width, double Height)
{
    public static RectangleSnapshot From(Rect rectangle) => new(
        Round(rectangle.X),
        Round(rectangle.Y),
        Round(rectangle.Width),
        Round(rectangle.Height));

    private static double Round(double value) => double.IsFinite(value) ? Math.Round(value, 2) : 0;
}

internal sealed record UiaSnapshot(
    string AutomationId,
    string Name,
    RectangleSnapshot BoundingRectangle,
    bool IsEnabled,
    bool IsOffscreen,
    string ControlType,
    bool InvokeAvailable)
{
    public static UiaSnapshot From(AutomationElement element)
    {
        var current = element.Current;
        return new UiaSnapshot(
            Safe.Text(current.AutomationId),
            Safe.Text(current.Name),
            RectangleSnapshot.From(current.BoundingRectangle),
            current.IsEnabled,
            current.IsOffscreen,
            current.ControlType?.ProgrammaticName.Replace("ControlType.", string.Empty, StringComparison.Ordinal) ?? "Unknown",
            element.TryGetCurrentPattern(InvokePattern.Pattern, out _));
    }
}

internal sealed record ScreenPointSnapshot(double X, double Y, bool IsValid)
{
    public static ScreenPointSnapshot From(Rect rectangle)
    {
        var x = rectangle.X + (rectangle.Width / 2d);
        var y = rectangle.Y + (rectangle.Height / 2d);
        var valid = !rectangle.IsEmpty && rectangle.Width > 0 && rectangle.Height > 0 &&
                    double.IsFinite(x) && double.IsFinite(y);
        return new ScreenPointSnapshot(Math.Round(valid ? x : 0, 2), Math.Round(valid ? y : 0, 2), valid);
    }
}

internal sealed record UiaClickSnapshot(
    string AutomationId,
    RectangleSnapshot BoundingRectangle,
    bool IsEnabled,
    bool IsOffscreen,
    string ControlType,
    bool InvokeAvailable,
    double CenterX,
    double CenterY,
    bool CenterValid)
{
    public static UiaClickSnapshot From(AutomationElement element)
    {
        var current = element.Current;
        var center = ScreenPointSnapshot.From(current.BoundingRectangle);
        return new UiaClickSnapshot(
            Safe.Text(current.AutomationId),
            RectangleSnapshot.From(current.BoundingRectangle),
            current.IsEnabled,
            current.IsOffscreen,
            current.ControlType?.ProgrammaticName.Replace("ControlType.", string.Empty, StringComparison.Ordinal) ?? "Unknown",
            element.TryGetCurrentPattern(InvokePattern.Pattern, out _),
            center.X,
            center.Y,
            center.IsValid);
    }

    public ScreenPointSnapshot Center() => new(CenterX, CenterY, CenterValid);
}

internal sealed record UiaPointSnapshot(
    string AutomationId,
    string ControlType,
    bool IsEnabled,
    bool IsOffscreen,
    RectangleSnapshot BoundingRectangle,
    bool BelongsToTargetProcess)
{
    public static UiaPointSnapshot From(AutomationElement element, int targetProcessId)
    {
        var current = element.Current;
        return new UiaPointSnapshot(
            Safe.Text(current.AutomationId),
            current.ControlType?.ProgrammaticName.Replace("ControlType.", string.Empty, StringComparison.Ordinal) ?? "Unknown",
            current.IsEnabled,
            current.IsOffscreen,
            RectangleSnapshot.From(current.BoundingRectangle),
            current.ProcessId == targetProcessId);
    }
}

internal static class WindowsInput
{
    private const ushort VirtualKeyEscape = 0x1B;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventVirtualDesk = 0x4000;
    private const uint MouseEventAbsolute = 0x8000;
    private const int ShowRestore = 9;

    public static InputDispatchResult SendEscape(TargetWindow target)
    {
        target.EnsureAllowed();
        NativeMethods.ShowWindow(target.Handle, ShowRestore);
        NativeMethods.BringWindowToTop(target.Handle);
        NativeMethods.SetForegroundWindow(target.Handle);

        var stopwatch = Stopwatch.StartNew();
        while (NativeMethods.GetForegroundWindow() != target.Handle && stopwatch.ElapsedMilliseconds < 1500)
        {
            Thread.Sleep(25);
            NativeMethods.SetForegroundWindow(target.Handle);
        }

        if (NativeMethods.GetForegroundWindow() != target.Handle)
        {
            return InputDispatchResult.Failed(
                "target_foreground_failed",
                "Escape was not sent because the requested target window could not be confirmed as foreground.");
        }

        var inputs = new[]
        {
            NativeMethods.INPUT.Keyboard(VirtualKeyEscape, 0),
            NativeMethods.INPUT.Keyboard(VirtualKeyEscape, KeyEventKeyUp)
        };
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            return InputDispatchResult.Failed("send_input_failed", "Windows did not accept the complete Escape key input sequence.");
        }

        return InputDispatchResult.Succeeded();
    }

    public static MouseClickDispatchResult SendMouseClick(TargetWindow target, ScreenPointSnapshot center)
    {
        target.EnsureAllowed();
        var foreground = EnsureForeground(target);
        if (!foreground.Success)
        {
            return MouseClickDispatchResult.Failed(foreground.ErrorCode, foreground.ErrorMessage, null);
        }

        var desktop = NativeMethods.VirtualDesktop();
        if (!center.IsValid || !desktop.Contains(center.X, center.Y))
        {
            return MouseClickDispatchResult.Failed(
                "center_outside_virtual_desktop",
                "The selected control center is outside the Windows virtual desktop.",
                null);
        }

        UiaPointSnapshot elementFromPoint;
        try
        {
            var topElement = AutomationElement.FromPoint(new Point(center.X, center.Y));
            elementFromPoint = UiaPointSnapshot.From(topElement, target.ProcessId);
        }
        catch (ElementNotAvailableException)
        {
            return MouseClickDispatchResult.Failed(
                "element_from_point_unavailable",
                "The topmost UI Automation element became unavailable before input was sent.",
                null);
        }

        if (!elementFromPoint.BelongsToTargetProcess)
        {
            return MouseClickDispatchResult.Failed(
                "point_not_owned_by_target",
                "Mouse input was not sent because the topmost UI Automation element does not belong to the target process.",
                elementFromPoint);
        }

        var absoluteX = desktop.NormalizeX(center.X);
        var absoluteY = desktop.NormalizeY(center.Y);
        var inputs = new[]
        {
            NativeMethods.INPUT.Mouse(absoluteX, absoluteY, MouseEventMove | MouseEventVirtualDesk | MouseEventAbsolute),
            NativeMethods.INPUT.Mouse(0, 0, MouseEventLeftDown),
            NativeMethods.INPUT.Mouse(0, 0, MouseEventLeftUp)
        };
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            return MouseClickDispatchResult.Failed(
                "send_input_failed",
                "Windows did not accept the complete mouse click input sequence.",
                elementFromPoint);
        }

        return MouseClickDispatchResult.Succeeded(elementFromPoint);
    }

    private static InputDispatchResult EnsureForeground(TargetWindow target)
    {
        NativeMethods.ShowWindow(target.Handle, ShowRestore);
        NativeMethods.BringWindowToTop(target.Handle);
        NativeMethods.SetForegroundWindow(target.Handle);

        var stopwatch = Stopwatch.StartNew();
        while (NativeMethods.GetForegroundWindow() != target.Handle && stopwatch.ElapsedMilliseconds < 1500)
        {
            Thread.Sleep(25);
            NativeMethods.SetForegroundWindow(target.Handle);
        }

        return NativeMethods.GetForegroundWindow() == target.Handle
            ? InputDispatchResult.Succeeded()
            : InputDispatchResult.Failed(
                "target_foreground_failed",
                "Input was not sent because the requested target window could not be confirmed as foreground.");
    }
}

internal sealed record InputDispatchResult(bool Success, string ErrorCode, string ErrorMessage)
{
    public static InputDispatchResult Succeeded() => new(true, string.Empty, string.Empty);
    public static InputDispatchResult Failed(string code, string message) => new(false, code, message);
}

internal sealed record MouseClickDispatchResult(
    bool Success,
    string ErrorCode,
    string ErrorMessage,
    UiaPointSnapshot? ElementFromPoint)
{
    public static MouseClickDispatchResult Succeeded(UiaPointSnapshot element) =>
        new(true, string.Empty, string.Empty, element);

    public static MouseClickDispatchResult Failed(string code, string message, UiaPointSnapshot? element) =>
        new(false, code, message, element);
}

internal sealed record PostInputObservation(
    bool TargetDisappeared,
    bool TargetCheckTimedOut,
    bool WindowStillExists,
    bool WindowResponsive);

internal static class PostInputProbe
{
    public static PostInputObservation Observe(TargetWindow target, string automationId, TimeSpan timeout)
    {
        var task = Task.Run(() =>
        {
            try
            {
                if (!NativeMethods.IsWindow(target.Handle))
                {
                    return true;
                }

                var root = AutomationElement.FromHandle(target.Handle);
                var condition = new PropertyCondition(
                    AutomationElement.AutomationIdProperty,
                    automationId,
                    PropertyConditionFlags.None);
                return root.FindFirst(TreeScope.Element | TreeScope.Descendants, condition) is null;
            }
            catch (ElementNotAvailableException)
            {
                return true;
            }
        });

        var completed = task.Wait(timeout);
        var windowStillExists = NativeMethods.IsWindow(target.Handle);
        var responsive = windowStillExists && NativeMethods.IsWindowResponsive(target.Handle, 350);
        return new PostInputObservation(
            completed ? task.Result : !windowStillExists,
            !completed,
            windowStillExists,
            responsive);
    }
}

internal sealed record VirtualDesktopBounds(int X, int Y, int Width, int Height)
{
    public bool Contains(double pointX, double pointY) =>
        Width > 1 && Height > 1 && pointX >= X && pointX < X + Width && pointY >= Y && pointY < Y + Height;

    public int NormalizeX(double pointX) => Normalize(pointX, X, Width);
    public int NormalizeY(double pointY) => Normalize(pointY, Y, Height);

    private static int Normalize(double value, int origin, int length) =>
        (int)Math.Clamp(Math.Round((value - origin) * 65535d / (length - 1d)), 0, 65535);
}

internal static class NativeMethods
{
    private const uint GetWindowOwner = 4;
    private const uint WmNull = 0x0000;
    private const uint SendMessageTimeoutAbortIfHung = 0x0002;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    internal delegate bool EnumWindowsProc(nint windowHandle, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint windowHandle);

    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint windowHandle, uint command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint windowHandle, out RECT rectangle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint windowHandle, char[] className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BringWindowToTop(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeoutMilliseconds,
        out nint result);

    internal static VirtualDesktopBounds VirtualDesktop() => new(
        GetSystemMetrics(SmXVirtualScreen),
        GetSystemMetrics(SmYVirtualScreen),
        GetSystemMetrics(SmCxVirtualScreen),
        GetSystemMetrics(SmCyVirtualScreen));

    internal static bool IsWindowResponsive(nint windowHandle, uint timeoutMilliseconds) =>
        IsWindow(windowHandle) &&
        SendMessageTimeout(
            windowHandle,
            WmNull,
            nint.Zero,
            nint.Zero,
            SendMessageTimeoutAbortIfHung,
            timeoutMilliseconds,
            out _) != nint.Zero;

    internal static nint FindBestTopLevelWindow(int processId)
    {
        nint best = nint.Zero;
        long bestArea = -1;
        EnumWindows((windowHandle, _) =>
        {
            GetWindowThreadProcessId(windowHandle, out var ownerProcessId);
            if (ownerProcessId != processId || !IsWindowVisible(windowHandle) || GetWindow(windowHandle, GetWindowOwner) != nint.Zero)
            {
                return true;
            }

            if (!GetWindowRect(windowHandle, out var rectangle))
            {
                return true;
            }

            var area = Math.Max(0L, rectangle.Right - rectangle.Left) * Math.Max(0L, rectangle.Bottom - rectangle.Top);
            if (area > bestArea)
            {
                best = windowHandle;
                bestArea = area;
            }
            return true;
        }, nint.Zero);
        return best;
    }

    internal static string WindowClass(nint windowHandle)
    {
        var buffer = new char[256];
        var length = GetClassName(windowHandle, buffer, buffer.Length);
        return length <= 0 ? string.Empty : new string(buffer, 0, length);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        internal uint Type;
        internal InputUnion Data;

        internal static INPUT Keyboard(ushort virtualKey, uint flags) => new()
        {
            Type = 1,
            Data = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    VirtualKey = virtualKey,
                    ScanCode = 0,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = nint.Zero
                }
            }
        };

        internal static INPUT Mouse(int x, int y, uint flags) => new()
        {
            Type = 0,
            Data = new InputUnion
            {
                Mouse = new MOUSEINPUT
                {
                    X = x,
                    Y = y,
                    MouseData = 0,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = nint.Zero
                }
            }
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        internal MOUSEINPUT Mouse;

        [FieldOffset(0)]
        internal KEYBDINPUT Keyboard;

        [FieldOffset(0)]
        internal HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        internal uint Message;
        internal ushort ParameterLow;
        internal ushort ParameterHigh;
    }
}
