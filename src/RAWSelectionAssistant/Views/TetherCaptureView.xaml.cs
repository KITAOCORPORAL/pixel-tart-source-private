using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public sealed class TetherFullScreenChangedEventArgs(bool isFullScreen) : EventArgs
{
    public bool IsFullScreen { get; } = isFullScreen;
}

public partial class TetherCaptureView : UserControl
{
    private Point _dragStart;
    private double _panStartX;
    private double _panStartY;
    private bool _isDragging;

    public TetherCaptureView()
    {
        InitializeComponent();
        DataContextChanged += View_DataContextChanged;
        Loaded += (_, _) => UpdateResponsiveLayout();
    }

    public event EventHandler<TetherFullScreenChangedEventArgs>? FullScreenChanged;

    private TetherCaptureViewModel? ViewModel => DataContext as TetherCaptureViewModel;

    private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldValue) oldValue.PropertyChanged -= ViewModel_PropertyChanged;
        if (e.NewValue is INotifyPropertyChanged newValue) newValue.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TetherCaptureViewModel.SelectedAsset) && ViewModel?.SelectedAsset is { } selected)
            Dispatcher.BeginInvoke(() => AssetList.ScrollIntoView(selected));
        else if (e.PropertyName == nameof(TetherCaptureViewModel.IsFullScreen) && ViewModel is not null)
            FullScreenChanged?.Invoke(this, new(ViewModel.IsFullScreen));
    }

    private void View_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout();

    private void UpdateResponsiveLayout()
    {
        var availableWidth = ActualWidth > 0 ? ActualWidth : Window.GetWindow(this)?.ActualWidth ?? 0;
        ApplyResponsiveLayout(availableWidth);
    }

    public void ApplyReviewPresentation(string state, double windowWidth)
    {
        ApplyResponsiveLayout(windowWidth);
        if (ViewModel is not null)
            ViewModel.ShowInspectorDrawer = string.Equals(state, "TetherCompact1280", StringComparison.Ordinal);
        InspectorPanel.UpdateLayout();
        var scroll = FindVisualChildren<ScrollViewer>(InspectorPanel)
            .OrderByDescending(element => element.ScrollableHeight)
            .FirstOrDefault();
        var offset = state switch
        {
            "TetherAnnotations" => 650,
            "TetherSideBySide" or "TetherOverlayCompare" => 880,
            "TetherReference" => 1110,
            "LutNone" or "LutImported" or "LutStrength50" or "LutBeforeAfter" or "LutSplitView" or "LutInvalid" => 370,
            "ColorProfileDetected" or "ColorProfileFallback" or "ClientMonitorSelector" or "ClientMonitorFollowMain" or "ClientMonitorFollowLatest" or "ClientMonitorLocked" or "ClientMonitorPrivacy" or "ClientMonitorFavoriteNote" or "ClientMonitorDisconnected" or "ClientMonitorReconnected" or "MixedDpi" => 750,
            _ => 0
        };
        scroll?.ScrollToVerticalOffset(offset);
        scroll?.UpdateLayout();
        InspectorPanel.UpdateLayout();
    }

    private void ApplyResponsiveLayout(double windowWidth)
    {
        var compact = windowWidth < 1350;
        ThumbnailColumn.Width = new GridLength(compact ? 230 : 270);
        InspectorColumn.MinWidth = compact ? 0 : 280;
        InspectorColumn.MaxWidth = compact ? 0 : 340;
        InspectorColumn.Width = compact ? new GridLength(0) : new GridLength(320);
        InspectorPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        if (ViewModel is not null)
        {
            ViewModel.IsInspectorCollapsed = compact;
            if (!compact) ViewModel.ShowInspectorDrawer = false;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    private async void ThumbnailItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: TetherAssetItemViewModel item }) await item.LoadThumbnailAsync();
    }

    private void ThumbnailItem_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: TetherAssetItemViewModel item }) item.ReleaseThumbnail();
    }

    private void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null) return;
        if (e.Key == Key.F11) { viewModel.ToggleFullScreenCommand.Execute(null); e.Handled = true; return; }
        if (e.Key == Key.Escape)
        {
            if (viewModel.IsFullScreen) viewModel.ToggleFullScreenCommand.Execute(null);
            else if (viewModel.CompareMode != Core.Models.TetherCompareMode.None) viewModel.ExitComparisonCommand.Execute(null);
            else if (viewModel.ShowInspectorDrawer) viewModel.ToggleInspectorCommand.Execute(null);
            else return;
            e.Handled = true; return;
        }
        if (e.OriginalSource is TextBox) return;
        switch (e.Key)
        {
            case Key.Left: case Key.Up: viewModel.SelectPrevious(); e.Handled = true; break;
            case Key.Right: case Key.Down: viewModel.SelectNext(); e.Handled = true; break;
            case Key.Enter: AssetList.ScrollIntoView(viewModel.SelectedAsset); PreviewViewport.Focus(); e.Handled = true; break;
            case Key.D0: case Key.NumPad0: SetRating(viewModel, 0, e); break;
            case Key.D1: case Key.NumPad1: SetRating(viewModel, 1, e); break;
            case Key.D2: case Key.NumPad2: SetRating(viewModel, 2, e); break;
            case Key.D3: case Key.NumPad3: SetRating(viewModel, 3, e); break;
            case Key.D4: case Key.NumPad4: SetRating(viewModel, 4, e); break;
            case Key.D5: case Key.NumPad5: SetRating(viewModel, 5, e); break;
            case Key.L:
                if (viewModel.ColorSettings.ToggleLutCommand.CanExecute(null)) viewModel.ColorSettings.ToggleLutCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.K:
                if (viewModel.ToggleLockCommand.CanExecute(null)) viewModel.ToggleLockCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.C:
                System.Windows.Input.ICommand clientCommand = viewModel.ColorSettings.IsClientMonitorOpen
                    ? viewModel.ColorSettings.CloseClientMonitorCommand
                    : viewModel.ColorSettings.OpenClientMonitorCommand;
                if (clientCommand.CanExecute(null)) clientCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.B: viewModel.ColorSettings.ShowBefore = true; e.Handled = true; break;
        }
    }

    private void LutBefore_MouseDown(object sender, MouseButtonEventArgs e) { if (ViewModel is not null) ViewModel.ColorSettings.ShowBefore = true; }
    private void LutBefore_MouseUp(object sender, MouseButtonEventArgs e) { if (ViewModel is not null) ViewModel.ColorSettings.ShowBefore = false; }
    private void LutBefore_MouseLeave(object sender, MouseEventArgs e) { if (ViewModel is not null) ViewModel.ColorSettings.ShowBefore = false; }
    private void View_PreviewKeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.B && ViewModel is not null) { ViewModel.ColorSettings.ShowBefore = false; e.Handled = true; } }

    private static void SetRating(TetherCaptureViewModel viewModel, int rating, KeyEventArgs e)
    {
        if (viewModel.SetRatingCommand.CanExecute(rating.ToString())) viewModel.SetRatingCommand.Execute(rating.ToString());
        e.Handled = true;
    }

    private void PreviewViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ViewModel?.AdjustZoom(e.Delta);
        e.Handled = true;
    }

    private void PreviewViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null || e.ClickCount > 1) return;
        _isDragging = true; _dragStart = e.GetPosition(PreviewViewport); _panStartX = ViewModel.PanX; _panStartY = ViewModel.PanY;
        PreviewViewport.CaptureMouse(); e.Handled = true;
    }

    private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ViewModel?.ToggleFitActual(); e.Handled = true; }
    }

    private void PreviewViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || ViewModel is null || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(PreviewViewport);
        ViewModel.SetPan(_panStartX + current.X - _dragStart.X, _panStartY + current.Y - _dragStart.Y);
    }

    private void PreviewViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDrag();
    private void PreviewViewport_MouseLeave(object sender, MouseEventArgs e) { if (e.LeftButton != MouseButtonState.Pressed) EndDrag(); }
    private void EndDrag() { if (!_isDragging) return; _isDragging = false; PreviewViewport.ReleaseMouseCapture(); ViewModel?.EndCanvasInteraction(); }
    private void Note_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel?.BeginNoteEditing();
    private void Note_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel?.EndNoteEditing();
}
