using System.Windows.Input;

namespace RAWSelectionAssistant.Utilities;

public sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null, bool allowConcurrent = false) : ICommand
{
    private int _executions;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => (allowConcurrent || _executions == 0) && (canExecute?.Invoke(parameter) ?? true);

    public void Execute(object? parameter)
    {
        _ = ExecuteAsync(parameter);
    }

    public async Task ExecuteAsync(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _executions++;
        RaiseCanExecuteChanged();
        try
        {
            await execute(parameter);
        }
        finally
        {
            _executions--;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
