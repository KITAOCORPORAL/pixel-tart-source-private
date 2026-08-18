using System.Windows.Input;

namespace PixelTart.Modules.AssetLibrary;

public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _running = true; RaiseCanExecuteChanged();
        try { await execute(); }
        finally { _running = false; RaiseCanExecuteChanged(); }
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
public sealed class AsyncCommand<T>(Func<T?, Task> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke(Convert(parameter)) ?? true);
    public async void Execute(object? parameter)
    {
        var converted = Convert(parameter); if (!CanExecute(parameter)) return;
        _running = true; RaiseCanExecuteChanged();
        try { await execute(converted); }
        finally { _running = false; RaiseCanExecuteChanged(); }
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    private static T? Convert(object? value)
    {
        if (value is null) return default;
        if (value is T typed) return typed;
        return (T?)System.Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class AssetCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter)) execute();
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
