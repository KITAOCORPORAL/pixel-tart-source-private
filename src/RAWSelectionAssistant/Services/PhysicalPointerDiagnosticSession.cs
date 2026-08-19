#if INPUT_ROUTING_DIAGNOSTICS
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
#if MODULAR_HARNESS_DEV_PREVIEW
    private const string DevPreviewProcessName = "PixelTart_ModularHarness_V1_DevPreview";
    private const string DevPreviewOptInEnvironmentVariable = "PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS";
#endif
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
    private static PhysicalKeyAttempt? _activeKeyAttempt;
    private static DateTimeOffset _lastMouseMoveWrite;

    public static bool IsEnabled
    {
        get
        {
            var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
            if (processName.EndsWith(".Acceptance", StringComparison.OrdinalIgnoreCase)) return true;
#if MODULAR_HARNESS_DEV_PREVIEW
            return string.Equals(processName, DevPreviewProcessName, StringComparison.Ordinal) &&
                   string.Equals(
                       Environment.GetEnvironmentVariable(DevPreviewOptInEnvironmentVariable),
                       "1",
                       StringComparison.Ordinal);
#else
            return false;
#endif
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

    public static void RecordWin32Key(
        string message,
        int virtualKey,
        int scanCode,
        int repeatCount,
        ModifierKeys modifiers,
        int nativeMessageTime,
        bool isExtendedKey,
        bool wasPreviouslyDown,
        bool isTransitionUp,
        PointerDiagnosticContext context)
    {
        if (!IsEnabled || virtualKey is not (0x25 or 0x27)) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            var now = DateTimeOffset.Now;
            var nativeEvent = new NativeKeyEvent
            {
                Timestamp = now,
                Message = SafeToken(message),
                VirtualKey = virtualKey,
                ScanCode = scanCode,
                RepeatCount = Math.Max(1, repeatCount),
                Modifiers = SafeToken(modifiers.ToString()),
                NativeMessageTime = nativeMessageTime,
                IsExtendedKey = isExtendedKey,
                WasPreviouslyDown = wasPreviouslyDown,
                IsTransitionUp = isTransitionUp
            };
            if (string.Equals(message, "WM_KEYDOWN", StringComparison.Ordinal))
            {
                if (_activeKeyAttempt is null ||
                    _activeKeyAttempt.Layer1Win32.KeyUpReceived ||
                    _activeKeyAttempt.VirtualKey != virtualKey ||
                    now - _activeKeyAttempt.StartedAt > TimeSpan.FromSeconds(3))
                {
                    _activeKeyAttempt = CreateKeyAttempt(now, virtualKey, nativeEvent, context);
                    if (_document is not null)
                    {
                        if (_document.KeyAttempts.Count >= MaximumAttempts) _document.KeyAttempts.RemoveAt(0);
                        _document.KeyAttempts.Add(_activeKeyAttempt);
                    }
                }
                else
                {
                    _activeKeyAttempt.Layer1Win32.RepeatKeyDownCount += nativeEvent.RepeatCount;
                    _activeKeyAttempt.Layer1Win32.Events.Add(nativeEvent);
                    _activeKeyAttempt.UpdatedAt = now;
                }
            }
            else if (string.Equals(message, "WM_KEYUP", StringComparison.Ordinal) &&
                     _activeKeyAttempt is not null &&
                     _activeKeyAttempt.VirtualKey == virtualKey)
            {
                _activeKeyAttempt.Layer1Win32.KeyUpReceived = true;
                _activeKeyAttempt.Layer1Win32.KeyUpAt = now;
                _activeKeyAttempt.Layer1Win32.Up = nativeEvent;
                _activeKeyAttempt.Layer1Win32.Events.Add(nativeEvent);
                _activeKeyAttempt.UpdatedAt = now;
            }
            Save();
        }
    }

    public static void RecordWpfKey(
        DependencyObject control,
        DependencyObject? focusedElement,
        Key key,
        string eventName,
        bool isRepeat,
        ModifierKeys modifiers,
        DependencyObject? originalSource,
        DependencyObject? source,
        bool handled,
        PointerDiagnosticContext context)
    {
        if (!IsEnabled || key is not (Key.Left or Key.Right)) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            var now = DateTimeOffset.Now;
            var virtualKey = KeyInterop.VirtualKeyFromKey(key);
            if (_activeKeyAttempt is null ||
                _activeKeyAttempt.VirtualKey != virtualKey ||
                now - _activeKeyAttempt.StartedAt > TimeSpan.FromSeconds(3))
            {
                _activeKeyAttempt = CreateKeyAttempt(
                    now,
                    virtualKey,
                    nativeDown: null,
                    context: context,
                    win32DownReceived: false);
                if (_document is not null)
                {
                    if (_document.KeyAttempts.Count >= MaximumAttempts) _document.KeyAttempts.RemoveAt(0);
                    _document.KeyAttempts.Add(_activeKeyAttempt);
                }
            }

            var attempt = _activeKeyAttempt!;
            attempt.Layer2Wpf.Events.Add(new WpfKeyEvent
            {
                Timestamp = now,
                EventName = SafeToken(eventName),
                Key = SafeToken(key.ToString()),
                IsRepeat = isRepeat,
                Modifiers = SafeToken(modifiers.ToString()),
                OriginalSource = Describe(originalSource),
                Source = Describe(source),
                FocusedElement = Describe(focusedElement),
                Handled = handled
            });
            attempt.Layer3Target.Control = Describe(control);
            attempt.Layer3Target.ControlAutomationId = NearestAutomationId(control);
            var focusedAutomationId = NearestAutomationId(focusedElement);
            switch (eventName)
            {
                case "PreviewKeyDown":
                    attempt.Layer2Wpf.PreviewKeyDownReceived = true;
                    attempt.Layer3Target.FocusedElementAtDown = Describe(focusedElement);
                    attempt.Layer3Target.FocusedAutomationIdAtDown = focusedAutomationId;
                    attempt.Layer3Target.FocusParentChainAtDown = ParentChain(focusedElement);
                    break;
                case "KeyDown":
                    attempt.Layer2Wpf.KeyDownReceived = true;
                    break;
                case "PreviewKeyUp":
                    attempt.Layer2Wpf.PreviewKeyUpReceived = true;
                    break;
                case "KeyUp":
                    attempt.Layer2Wpf.KeyUpReceived = true;
                    attempt.Layer3Target.FocusedElementAtUp = Describe(focusedElement);
                    attempt.Layer3Target.FocusedAutomationIdAtUp = focusedAutomationId;
                    attempt.Layer3Target.FocusParentChainAtUp = ParentChain(focusedElement);
                    break;
            }
            attempt.UpdatedAt = now;
            Save();
        }
    }

    public static string BeginControlStateTransition(
        DependencyObject control,
        string inputKind,
        string inputKey,
        string expectedAdjustment,
        string controlKind,
        string propertyName,
        double beforeActualValue,
        double beforePersistedValue,
        double minimumValue,
        double maximumValue,
        bool beforeCollapsed,
        PointerDiagnosticContext context)
    {
        if (!IsEnabled ||
            !double.IsFinite(beforeActualValue) ||
            !double.IsFinite(beforePersistedValue) ||
            !double.IsFinite(minimumValue) ||
            !double.IsFinite(maximumValue))
            return string.Empty;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            if (_document is null) return string.Empty;

            var now = DateTimeOffset.Now;
            var safeInputKind = SafeToken(inputKind);
            var transition = new ControlStateTransition
            {
                TransitionId = $"control-{_document.NextControlTransitionNumber++:D3}",
                StartedAt = now,
                UpdatedAt = now,
                CurrentSurface = SafeToken(context.CurrentSurface),
                InputKind = safeInputKind,
                InputKey = SafeToken(inputKey),
                ExpectedAdjustment = SafeToken(expectedAdjustment),
                ControlKind = SafeToken(controlKind),
                PropertyName = SafeToken(propertyName),
                Control = Describe(control),
                BeforeActualValue = RoundStateValue(beforeActualValue),
                BeforePersistedValue = RoundStateValue(beforePersistedValue),
                MinimumValue = RoundStateValue(minimumValue),
                MaximumValue = RoundStateValue(maximumValue),
                BeforeCollapsed = beforeCollapsed
            };

            if (safeInputKind == "MouseDrag" &&
                _activeAttempt is not null &&
                now - _activeAttempt.StartedAt <= TimeSpan.FromSeconds(3))
            {
                transition.CorrelatedPointerAttemptId = _activeAttempt.AttemptId;
                transition.TargetMatchedAtStart = MatchesPointerTarget(_activeAttempt, control);
                _activeAttempt.Layer4Action.Events.Add(new ActionEvent
                {
                    Timestamp = now,
                    EventName = "ControlStateTransitionStarted",
                    Control = Describe(control)
                });
                _activeAttempt.UpdatedAt = now;
            }
            else if (safeInputKind == "Keyboard" &&
                     _activeKeyAttempt is not null &&
                     now - _activeKeyAttempt.StartedAt <= TimeSpan.FromSeconds(3))
            {
                transition.CorrelatedKeyAttemptId = _activeKeyAttempt.AttemptId;
                transition.TargetMatchedAtStart = MatchesKeyTarget(_activeKeyAttempt, control, requireUp: false);
            }

            if (_document.ControlStateTransitions.Count >= MaximumAttempts)
                _document.ControlStateTransitions.RemoveAt(0);
            _document.ControlStateTransitions.Add(transition);
            Save();
            return transition.TransitionId;
        }
    }

    public static void CompleteControlStateTransition(
        string transitionId,
        DependencyObject control,
        double afterActualValue,
        double afterPersistedValue,
        bool afterCollapsed,
        PointerDiagnosticContext context)
    {
        if (!IsEnabled || !double.IsFinite(afterActualValue) || !double.IsFinite(afterPersistedValue)) return;
        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            var transition = _document?.ControlStateTransitions.LastOrDefault(item =>
                string.Equals(item.TransitionId, transitionId, StringComparison.Ordinal));
            if (transition is null || transition.CompletedAt is not null) return;

            var now = DateTimeOffset.Now;
            transition.AfterActualValue = RoundStateValue(afterActualValue);
            transition.AfterPersistedValue = RoundStateValue(afterPersistedValue);
            transition.AfterCollapsed = afterCollapsed;
            transition.StateChanged = Math.Abs(transition.AfterActualValue.Value - transition.BeforeActualValue) >= 0.1d;
            transition.SettingsStateChanged = Math.Abs(transition.AfterPersistedValue.Value - transition.BeforePersistedValue) >= 0.1d;
            transition.SettingsWriteBackConfirmed = Math.Abs(transition.AfterActualValue.Value - transition.AfterPersistedValue.Value) <= 0.5d;
            transition.BoundaryReached = !transition.StateChanged && IsExpectedBoundary(transition);
            transition.CompletedAt = now;
            transition.UpdatedAt = now;

            if (transition.CorrelatedPointerAttemptId.Length > 0)
            {
                var pointerAttempt = _document!.Attempts.LastOrDefault(item =>
                    string.Equals(item.AttemptId, transition.CorrelatedPointerAttemptId, StringComparison.Ordinal));
                if (pointerAttempt is not null)
                {
                    transition.Layer1Win32Confirmed = pointerAttempt.Layer1Win32.LButtonDownReceived &&
                                                      pointerAttempt.Layer1Win32.LButtonUpReceived;
                    transition.Layer2WpfConfirmed = pointerAttempt.Layer2Wpf.PreviewMouseDownReceived &&
                                                   pointerAttempt.Layer2Wpf.PreviewMouseUpReceived;
                    transition.Layer3TargetConfirmed = transition.TargetMatchedAtStart &&
                                                       MatchesPointerUpTarget(pointerAttempt, control);
                    if (transition.Layer1Win32Confirmed && transition.Layer2WpfConfirmed && transition.Layer3TargetConfirmed)
                    {
                        pointerAttempt.Layer4Action.ControlStateTransitionConfirmed = true;
                        pointerAttempt.Layer4Action.PhysicalTargetConfirmed = true;
                    }
                    pointerAttempt.Layer4Action.Events.Add(new ActionEvent
                    {
                        Timestamp = now,
                        EventName = "ControlStateTransitionCompleted",
                        Control = Describe(control)
                    });
                    pointerAttempt.UpdatedAt = now;
                }
            }
            else if (transition.CorrelatedKeyAttemptId.Length > 0)
            {
                var keyAttempt = _document!.KeyAttempts.LastOrDefault(item =>
                    string.Equals(item.AttemptId, transition.CorrelatedKeyAttemptId, StringComparison.Ordinal));
                if (keyAttempt is not null)
                {
                    transition.Layer1Win32Confirmed = keyAttempt.Layer1Win32.KeyDownReceived &&
                                                      keyAttempt.Layer1Win32.KeyUpReceived;
                    transition.Layer2WpfConfirmed = keyAttempt.Layer2Wpf.PreviewKeyDownReceived &&
                                                   keyAttempt.Layer2Wpf.KeyDownReceived &&
                                                   keyAttempt.Layer2Wpf.PreviewKeyUpReceived &&
                                                   keyAttempt.Layer2Wpf.KeyUpReceived;
                    transition.Layer3TargetConfirmed = transition.TargetMatchedAtStart &&
                                                       MatchesKeyTarget(keyAttempt, control, requireUp: true);
                    keyAttempt.Layer4Action.ControlStateTransitionConfirmed =
                        transition.Layer1Win32Confirmed && transition.Layer2WpfConfirmed && transition.Layer3TargetConfirmed;
                    keyAttempt.Layer4Action.BeforeActualValue = transition.BeforeActualValue;
                    keyAttempt.Layer4Action.AfterActualValue = transition.AfterActualValue;
                    keyAttempt.Layer4Action.BeforePersistedValue = transition.BeforePersistedValue;
                    keyAttempt.Layer4Action.AfterPersistedValue = transition.AfterPersistedValue;
                    keyAttempt.Layer4Action.SettingsWriteBackConfirmed = transition.SettingsWriteBackConfirmed;
                    keyAttempt.Layer4Action.StateChanged = transition.StateChanged;
                    keyAttempt.Layer4Action.BoundaryReached = transition.BoundaryReached;
                    keyAttempt.Layer4Action.CompletedAt = now;
                    keyAttempt.Layer4Action.TransitionId = transition.TransitionId;
                    keyAttempt.UpdatedAt = now;
                }
            }

            var inputConfirmed = transition.Layer1Win32Confirmed &&
                                               transition.Layer2WpfConfirmed &&
                                               transition.Layer3TargetConfirmed;
            transition.BoundaryNoOpConfirmed = inputConfirmed &&
                                               transition.BoundaryReached &&
                                               transition.SettingsWriteBackConfirmed;
            transition.Layer4ActionConfirmed = inputConfirmed &&
                                               transition.SettingsWriteBackConfirmed &&
                                               (transition.StateChanged || transition.BoundaryNoOpConfirmed);
            transition.Result = transition.BoundaryNoOpConfirmed
                ? "BoundaryNoOpConfirmed"
                : transition.Layer4ActionConfirmed
                ? "Confirmed"
                : !inputConfirmed
                    ? "InputUnconfirmed"
                    : !transition.SettingsWriteBackConfirmed
                        ? "SettingsWriteBackMismatch"
                        : "UnexpectedNoStateChange";
            if (transition.CorrelatedKeyAttemptId.Length > 0)
            {
                var keyAttempt = _document!.KeyAttempts.LastOrDefault(item =>
                    string.Equals(item.AttemptId, transition.CorrelatedKeyAttemptId, StringComparison.Ordinal));
                if (keyAttempt is not null)
                    keyAttempt.Layer4Action.BoundaryNoOpConfirmed = transition.BoundaryNoOpConfirmed;
            }
            Save();
        }
    }

    public static void RecordWorkspaceRestoreState(
        double organizationActualWidth,
        double organizationPersistedWidth,
        bool organizationVisible,
        bool organizationCollapsed,
        double inspectorActualWidth,
        double inspectorPersistedWidth,
        bool inspectorVisible,
        bool inspectorCollapsed,
        double thumbnailActualWidth,
        double thumbnailPersistedWidth,
        PointerDiagnosticContext context)
    {
        if (!IsEnabled || new[]
            {
                organizationActualWidth,
                organizationPersistedWidth,
                inspectorActualWidth,
                inspectorPersistedWidth,
                thumbnailActualWidth,
                thumbnailPersistedWidth
            }.Any(value => !double.IsFinite(value)))
            return;

        lock (Gate)
        {
            EnsureSession();
            UpdateContext(context);
            if (_document is null) return;

            var signature = string.Join('|',
                RoundStateValue(organizationActualWidth),
                RoundStateValue(organizationPersistedWidth),
                organizationVisible,
                organizationCollapsed,
                RoundStateValue(inspectorActualWidth),
                RoundStateValue(inspectorPersistedWidth),
                inspectorVisible,
                inspectorCollapsed,
                RoundStateValue(thumbnailActualWidth),
                RoundStateValue(thumbnailPersistedWidth));
            if (string.Equals(_document.LastWorkspaceRestoreSignature, signature, StringComparison.Ordinal)) return;

            _document.LastWorkspaceRestoreSignature = signature;
            var snapshot = new WorkspaceRestoreSnapshot
            {
                Timestamp = DateTimeOffset.Now,
                CurrentSurface = SafeToken(context.CurrentSurface),
                OrganizationActualWidth = RoundStateValue(organizationActualWidth),
                OrganizationPersistedWidth = RoundStateValue(organizationPersistedWidth),
                OrganizationVisible = organizationVisible,
                OrganizationCollapsed = organizationCollapsed,
                OrganizationRestoreResult = PaneRestoreResult(
                    organizationActualWidth,
                    organizationPersistedWidth,
                    organizationVisible,
                    organizationCollapsed),
                InspectorActualWidth = RoundStateValue(inspectorActualWidth),
                InspectorPersistedWidth = RoundStateValue(inspectorPersistedWidth),
                InspectorVisible = inspectorVisible,
                InspectorCollapsed = inspectorCollapsed,
                InspectorRestoreResult = PaneRestoreResult(
                    inspectorActualWidth,
                    inspectorPersistedWidth,
                    inspectorVisible,
                    inspectorCollapsed),
                ThumbnailActualWidth = RoundStateValue(thumbnailActualWidth),
                ThumbnailPersistedWidth = RoundStateValue(thumbnailPersistedWidth),
                ThumbnailRestoreConfirmed = Math.Abs(thumbnailActualWidth - thumbnailPersistedWidth) <= 0.5d
            };
            snapshot.RestoreConfirmed = IsPaneRestoreAcceptable(snapshot.OrganizationRestoreResult) &&
                                        IsPaneRestoreAcceptable(snapshot.InspectorRestoreResult) &&
                                        snapshot.ThumbnailRestoreConfirmed;

            var previous = _document.PreviousSession;
            if (previous?.HasWorkspaceState == true)
            {
                snapshot.RestartComparisonPerformed = true;
                snapshot.PreviousDiagnosticId = previous.DiagnosticId;
                snapshot.RestartSettingsMatchPreviousSession =
                    Math.Abs(snapshot.OrganizationPersistedWidth - previous.OrganizationPersistedWidth) <= 0.5d &&
                    Math.Abs(snapshot.InspectorPersistedWidth - previous.InspectorPersistedWidth) <= 0.5d &&
                    Math.Abs(snapshot.ThumbnailPersistedWidth - previous.ThumbnailPersistedWidth) <= 0.5d &&
                    snapshot.OrganizationCollapsed == previous.OrganizationCollapsed &&
                    snapshot.InspectorCollapsed == previous.InspectorCollapsed;
            }

            if (_document.WorkspaceRestoreSnapshots.Count >= MaximumAttempts)
                _document.WorkspaceRestoreSnapshots.RemoveAt(0);
            _document.WorkspaceRestoreSnapshots.Add(snapshot);
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
        PointerButtonState buttonState;
        try
        {
            windowPosition = args.GetPosition(root);
            screenPosition = root.PointToScreen(windowPosition);
            inputHit = root.InputHitTest(windowPosition) as DependencyObject;
            visualHit = VisualTreeHelper.HitTest(root, windowPosition)?.VisualHit;
            buttonState = CaptureButtonState(args.OriginalSource as DependencyObject);
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
                Handled = args.Handled,
                ButtonState = buttonState
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
                attempt.DownControlAutomationId = NearestAutomationId(args.OriginalSource as DependencyObject);
                attempt.DownTargetAutomationId = buttonState.AutomationId;
                attempt.DownButtonInstanceId = buttonState.InstanceId;
            }
            if (eventName.Contains("LeftButtonUp", StringComparison.Ordinal) && args.ChangedButton == MouseButton.Left)
            {
                attempt.UpControlAutomationId = NearestAutomationId(args.OriginalSource as DependencyObject);
                attempt.UpTargetAutomationId = buttonState.AutomationId;
                attempt.UpButtonInstanceId = buttonState.InstanceId;
                attempt.ButtonInstanceSameDownUp = attempt.DownButtonInstanceId is not null &&
                                                   attempt.UpButtonInstanceId is not null &&
                                                   attempt.DownButtonInstanceId == attempt.UpButtonInstanceId;
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

    public static void RecordPointerDownEscapeTarget(
        DependencyObject escapeOwner,
        DependencyObject? originalSource,
        ShellEscapePointerAction action)
    {
        if (!IsEnabled || action == ShellEscapePointerAction.None) return;
        lock (Gate)
        {
            EnsureSession();
            if (!CanConfirmPhysicalPointerDownEscape(originalSource, escapeOwner, action)) return;

            var attempt = _activeAttempt!;
            attempt.Layer4Action.PhysicalTargetConfirmed = true;
            attempt.Layer4Action.PointerDownEscapeTargetConfirmed = true;
            attempt.Layer4Action.ActionFinalized = true;
            attempt.Layer4Action.PointerDownEscapeAction = SafeToken(action.ToString());
            attempt.Layer4Action.Button = Describe(escapeOwner);
            attempt.Layer4Action.Events.Add(new ActionEvent
            {
                Timestamp = DateTimeOffset.Now,
                EventName = "PointerDownEscapeTargetConfirmed",
                Control = Describe(escapeOwner),
                OriginalSource = Describe(originalSource),
                Source = Describe(escapeOwner),
                Handled = false
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

    private static PhysicalKeyAttempt CreateKeyAttempt(
        DateTimeOffset now,
        int virtualKey,
        NativeKeyEvent? nativeDown,
        PointerDiagnosticContext context,
        bool win32DownReceived = true) => new()
    {
        AttemptId = $"key-{NextKeyAttemptNumber():D3}",
        StartedAt = now,
        UpdatedAt = now,
        CurrentSurface = SafeToken(context.CurrentSurface),
        VirtualKey = virtualKey,
        Key = SafeToken(KeyInterop.KeyFromVirtualKey(virtualKey).ToString()),
        Origin = win32DownReceived ? "Win32" : "WpfWithoutWin32",
        Layer1Win32 = new KeyWin32Layer
        {
            KeyDownReceived = win32DownReceived,
            KeyDownAt = win32DownReceived ? now : null,
            RepeatKeyDownCount = nativeDown?.RepeatCount ?? 0,
            Down = nativeDown,
            Events = nativeDown is null ? [] : [nativeDown]
        }
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

    private static int NextKeyAttemptNumber()
    {
        if (_document is null) return 1;
        var number = _document.NextKeyAttemptNumber;
        _document.NextKeyAttemptNumber++;
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

        return MatchesLastWpfDownTarget(source);
    }

    private static bool CanConfirmPhysicalPointerDownEscape(
        DependencyObject? originalSource,
        DependencyObject escapeOwner,
        ShellEscapePointerAction action)
    {
        if (_activeAttempt is null || _activeAttempt.Layer4Action.ActionFinalized ||
            !_activeAttempt.Layer1Win32.LButtonDownReceived ||
            !_activeAttempt.Layer2Wpf.PreviewMouseDownReceived ||
            _activeAttempt.LastWpfDownAt is null ||
            DateTimeOffset.Now - _activeAttempt.LastWpfDownAt.Value > TimeSpan.FromSeconds(1))
            return false;

        return ShellEscapePointer.GetAction(escapeOwner) == action &&
               MatchesLastWpfDownTarget(originalSource) &&
               IsAncestorOrSelf(escapeOwner, originalSource);
    }

    private static bool IsAncestorOrSelf(DependencyObject ancestor, DependencyObject? element)
    {
        for (var current = element; current is not null; current = Parent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    private static bool MatchesLastWpfDownTarget(DependencyObject? source)
    {
        if (_activeAttempt is null || source is null) return false;
        return MatchesPointerTarget(_activeAttempt, source);
    }

    private static bool MatchesPointerTarget(PhysicalPointerAttempt attempt, DependencyObject source)
    {
        var candidateChain = ParentChain(source);
        var knownIds = attempt.LastWpfDownParentChain
            .Select(item => item.AutomationId)
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (knownIds.Count > 0 && candidateChain.Any(item => item.AutomationId.Length > 0 && knownIds.Contains(item.AutomationId)))
            return true;

        var target = attempt.LastWpfDownTarget;
        var candidate = Describe(source);
        return target.Type != "None" && string.Equals(target.Type, candidate.Type, StringComparison.Ordinal) &&
               string.Equals(target.ElementName, candidate.ElementName, StringComparison.Ordinal);
    }

    private static bool MatchesPointerUpTarget(PhysicalPointerAttempt attempt, DependencyObject control)
    {
        var controlAutomationId = NearestAutomationId(control);
        return controlAutomationId.Length > 0 &&
               string.Equals(attempt.DownControlAutomationId, controlAutomationId, StringComparison.Ordinal) &&
               string.Equals(attempt.UpControlAutomationId, controlAutomationId, StringComparison.Ordinal);
    }

    private static bool MatchesKeyTarget(PhysicalKeyAttempt attempt, DependencyObject control, bool requireUp)
    {
        var controlAutomationId = NearestAutomationId(control);
        if (controlAutomationId.Length == 0 ||
            !string.Equals(attempt.Layer3Target.ControlAutomationId, controlAutomationId, StringComparison.Ordinal) ||
            !string.Equals(attempt.Layer3Target.FocusedAutomationIdAtDown, controlAutomationId, StringComparison.Ordinal))
            return false;
        return !requireUp ||
               string.Equals(attempt.Layer3Target.FocusedAutomationIdAtUp, controlAutomationId, StringComparison.Ordinal);
    }

    private static bool IsExpectedBoundary(ControlStateTransition transition) => transition.ExpectedAdjustment switch
    {
        "Decrease" => transition.BeforeActualValue <= transition.MinimumValue + 0.5d,
        "Increase" => transition.BeforeActualValue >= transition.MaximumValue - 0.5d,
        _ => false
    };

    private static string PaneRestoreResult(
        double actualWidth,
        double persistedWidth,
        bool visible,
        bool collapsed)
    {
        if (collapsed)
            return actualWidth <= 0.5d && persistedWidth > 0.5d ? "CollapsedRestored" : "CollapsedRestoreMismatch";
        if (!visible)
            return actualWidth <= 0.5d ? "DeferredByViewport" : "ResponsiveHideMismatch";
        return Math.Abs(actualWidth - persistedWidth) <= 0.5d ? "ExpandedRestored" : "ExpandedRestoreMismatch";
    }

    private static bool IsPaneRestoreAcceptable(string result) => result is
        "CollapsedRestored" or "DeferredByViewport" or "ExpandedRestored";

    private static double RoundStateValue(double value) => Math.Round(value, 3);

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
        var previousSession = ReadPreviousSessionSummary();
        using var process = Process.GetCurrentProcess();
        _document = new PhysicalPointerSessionDocument
        {
            Protocol = Protocol,
            DiagnosticId = CreateDiagnosticId(now),
            StartedAt = now,
            ProcessId = Environment.ProcessId,
            ProcessStartedAt = process.StartTime,
            PreviousSession = previousSession,
            Privacy = "Controls, sanitized visual types, automation identifiers, coordinates and input events only."
        };
    }

    private static PreviousSessionSummary? ReadPreviousSessionSummary()
    {
        try
        {
            var file = OutputFile();
            if (!File.Exists(file)) return null;
            using var json = JsonDocument.Parse(File.ReadAllText(file, Utf8WithoutBom));
            var root = json.RootElement;
            if (!root.TryGetProperty("diagnostic_id", out var diagnosticIdElement)) return null;
            var diagnosticId = SafeToken(diagnosticIdElement.GetString());
            if (diagnosticId.Length == 0) return null;

            var summary = new PreviousSessionSummary
            {
                DiagnosticId = diagnosticId,
                ProcessId = root.TryGetProperty("process_id", out var processIdElement) && processIdElement.TryGetInt32(out var processId)
                    ? processId
                    : 0
            };
            if (!root.TryGetProperty("workspace_restore_snapshots", out var snapshotsElement) ||
                snapshotsElement.ValueKind != JsonValueKind.Array)
                return summary;

            var snapshots = snapshotsElement.EnumerateArray().ToArray();
            if (snapshots.Length == 0) return summary;
            var last = snapshots[^1];
            if (!TryReadFiniteDouble(last, "organization_persisted_width", out var organizationWidth) ||
                !TryReadFiniteDouble(last, "inspector_persisted_width", out var inspectorWidth) ||
                !TryReadFiniteDouble(last, "thumbnail_persisted_width", out var thumbnailWidth) ||
                !last.TryGetProperty("organization_collapsed", out var organizationCollapsedElement) ||
                !last.TryGetProperty("inspector_collapsed", out var inspectorCollapsedElement) ||
                organizationCollapsedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                inspectorCollapsedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return summary;

            summary.HasWorkspaceState = true;
            summary.OrganizationPersistedWidth = organizationWidth;
            summary.InspectorPersistedWidth = inspectorWidth;
            summary.ThumbnailPersistedWidth = thumbnailWidth;
            summary.OrganizationCollapsed = organizationCollapsedElement.GetBoolean();
            summary.InspectorCollapsed = inspectorCollapsedElement.GetBoolean();
            return summary;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool TryReadFiniteDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0d;
        return element.TryGetProperty(propertyName, out var property) &&
               property.TryGetDouble(out value) &&
               double.IsFinite(value);
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

    private static string NearestAutomationId(DependencyObject? element)
    {
        for (var current = element; current is not null; current = Parent(current))
        {
            var automationId = SafeToken(AutomationProperties.GetAutomationId(current));
            if (automationId.Length > 0) return automationId;
        }
        return string.Empty;
    }

    private static PointerButtonState CaptureButtonState(DependencyObject? element)
    {
        var button = FindAncestor<ButtonBase>(element);
        var captured = Mouse.Captured as DependencyObject;
        return new PointerButtonState
        {
            AutomationId = SafeToken(button is null ? string.Empty : AutomationProperties.GetAutomationId(button)),
            InstanceId = button is null ? null : RuntimeHelpers.GetHashCode(button),
            MouseCapturedElement = Describe(captured),
            IsMouseCaptured = button?.IsMouseCaptured ?? false,
            IsMouseCaptureWithin = button?.IsMouseCaptureWithin ?? false,
            IsPressed = button?.IsPressed ?? false,
            IsEnabled = button?.IsEnabled ?? false,
            IsHitTestVisible = button?.IsHitTestVisible ?? false,
            Visibility = SafeToken(button?.Visibility.ToString()),
            VisualParent = Describe(button is null ? null : Parent(button)),
            ClickMode = SafeToken(button?.ClickMode.ToString()),
            CommandCanExecute = button is Button commandButton && commandButton.Command is not null
                ? commandButton.Command.CanExecute(commandButton.CommandParameter)
                : true
        };
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        for (var current = element; current is not null; current = Parent(current))
            if (current is T match) return match;
        return null;
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
        public int ProcessId { get; set; }
        public DateTimeOffset ProcessStartedAt { get; set; }
        public PreviousSessionSummary? PreviousSession { get; set; }
        public string Privacy { get; set; } = string.Empty;
        public string CurrentSurface { get; set; } = string.Empty;
        public string CurrentOverlay { get; set; } = string.Empty;
        public bool TutorialActive { get; set; }
        public int? CurrentTutorialStep { get; set; }
        public string Origin { get; set; } = string.Empty;
        public int NativeMouseMoveCount { get; set; }
        public PointerPosition? LastNativeMouseMove { get; set; }
        public int NextAttemptNumber { get; set; } = 1;
        public int NextKeyAttemptNumber { get; set; } = 1;
        public int NextControlTransitionNumber { get; set; } = 1;
        public List<PhysicalPointerAttempt> Attempts { get; } = [];
        public List<PhysicalKeyAttempt> KeyAttempts { get; } = [];
        public List<ControlStateTransition> ControlStateTransitions { get; } = [];
        public List<WorkspaceRestoreSnapshot> WorkspaceRestoreSnapshots { get; } = [];
        public List<CloseAuthorityCheck> CloseAuthorityChecks { get; } = [];
        public List<ActionEvent> UncorrelatedActionEvents { get; } = [];
        public string LastCloseAuthoritySignature { get; set; } = string.Empty;
        public string LastWorkspaceRestoreSignature { get; set; } = string.Empty;
    }

    private sealed class PreviousSessionSummary
    {
        public string DiagnosticId { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public bool HasWorkspaceState { get; set; }
        public double OrganizationPersistedWidth { get; set; }
        public double InspectorPersistedWidth { get; set; }
        public double ThumbnailPersistedWidth { get; set; }
        public bool OrganizationCollapsed { get; set; }
        public bool InspectorCollapsed { get; set; }
    }

    private sealed class WorkspaceRestoreSnapshot
    {
        public DateTimeOffset Timestamp { get; set; }
        public string CurrentSurface { get; set; } = string.Empty;
        public double OrganizationActualWidth { get; set; }
        public double OrganizationPersistedWidth { get; set; }
        public bool OrganizationVisible { get; set; }
        public bool OrganizationCollapsed { get; set; }
        public string OrganizationRestoreResult { get; set; } = string.Empty;
        public double InspectorActualWidth { get; set; }
        public double InspectorPersistedWidth { get; set; }
        public bool InspectorVisible { get; set; }
        public bool InspectorCollapsed { get; set; }
        public string InspectorRestoreResult { get; set; } = string.Empty;
        public double ThumbnailActualWidth { get; set; }
        public double ThumbnailPersistedWidth { get; set; }
        public bool ThumbnailRestoreConfirmed { get; set; }
        public bool RestoreConfirmed { get; set; }
        public bool RestartComparisonPerformed { get; set; }
        public string PreviousDiagnosticId { get; set; } = string.Empty;
        public bool RestartSettingsMatchPreviousSession { get; set; }
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
        public string DownTargetAutomationId { get; set; } = string.Empty;
        public string UpTargetAutomationId { get; set; } = string.Empty;
        public string DownControlAutomationId { get; set; } = string.Empty;
        public string UpControlAutomationId { get; set; } = string.Empty;
        public int? DownButtonInstanceId { get; set; }
        public int? UpButtonInstanceId { get; set; }
        public bool? ButtonInstanceSameDownUp { get; set; }
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
        public bool ControlStateTransitionConfirmed { get; set; }
        public bool PointerDownEscapeTargetConfirmed { get; set; }
        public bool ActionFinalized { get; set; }
        public string PointerDownEscapeAction { get; set; } = string.Empty;
        public PointerElementSnapshot Button { get; set; } = PointerElementSnapshot.None;
        public List<ActionEvent> Events { get; } = [];
    }

    private sealed class PhysicalKeyAttempt
    {
        public string AttemptId { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string CurrentSurface { get; set; } = string.Empty;
        public int VirtualKey { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public KeyWin32Layer Layer1Win32 { get; set; } = new();
        public KeyWpfLayer Layer2Wpf { get; } = new();
        public KeyTargetLayer Layer3Target { get; } = new();
        public KeyActionLayer Layer4Action { get; } = new();
    }

    private sealed class KeyWin32Layer
    {
        public bool KeyDownReceived { get; set; }
        public bool KeyUpReceived { get; set; }
        public DateTimeOffset? KeyDownAt { get; set; }
        public DateTimeOffset? KeyUpAt { get; set; }
        public int RepeatKeyDownCount { get; set; }
        public NativeKeyEvent? Down { get; set; }
        public NativeKeyEvent? Up { get; set; }
        public List<NativeKeyEvent> Events { get; set; } = [];
    }

    private sealed class KeyWpfLayer
    {
        public bool PreviewKeyDownReceived { get; set; }
        public bool KeyDownReceived { get; set; }
        public bool PreviewKeyUpReceived { get; set; }
        public bool KeyUpReceived { get; set; }
        public List<WpfKeyEvent> Events { get; } = [];
    }

    private sealed class KeyTargetLayer
    {
        public PointerElementSnapshot Control { get; set; } = PointerElementSnapshot.None;
        public string ControlAutomationId { get; set; } = string.Empty;
        public PointerElementSnapshot FocusedElementAtDown { get; set; } = PointerElementSnapshot.None;
        public PointerElementSnapshot FocusedElementAtUp { get; set; } = PointerElementSnapshot.None;
        public string FocusedAutomationIdAtDown { get; set; } = string.Empty;
        public string FocusedAutomationIdAtUp { get; set; } = string.Empty;
        public List<PointerElementSnapshot> FocusParentChainAtDown { get; set; } = [];
        public List<PointerElementSnapshot> FocusParentChainAtUp { get; set; } = [];
    }

    private sealed class KeyActionLayer
    {
        public bool ControlStateTransitionConfirmed { get; set; }
        public double BeforeActualValue { get; set; }
        public double? AfterActualValue { get; set; }
        public double BeforePersistedValue { get; set; }
        public double? AfterPersistedValue { get; set; }
        public bool SettingsWriteBackConfirmed { get; set; }
        public bool StateChanged { get; set; }
        public bool BoundaryReached { get; set; }
        public bool BoundaryNoOpConfirmed { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string TransitionId { get; set; } = string.Empty;
    }

    private sealed class NativeKeyEvent
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
        public int VirtualKey { get; set; }
        public int ScanCode { get; set; }
        public int RepeatCount { get; set; }
        public string Modifiers { get; set; } = string.Empty;
        public int NativeMessageTime { get; set; }
        public bool IsExtendedKey { get; set; }
        public bool WasPreviouslyDown { get; set; }
        public bool IsTransitionUp { get; set; }
    }

    private sealed class WpfKeyEvent
    {
        public DateTimeOffset Timestamp { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public bool IsRepeat { get; set; }
        public string Modifiers { get; set; } = string.Empty;
        public PointerElementSnapshot OriginalSource { get; set; } = PointerElementSnapshot.None;
        public PointerElementSnapshot Source { get; set; } = PointerElementSnapshot.None;
        public PointerElementSnapshot FocusedElement { get; set; } = PointerElementSnapshot.None;
        public bool Handled { get; set; }
    }

    private sealed class ControlStateTransition
    {
        public string TransitionId { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string CurrentSurface { get; set; } = string.Empty;
        public string InputKind { get; set; } = string.Empty;
        public string InputKey { get; set; } = string.Empty;
        public string ExpectedAdjustment { get; set; } = string.Empty;
        public string ControlKind { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public PointerElementSnapshot Control { get; set; } = PointerElementSnapshot.None;
        public double BeforeActualValue { get; set; }
        public double? AfterActualValue { get; set; }
        public double BeforePersistedValue { get; set; }
        public double? AfterPersistedValue { get; set; }
        public double MinimumValue { get; set; }
        public double MaximumValue { get; set; }
        public bool BeforeCollapsed { get; set; }
        public bool AfterCollapsed { get; set; }
        public bool StateChanged { get; set; }
        public bool SettingsStateChanged { get; set; }
        public bool SettingsWriteBackConfirmed { get; set; }
        public bool BoundaryReached { get; set; }
        public bool BoundaryNoOpConfirmed { get; set; }
        public string CorrelatedPointerAttemptId { get; set; } = string.Empty;
        public string CorrelatedKeyAttemptId { get; set; } = string.Empty;
        public bool TargetMatchedAtStart { get; set; }
        public bool Layer1Win32Confirmed { get; set; }
        public bool Layer2WpfConfirmed { get; set; }
        public bool Layer3TargetConfirmed { get; set; }
        public bool Layer4ActionConfirmed { get; set; }
        public string Result { get; set; } = string.Empty;
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
        public PointerButtonState ButtonState { get; set; } = new();
    }

    private sealed class PointerButtonState
    {
        public string AutomationId { get; set; } = string.Empty;
        public int? InstanceId { get; set; }
        public PointerElementSnapshot MouseCapturedElement { get; set; } = PointerElementSnapshot.None;
        public bool IsMouseCaptured { get; set; }
        public bool IsMouseCaptureWithin { get; set; }
        public bool IsPressed { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsHitTestVisible { get; set; }
        public string Visibility { get; set; } = string.Empty;
        public PointerElementSnapshot VisualParent { get; set; } = PointerElementSnapshot.None;
        public string ClickMode { get; set; } = string.Empty;
        public bool CommandCanExecute { get; set; }
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
