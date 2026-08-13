using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RAWSelectionAssistant.Services;

public enum ShellEscapePointerAction
{
    None,
    CloseCurrentSurface,
    ExitTutorial
}

public static class ShellEscapePointer
{
    private static readonly ConditionalWeakTable<InputEventArgs, DispatchState> DispatchStates = new();

    public static readonly DependencyProperty ActionProperty = DependencyProperty.RegisterAttached(
        "Action",
        typeof(ShellEscapePointerAction),
        typeof(ShellEscapePointer),
        new FrameworkPropertyMetadata(ShellEscapePointerAction.None));

    public static void SetAction(DependencyObject element, ShellEscapePointerAction value) =>
        element.SetValue(ActionProperty, value);

    public static ShellEscapePointerAction GetAction(DependencyObject element) =>
        (ShellEscapePointerAction)element.GetValue(ActionProperty);

    public static bool TryResolve(
        DependencyObject? source,
        out DependencyObject? owner,
        out ShellEscapePointerAction action)
    {
        for (var current = source; current is not null; current = Parent(current))
        {
            var localValue = current.ReadLocalValue(ActionProperty);
            if (localValue == DependencyProperty.UnsetValue) continue;

            owner = current;
            action = (ShellEscapePointerAction)localValue;
            return action != ShellEscapePointerAction.None;
        }

        owner = null;
        action = ShellEscapePointerAction.None;
        return false;
    }

    public static bool TryBeginDispatch(InputEventArgs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var state = DispatchStates.GetOrCreateValue(input);
        lock (state)
        {
            if (state.Dispatched) return false;

            state.Dispatched = true;
            return true;
        }
    }

    private static DependencyObject? Parent(DependencyObject element)
    {
        if (element is FrameworkContentElement contentElement && contentElement.Parent is not null)
            return contentElement.Parent;

        try
        {
            if (element is Visual or Visual3D)
                return VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        }
        catch (InvalidOperationException)
        {
        }

        return LogicalTreeHelper.GetParent(element);
    }

    private sealed class DispatchState
    {
        public bool Dispatched { get; set; }
    }
}
