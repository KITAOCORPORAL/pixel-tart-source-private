using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class WorkCalendarView : UserControl
{
    private WorkCalendarViewModel? _subscribedViewModel;
    public WorkCalendarView() => InitializeComponent();

    private async void WorkCalendarView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkCalendarViewModel viewModel) return;
        if (!ReferenceEquals(_subscribedViewModel, viewModel))
        {
            if (_subscribedViewModel is not null)
            {
                _subscribedViewModel.EditorRequested -= ViewModel_EditorRequested;
                _subscribedViewModel.SearchFocusRequested -= ViewModel_SearchFocusRequested;
            }
            _subscribedViewModel = viewModel;
            viewModel.EditorRequested += ViewModel_EditorRequested;
            viewModel.SearchFocusRequested += ViewModel_SearchFocusRequested;
        }
        await viewModel.InitializeAsync();
        Focus();
    }

    private void ViewModel_SearchFocusRequested(object? sender, EventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void ViewModel_EditorRequested(object? sender, BookingEditorRequestEventArgs e)
    {
        var previousFocus = Keyboard.FocusedElement;
        var owner = Window.GetWindow(this);
        var ownerOpacity = owner?.Opacity ?? 1d;
        var window = new Window
        {
            Title = e.Editor.DialogTitle,
            Owner = owner,
            Content = new ShootBookingEditorView { DataContext = e.Editor },
            Width = Math.Min(920, SystemParameters.WorkArea.Width - 80),
            Height = Math.Min(820, SystemParameters.WorkArea.Height - 80),
            MinWidth = 680,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Application.Current.TryFindResource("WindowBackgroundBrush") as System.Windows.Media.Brush
        };
        window.InputBindings.Add(new KeyBinding(e.Editor.CancelCommand, Key.Escape, ModifierKeys.None));
        e.Editor.CloseRequested += (_, _) => window.Close();
        window.Closing += (_, args) =>
        {
            if (!e.Editor.WasSaved && !e.Editor.ConfirmDiscardChanges()) args.Cancel = true;
        };
        window.Closed += (_, _) => previousFocus?.Focus();
        try
        {
            if (owner is not null) owner.Opacity = 0.72;
            window.ShowDialog();
        }
        finally
        {
            if (owner is not null) owner.Opacity = ownerOpacity;
        }
    }
}
