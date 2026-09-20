using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RAWSelectionAssistant.Views;

// Keep the existing photographic layout; supply the missing interaction peer,
// not a Button template (which would change margins, chrome and image layout).
public sealed class PlanningReferenceTile : StackPanel
{
    public event EventHandler? Selected;
    public event EventHandler? OpenRequested;

    public PlanningReferenceTile()
    {
        Focusable = true;
        KeyboardNavigation.SetIsTabStop(this, true);
        Background = Brushes.Transparent;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ReferencePeer(this);

    public void Open()
    {
        if (!IsEnabled) throw new ElementNotEnabledException();
        Selected?.Invoke(this, EventArgs.Empty);
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus(); Selected?.Invoke(this, EventArgs.Empty);
        if (e.ClickCount == 2) Open();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HandleKey(e.Key, Keyboard.Modifiers)) e.Handled = true;
        base.OnKeyDown(e);
    }

    // The production key handler and in-process regression share this path.
    internal bool HandleKey(Key key, ModifierKeys modifiers)
    {
        if (!IsEnabled) return false;
        if (key is Key.Enter or Key.Space) { Open(); return true; }
        if (key == Key.Apps || key == Key.F10 && modifiers.HasFlag(ModifierKeys.Shift))
        {
            if (ContextMenu is null) return false;
            Selected?.Invoke(this, EventArgs.Empty);
            ContextMenu.PlacementTarget = this;
            ContextMenu.Placement = PlacementMode.Bottom;
            ContextMenu.IsOpen = true;
            return true;
        }
        return false;
    }

    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        Focus(); Selected?.Invoke(this, EventArgs.Empty);
        base.OnContextMenuOpening(e);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (IsKeyboardFocused && ActualWidth > 2 && ActualHeight > 2 && TryFindResource("AccentBrush") is Brush accent)
            dc.DrawLine(new Pen(accent, 2), new Point(-3, 0), new Point(-3, ActualHeight));
    }

    private sealed class ReferencePeer(PlanningReferenceTile owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => nameof(PlanningReferenceTile);
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
        public void Invoke()
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            owner.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(owner.Open));
        }
    }
}
