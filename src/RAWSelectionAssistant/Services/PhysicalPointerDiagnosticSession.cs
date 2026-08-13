#if INPUT_ROUTING_DIAGNOSTICS
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

internal sealed record PointerDiagnosticContext(
    string CurrentSurface,
    string CurrentOverlay,
    bool TutorialActive,
    int? CurrentTutorialStep);

internal static class PhysicalPointerDiagnosticSession
{
    private const string Protocol = "pixel-tart-physical-pointer/v1";
    private const int MaximumAttempts = 64;
    private static readonly object Gate = new();
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private static PhysicalPointerSessionDocument? _document;
    private static PhysicalPointerAttempt? _activeAttempt;
    private static DateTimeOffset _lastMouseMoveWrite;

    public static bool IsEnabled
    {
        get
        {
            var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
            return processName.EndsWith(".Acceptance", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string DiagnosticId
    {
        get
        {
            lock (Gate)
            {
                EnsureSession();
                return _document?.DiagnosticId ?? string.Empty;
            }
        }
    }

    public static string DiagnosticFileName => "physical-pointer-session.json";

    public static void Begin(PointerDiagnosticContext context)
    {
        if (!IsEnabled) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            Save();
        }
    }

    public static void Complete(PointerDiagnosticContext context)
    {
        if (!IsEnabled) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            if (_document is not null) _document.CompletedAt = DateTimeOffset.Now;
            Save();
        }
    }

    public static void RecordWin32Mouse(
        string message,
        Point windowPosition,
        Point screenPosition,
        PointerDiagnosticContext context)
    {
        if (!IsEnabled) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            var now = DateTimeOffset.Now;
            if (string.Equals(message, "WM_MOUSEMOVE", StringComparison.Ordinal))
            {
                if (_document is null) return;
                _document.NativeMouseMoveCount++;
                _document.LastNativeMouseMove = PointerPosition.From(now, windowPosition, screenPosition);
                if (now - _lastMouseMoveWrite < TimeSpan.FromSeconds(1)) return;
                _lastMouseMoveWrite = now;
                Save();
                return;
            }

            if (string.Equals(message, "WM_LBUTTONDOWN", StringComparison.Ordinal))
            {
                _activeAttempt = CreateAttempt(now, windowPosition, screenPosition, context);
                if (_document is not null)
                {
                    if (_document.Attempts.Count >= MaximumAttempts) _document.Attempts.RemoveAt(0);
                    _document.Attempts.Add(_activeAttempt);
                }
            }
            else if (string.Equals(message, "WM_LBUTTONUP", StringComparison.Ordinal) && _activeAttempt is not null)
            {
                _activeAttempt.Layer1Win32.LButtonUpReceived = true;
                _activeAttempt.Layer1Win32.Up = PointerPosition.From(now, windowPosition, screenPosition);
                _activeAttempt.Layer1Win32.UpAt = now;
                _activeAttempt.UpdatedAt = now;
            }

            Save();
        }
    }

    public static void RecordWpfMouse(
        UIElement root,
        MouseButtonEventArgs args,
        string eventName,
        PointerDiagnosticContext context)
    {
        if (!IsEnabled) return;
        if (args.ChangedButton != MouseButton.Left) return;
        Point windowPosition;
        Point screenPosition;
        DependencyObject? inputHit;
        DependencyObject? visualHit;
        try
        {
            windowPosition = args.GetPosition(root);
            screenPosition = root.PointToScreen(windowPosition);
            inputHit = root.InputHitTest(windowPosition) as DependencyObject;
            visualHit = VisualTreeHelper.HitTest(root, windowPosition)?.VisualHit;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            var attempt = GetOrCreateWpfAttempt(now: DateTimeOffset.Now, windowPosition, screenPosition, context);

            var now = DateTimeOffset.Now;
            attempt.Layer2Wpf.Events.Add(new WpfPointerEvent
            {
                Timestamp = now,
                EventName = SafeToken(eventName),
                WindowPosition = DiagnosticPoint.From(windowPosition),
                ScreenPosition = DiagnosticPoint.From(screenPosition),
                OriginalSource = Describe(args.OriginalSource as DependencyObject),
                Source = Describe(args.Source as DependencyObject),
                Handled = args.Handled
            });
            if (eventName.Contains("Down", StringComparison.Ordinal))
                attempt.Layer2Wpf.PreviewMouseDownReceived = true;
            if (eventName.Contains("Up", StringComparison.Ordinal))
                attempt.Layer2Wpf.PreviewMouseUpReceived = true;

            attempt.Layer3Target.InputHitTest = Describe(inputHit);
            attempt.Layer3Target.VisualHitTest = Describe(visualHit);
            attempt.Layer3Target.VisualParentChain = ParentChain(inputHit);
            attempt.Layer3Target.BlockingAncestor = FirstBlockingAncestor(inputHit);
            if (eventName.Contains("LeftButtonDown", StringComparison.Ordinal) && args.ChangedButton == MouseButton.Left)
            {
                attempt.LastWpfDownAt = now;
                attempt.LastWpfDownTarget = Describe(args.OriginalSource as DependencyObject);
                attempt.LastWpfDownParentChain = ParentChain(args.OriginalSource as DependencyObject);
            }
            attempt.UpdatedAt = now;
            Save();
        }
    }

    public static void RecordButtonClick(DependencyObject? button, bool handled, PointerDiagnosticContext context)
    {
        if (!IsEnabled) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            if (!CanCorrelateWithActiveAttempt(requireWpfDown: true) || !TryConfirmPhysicalTarget(button)) return;
            var attempt = _activeAttempt!;
            if (!IsCloseLike(button)) return;
            attempt.Layer4Action.ButtonClickReceived = true;
            attempt.Layer4Action.PhysicalTargetConfirmed = true;
            attempt.Layer4Action.Button = Describe(button);
            attempt.Layer4Action.Events.Add(new ActionEvent
            {
                Timestamp = DateTimeOffset.Now,
                EventName = "ButtonClick",
                Handled = handled
            });
            attempt.UpdatedAt = DateTimeOffset.Now;
            Save();
        }
    }

    public static void CompleteWpfMouseDispatch(MouseButton button)
    {
        if (!IsEnabled || button != MouseButton.Left) return;
        lock (Gate)
        {
            if (_activeAttempt is null) return;
            _activeAttempt.UpdatedAt = DateTimeOffset.Now;
            Save();
            _activeAttempt = null;
        }
    }

    public static void RecordControlEvent(
        DependencyObject control,
        string eventName,
        object? originalSource,
        object? source,
        bool handled)
    {
        if (!IsEnabled) return;
        lock (Gate)
        {
            EnsureSession();
            if (!CanCorrelateWithActiveAttempt(requireWpfDown: true)) return;
            var attempt = _activeAttempt!;
            if (string.Equals(eventName, "CloseClick", StringComparison.Ordinal))
            {
                var targetSource = originalSource as DependencyObject ?? source as DependencyObject ?? control;
                if (!TryConfirmPhysicalTarget(targetSource) && !TryConfirmPhysicalTarget(control)) return;
                if (!IsCloseLike(control) && !IsCloseLike(targetSource)) return;
                attempt.Layer4Action.ButtonClickReceived = true;
                attempt.Layer4Action.PhysicalTargetConfirmed = true;
                attempt.Layer4Action.ActionFinalized = true;
            }
            attempt.Layer4Action.Events.Add(new ActionEvent
            {
                Timestamp = DateTimeOffset.Now,
                EventName = SafeToken(eventName),
                Control = Describe(control),
                OriginalSource = Describe(originalSource as DependencyObject),
                Source = Describe(source as DependencyObject),
                Handled = handled
            });
            attempt.UpdatedAt = DateTimeOffset.Now;
            Save();
        }
    }

    public static void RecordShellEvent(string eventName, PointerDiagnosticContext context)
    {
        if (!IsEnabled) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            if (!CanCorrelateWithActiveAttempt(requireWpfDown: true) || !_activeAttempt!.Layer4Action.PhysicalTargetConfirmed)
            {
                if (_document is not null)
                {
                    _document.UncorrelatedActionEvents.Add(new ActionEvent
                    {
                        Timestamp = DateTimeOffset.Now,
                        EventName = SafeToken(eventName)
                    });
                    if (_document.UncorrelatedActionEvents.Count > 64) _document.UncorrelatedActionEvents.RemoveAt(0);
                    Save();
                }
                return;
            }
            var attempt = _activeAttempt!;
            var safeEventName = SafeToken(eventName);
            attempt.Layer4Action.Events.Add(new ActionEvent
            {
                Timestamp = DateTimeOffset.Now,
                EventName = safeEventName
            });
            if (safeEventName is "ForceExitTutorialEntered" or "ForceCloseCurrentSurfaceEntered")
                attempt.Layer4Action.ShellEscapeEntered = true;
            if (safeEventName == "TutorialOverlayDetached")
                attempt.Layer4Action.TutorialOverlayDetached = true;
            if (safeEventName == "SurfaceCloseDispatchCompleted")
                attempt.Layer4Action.SurfaceCloseDispatchCompleted = true;
            if (safeEventName == "SurfaceClosed")
                attempt.Layer4Action.SurfaceClosed = true;
            attempt.UpdatedAt = DateTimeOffset.Now;
            Save();
        }
    }

    public static void RecordCloseAuthority(
        string surface,
        IReadOnlyList<string> visibleAutomationIds,
        PointerDiagnosticContext context)
    {
        if (!IsEnabled) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            if (_document is null) return;
            var safeIds = visibleAutomationIds.Select(SafeToken).Where(value => value.Length > 0).ToArray();
            var signature = $"{SafeToken(surface)}:{string.Join(',', safeIds)}";
            if (string.Equals(_document.LastCloseAuthoritySignature, signature, StringComparison.Ordinal)) return;
            _document.LastCloseAuthoritySignature = signature;
            _document.CloseAuthorityChecks.Add(new CloseAuthorityCheck
            {
                Timestamp = DateTimeOffset.Now,
                Surface = SafeToken(surface),
                VisibleCloseButtonCount = safeIds.Length,
                VisibleAutomationIds = safeIds,
                Result = safeIds.Length > 1 ? "DuplicateCloseAuthority" : "SingleCloseAuthority"
            });
            if (_document.CloseAuthorityChecks.Count > 64) _document.CloseAuthorityChecks.RemoveAt(0);
            Save();
        }
    }

    private static PhysicalPointerAttempt CreateAttempt(
        DateTimeOffset now,
        Point windowPosition,
        Point screenPosition,
        PointerDiagnosticContext context,
        bool win32DownReceived = true) => new()
    {
        AttemptId = $"pointer-{NextAttemptNumber():D3}",
        StartedAt = now,
        UpdatedAt = now,
        CurrentSurface = SafeToken(context.CurrentSurface),
        CurrentOverlay = SafeToken(context.CurrentOverlay),
        TutorialActive = context.TutorialActive,
        CurrentTutorialStep = context.CurrentTutorialStep,
        Layer1Win32 = new Win32Layer
        {
            LButtonDownReceived = win32DownReceived,
            Down = win32DownReceived ? PointerPosition.From(now, windowPosition, screenPosition) : null
        },
        Origin = win32DownReceived ? "Win32" : "WpfWithoutWin32"
    };

    private static PhysicalPointerAttempt GetOrCreateWpfAttempt(
        DateTimeOffset now,
        Point windowPosition,
        Point screenPosition,
        PointerDiagnosticContext context)
    {
        if (_activeAttempt is not null && now - _activeAttempt.StartedAt <= TimeSpan.FromSeconds(3))
            return _activeAttempt;
        _activeAttempt = CreateAttempt(now, windowPosition, screenPosition, context, win32DownReceived: false);
        if (_document is not null)
        {
            if (_document.Attempts.Count >= MaximumAttempts) _document.Attempts.RemoveAt(0);
            _document.Attempts.Add(_activeAttempt);
        }
        return _activeAttempt;
    }

    private static int NextAttemptNumber()
    {
        if (_document is null) return 1;
        var number = _document.NextAttemptNumber;
        _document.NextAttemptNumber++;
        return number;
    }

    private static bool CanCorrelateWithActiveAttempt(bool requireWpfDown)
    {
        if (_activeAttempt is null || DateTimeOffset.Now - _activeAttempt.StartedAt > TimeSpan.FromSeconds(3))
            return false;
        if (_activeAttempt.Layer1Win32.UpAt is { } upAt && DateTimeOffset.Now - upAt > TimeSpan.FromSeconds(1))
            return false;
        return !requireWpfDown || _activeAttempt.Layer2Wpf.PreviewMouseDownReceived;
    }

    private static bool TryConfirmPhysicalTarget(DependencyObject? source)
    {
        if (_activeAttempt is null || _activeAttempt.Layer4Action.ActionFinalized ||
            !_activeAttempt.Layer1Win32.LButtonDownReceived ||
            !_activeAttempt.Layer1Win32.LButtonUpReceived ||
            !_activeAttempt.Layer2Wpf.PreviewMouseDownReceived ||
            !_activeAttempt.Layer2Wpf.PreviewMouseUpReceived || _activeAttempt.LastWpfDownAt is null ||
            DateTimeOffset.Now - _activeAttempt.LastWpfDownAt.Value > TimeSpan.FromSeconds(1))
            return false;

        var candidateChain = ParentChain(source);
        var knownIds = _activeAttempt.LastWpfDownParentChain
            .Select(item => item.AutomationId)
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (knownIds.Count > 0 && candidateChain.Any(item => item.AutomationId.Length > 0 && knownIds.Contains(item.AutomationId)))
            return true;

        var target = _activeAttempt.LastWpfDownTarget;
        var candidate = Describe(source);
        return target.Type != "None" && string.Equals(target.Type, candidate.Type, StringComparison.Ordinal) &&
               string.Equals(target.ElementName, candidate.ElementName, StringComparison.Ordinal);
    }

    private static bool IsCloseLike(DependencyObject? source)
    {
        for (var current = source; current is not null; current = Parent(current))
        {
            if (current is Views.SurfaceCloseButton) return true;
            var automationId = AutomationProperties.GetAutomationId(current);
            if (automationId.Contains("Close", StringComparison.OrdinalIgnoreCase) ||
                automationId.Contains("Exit", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static void EnsureSession()
    {
        if (!IsEnabled || _document is not null) return;
        var now = DateTimeOffset.Now;
        _document = new PhysicalPointerSessionDocument
        {
            Protocol = Protocol,
            DiagnosticId = CreateDiagnosticId(now),
            StartedAt = now,
            Privacy = "Controls, sanitized visual types, automation identifiers, coordinates and input events only."
        };
    }

    private static string CreateDiagnosticId(DateTimeOffset now)
    {
        var date = now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var sequence = 1;
        try
        {
            var file = OutputFile();
            if (File.Exists(file))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(file, Utf8WithoutBom));
                if (json.RootElement.TryGetProperty("diagnostic_id", out var idElement))
                {
                    var previous = idElement.GetString();
                    var prefix = $"PT-INPUT-{date}-";
                    if (previous?.StartsWith(prefix, StringComparison.Ordinal) == true &&
                        int.TryParse(previous[prefix.Length..], out var previousSequence))
                        sequence = previousSequence + 1;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
        }
        return $"PT-INPUT-{date}-{sequence:D3}";
    }

    private static void UpdateContext(PointerDiagnosticContext context)
    {
        if (_document is null) return;
        _document.CurrentSurface = SafeToken(context.CurrentSurface);
        _document.CurrentOverlay = SafeToken(context.CurrentOverlay);
        _document.TutorialActive = context.TutorialActive;
        _document.CurrentTutorialStep = context.CurrentTutorialStep;
        _document.UpdatedAt = DateTimeOffset.Now;
    }

    private static PointerElementSnapshot Describe(DependencyObject? element)
    {
        if (element is null) return PointerElementSnapshot.None;
        var framework = element as FrameworkElement;
        var uiElement = element as UIElement;
        return new PointerElementSnapshot
        {
            Type = SafeToken(element.GetType().Name),
            ElementName = SafeToken(framework?.Name),
            AutomationId = SafeToken(AutomationProperties.GetAutomationId(element)),
            ZIndex = uiElement is null ? 0 : Panel.GetZIndex(uiElement),
            IsHitTestVisible = uiElement?.IsHitTestVisible ?? false,
            EffectiveIsEnabled = uiElement?.IsEnabled ?? false
        };
    }

    private static List<PointerElementSnapshot> ParentChain(DependencyObject? element)
    {
        var result = new List<PointerElementSnapshot>();
        for (var current = element; current is not null && result.Count < 24; current = Parent(current))
            result.Add(Describe(current));
        return result;
    }

    private static PointerElementSnapshot? FirstBlockingAncestor(DependencyObject? element)
    {
        for (var current = element; current is not null; current = Parent(current))
        {
            if (current is UIElement uiElement && (!uiElement.IsHitTestVisible || !uiElement.IsEnabled))
                return Describe(current);
        }
        return null;
    }

    private static DependencyObject? Parent(DependencyObject element)
    {
        try
        {
            return VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        }
        catch (InvalidOperationException)
        {
            return LogicalTreeHelper.GetParent(element);
        }
    }

    private static string SafeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new StringBuilder(Math.Min(value.Length, 80));
        foreach (var character in value.Take(80))
            builder.Append(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.' ? character : '_');
        return builder.ToString();
    }

    private static string OutputFile() => Path.Combine(AppDataPaths.Root, "InputDiagnostics", DiagnosticFileName);

    private static void Save()
    {
        if (_document is null) return;
        try
        {
            var path = OutputFile();
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp";
            var json = JsonSerializer.Serialize(_document, JsonOptions);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       8192,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
            File.Move(temporaryPath, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
        }
    }

    private sealed class PhysicalPointerSessionDocument
    {
        public string Protocol { get; set; } = string.Empty;
        public string DiagnosticId { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string Privacy { get; set; } = string.Empty;
        public string CurrentSurface { get; set; } = string.Empty;
        public string CurrentOverlay { get; set; } = string.Empty;
        public bool TutorialActive { get; set; }
        public int? CurrentTutorialStep { get; set; }
        public string Origin { get; set; } = string.Empty;
        public int NativeMouseMoveCount { get; set; }
        public PointerPosition? LastNativeMouseMove { get; set; }
        public int NextAttemptNumber { get; set; } = 1;
        public List<PhysicalPointerAttempt> Attempts { get; } = [];
        public List<CloseAuthorityCheck> CloseAuthorityChecks { get; } = [];
        public List<ActionEvent> UncorrelatedActionEvents { get; } = [];
        public string LastCloseAuthoritySignature { get; set; } = string.Empty;
    }

    private sealed class PhysicalPointerAttempt
    {
        public string AttemptId { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string CurrentSurface { get; set; } = string.Empty;
        public string CurrentOverlay { get; set; } = string.Empty;
        public bool TutorialActive { get; set; }
        public int? CurrentTutorialStep { get; set; }
        public string Origin { get; set; } = string.Empty;
        public Win32Layer Layer1Win32 { get; set; } = new();
        public WpfLayer Layer2Wpf { get; } = new();
        public TargetLayer Layer3Target { get; } = new();
        public ActionLayer Layer4Action { get; } = new();
        public PointerElementSnapshot LastWpfDownTarget { get; set; } = PointerElementSnapshot.None;
        public List<PointerElementSnapshot> LastWpfDownParentChain { get; set; } = [];
        public DateTimeOffset? LastWpfDownAt { get; set; }
    }

    private sealed class Win32Layer
    {
        public bool LButtonDownReceived { get; set; }
        public bool LButtonUpReceived { get; set; }
        public DateTimeOffset? UpAt { get; set; }
        public PointerPosition? Down { get; set; }
        public PointerPosition? Up { get; set; }
    }

    private sealed class WpfLayer
    {
        public bool PreviewMouseDownReceived { get; set; }
        public bool PreviewMouseUpReceived { get; set; }
        public List<WpfPointerEvent> Events { get; } = [];
    }

    private sealed class TargetLayer
    {
        public PointerElementSnapshot InputHitTest { get; set; } = PointerElementSnapshot.None;
        public PointerElementSnapshot VisualHitTest { get; set; } = PointerElementSnapshot.None;
        public List<PointerElementSnapshot> VisualParentChain { get; set; } = [];
        public PointerElementSnapshot? BlockingAncestor { get; set; }
    }

    private sealed class ActionLayer
    {
        public bool ButtonClickReceived { get; set; }
        public bool ShellEscapeEntered { get; set; }
        public bool TutorialOverlayDetached { get; set; }
        public bool SurfaceCloseDispatchCompleted { get; set; }
        public bool SurfaceClosed { get; set; }
        public bool PhysicalTargetConfirmed { get; set; }
        public bool ActionFinalized { get; set; }
        public PointerElementSnapshot Button { get; set; } = PointerElementSnapshot.None;
        public List<ActionEvent> Events { get; } = [];
    }

    private sealed class WpfPointerEvent
    {
        public DateTimeOffset Timestamp { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DiagnosticPoint WindowPosition { get; set; } = new();
        public DiagnosticPoint ScreenPosition { get; set; } = new();
        public PointerElementSnapshot OriginalSource { get; set; } = PointerElementSnapshot.None;
        public PointerElementSnapshot Source { get; set; } = PointerElementSnapshot.None;
        public bool Handled { get; set; }
    }

    private sealed class ActionEvent
    {
        public DateTimeOffset Timestamp { get; set; }
        public string EventName { get; set; } = string.Empty;
        public PointerElementSnapshot Control { get; set; } = PointerElementSnapshot.None;
        public PointerElementSnapshot OriginalSource { get; set; } = PointerElementSnapshot.None;
        public PointerElementSnapshot Source { get; set; } = PointerElementSnapshot.None;
        public bool Handled { get; set; }
    }

    private sealed class CloseAuthorityCheck
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Surface { get; set; } = string.Empty;
        public int VisibleCloseButtonCount { get; set; }
        public IReadOnlyList<string> VisibleAutomationIds { get; set; } = [];
        public string Result { get; set; } = string.Empty;
    }

    private sealed class PointerElementSnapshot
    {
        public static PointerElementSnapshot None => new() { Type = "None" };
        public string Type { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string AutomationId { get; set; } = string.Empty;
        public int ZIndex { get; set; }
        public bool IsHitTestVisible { get; set; }
        public bool EffectiveIsEnabled { get; set; }
    }

    private sealed class PointerPosition
    {
        public DateTimeOffset Timestamp { get; set; }
        public DiagnosticPoint WindowPosition { get; set; } = new();
        public DiagnosticPoint ScreenPosition { get; set; } = new();

        public static PointerPosition From(DateTimeOffset timestamp, Point window, Point screen) => new()
        {
            Timestamp = timestamp,
            WindowPosition = DiagnosticPoint.From(window),
            ScreenPosition = DiagnosticPoint.From(screen)
        };
    }

    private sealed class DiagnosticPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public static DiagnosticPoint From(Point point) => new()
        {
            X = Math.Round(point.X, 1),
            Y = Math.Round(point.Y, 1)
        };
    }
}
#endif
