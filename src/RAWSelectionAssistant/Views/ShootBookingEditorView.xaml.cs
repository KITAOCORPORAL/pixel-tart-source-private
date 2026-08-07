using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class ShootBookingEditorView : UserControl
{
    private ShootBookingEditorViewModel? _viewModel;
    public ShootBookingEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.FocusFieldRequested -= FocusFieldRequested;
        _viewModel = e.NewValue as ShootBookingEditorViewModel;
        if (_viewModel is not null) _viewModel.FocusFieldRequested += FocusFieldRequested;
    }

    private void FocusFieldRequested(object? sender, string field)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (field == "Title") TitleInput.Focus();
            else StartDateInput.Focus();
            Keyboard.Focus(field == "Title" ? TitleInput : StartDateInput);
        });
    }
}
