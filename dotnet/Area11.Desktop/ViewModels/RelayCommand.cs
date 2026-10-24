using System.Windows.Input;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>A command that runs one method; no business logic lives here.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        // One failed click must not take the counter down (v0.34.0, D-90). The method name is the
        // button's name, so the sentence, the log line and the crash file all agree without a
        // hundred call sites being edited.
        CommandGuard.Run(CommandGuard.NameOf(_execute), _execute);
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A command that carries the clicked row with it - the "close this reminder" button inside a list
/// needs to know WHICH reminder (D-50). Kept tiny on purpose: the work is in the view model.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        parameter is T typed ? _canExecute?.Invoke(typed) ?? true : _canExecute?.Invoke(default) ?? true;

    public void Execute(object? parameter) =>
        CommandGuard.Run(CommandGuard.NameOf(_execute),
            () => _execute(parameter is T typed ? typed : default));

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
