using System.ComponentModel;
using System.Runtime.InteropServices;
#if UI_REVIEW_BUILD
using System.Text.Json;
#endif
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant;

public partial class MainWindow : Window
{
    private bool _hasSavedPosition;
    private readonly TutorialSpotlightLayoutService _spotlightLayoutService = new();
    private bool _tutorialLayoutPending;
    private MainViewModel? _viewModel;
    private TutorialTarget? _lastTutorialTarget;
    private bool _taskCenterDrawerOpen;
    private bool _tetherFullScreen;
    private WindowStyle _tetherPreviousWindowStyle;
    private WindowState _tetherPreviousWindowState;
    private ResizeMode _tetherPreviousResizeMode;
    private Point _quickDragStart;
    private string? _quickDraggedId;
    private Button? _quickInsertionTarget;
    private ShootBookingEditorViewModel? _activeBookingEditor;
    private readonly IModalHost _modalHost = new ModalHost();
#if UI_REVIEW_BUILD
    private DispatcherTimer? _uiReviewTimer;
    private string _uiReviewStateContent = string.Empty;
#endif

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(SurfaceCloseButton.CloseRequestedEvent, new RoutedEventHandler(CloseCurrentSurface_Click));
        Loaded += MainWindow_Loaded;
        Closed += Window_Closed;
        SizeChanged += (_, _) =>
        {
            ScheduleTutorialLayout();
            _viewModel?.UpdateSidebarForWidth(ActualWidth);
            UpdateWorkbenchResponsiveLayout();
        };
        LayoutUpdated += (_, _) => ScheduleTutorialLayout();
        DataContextChanged += MainWindow_DataContextChanged;
    }

    public void ApplySavedBounds(AppSettings settings)
    {
        if (settings.WindowWidth is > 0) Width = Math.Max(MinWidth, settings.WindowWidth.Value);
        if (settings.WindowHeight is > 0) Height = Math.Max(MinHeight, settings.WindowHeight.Value);

        if (settings.WindowLeft is { } left && settings.WindowTop is { } top)
        {
            _hasSavedPosition = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (!_hasSavedPosition)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        var source = PresentationSource.FromVisual(this);
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo) || source?.CompositionTarget is null)
        {
            return;
        }

        var fromDevice = source.CompositionTarget.TransformFromDevice;
        var workTopLeft = fromDevice.Transform(new Point(monitorInfo.WorkArea.Left, monitorInfo.WorkArea.Top));
        var workBottomRight = fromDevice.Transform(new Point(monitorInfo.WorkArea.Right, monitorInfo.WorkArea.Bottom));
        var workWidth = workBottomRight.X - workTopLeft.X;
        var workHeight = workBottomRight.Y - workTopLeft.Y;

        Width = Math.Min(Width, Math.Max(MinWidth, workWidth));
        Height = Math.Min(Height, Math.Max(MinHeight, workHeight));
        Left = Math.Clamp(Left, workTopLeft.X, Math.Max(workTopLeft.X, workBottomRight.X - Width));
        Top = Math.Clamp(Top, workTopLeft.Y, Math.Max(workTopLeft.Y, workBottomRight.Y - Height));
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var paths = e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] : null;
        var text = e.Data.GetDataPresent(DataFormats.UnicodeText) ? e.Data.GetData(DataFormats.UnicodeText) as string : null;
        await viewModel.HandleDropAsync(paths, text);
        e.Handled = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!TryCloseBookingEditorVisual())
        {
            e.Cancel = true;
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CaptureWindowState(ActualWidth, ActualHeight, Left, Top);
        }
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.TutorialVisualStateChanged -= ViewModel_TutorialVisualStateChanged;
            _viewModel.CloseRequested -= ViewModel_CloseRequested;
            _viewModel.PageChanged -= ViewModel_PageChanged;
            _viewModel.WorkCalendarPage.EditorRequested -= ViewModel_EditorRequested;
        }
        _viewModel = e.NewValue as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.TutorialVisualStateChanged += ViewModel_TutorialVisualStateChanged;
            _viewModel.CloseRequested += ViewModel_CloseRequested;
            _viewModel.PageChanged += ViewModel_PageChanged;
            _viewModel.WorkCalendarPage.EditorRequested += ViewModel_EditorRequested;
        }
        ScheduleTutorialLayout();
    }

    private void Window_Closed(object? sender, EventArgs e) => _modalHost.Dispose();

    private void ViewModel_EditorRequested(object? sender, BookingEditorRequestEventArgs e)
    {
        if (!TryCloseBookingEditorVisual()) return;

        _activeBookingEditor = e.Editor;
        _activeBookingEditor.CloseRequested += ActiveBookingEditor_CloseRequested;
        QuickBookingEditorHost.DataContext = e.Editor;
        DrawerBookingEditorHost.DataContext = e.Editor;
        PlanningBookingEditorHost.DataContext = e.Editor;

        switch (e.Presentation)
        {
            case BookingEditorPresentation.QuickEdit:
                BookingEditorDrawerSurface.Visibility = Visibility.Visible;
                DrawerBookingEditorHost.Focus();
                break;
            case BookingEditorPresentation.FullPlanning:
                BookingEditorPlanningSurface.Visibility = Visibility.Visible;
                PlanningBookingEditorHost.Focus();
                break;
            default:
                BookingEditorModalSurface.Visibility = Visibility.Visible;
                QuickBookingEditorHost.Focus();
                break;
        }

        BookingEditorOverlay.Visibility = Visibility.Visible;
    }

    private void ActiveBookingEditor_CloseRequested(object? sender, EventArgs e)
    {
        TryCloseBookingEditorVisual();
    }

    private bool TryCloseBookingEditorVisual()
    {
        if (_activeBookingEditor is not null && !_activeBookingEditor.WasSaved && !_activeBookingEditor.ConfirmDiscardChanges())
            return false;

        CloseBookingEditorVisual();
        return true;
    }

    private void CloseBookingEditorVisual()
    {
        if (_activeBookingEditor is not null)
        {
            _activeBookingEditor.CloseRequested -= ActiveBookingEditor_CloseRequested;
        }
        _activeBookingEditor = null;
        QuickBookingEditorHost.DataContext = null;
        DrawerBookingEditorHost.DataContext = null;
        PlanningBookingEditorHost.DataContext = null;
        BookingEditorModalSurface.Visibility = Visibility.Collapsed;
        BookingEditorPlanningSurface.Visibility = Visibility.Collapsed;
        BookingEditorDrawerSurface.Visibility = Visibility.Collapsed;
        BookingEditorOverlay.Visibility = Visibility.Collapsed;
    }

    private void ViewModel_PageChanged(object? sender, PageChangedEventArgs e)
    {
        WorkbenchToolboxPopup.IsOpen = false;
        QuickToolsOverflowPopup.IsOpen = false;
        Keyboard.ClearFocus();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            if (_viewModel?.CurrentPage == e.CurrentPage) FocusActivePage();
        });
    }

    private void FocusActivePage()
    {
        var automationName = _viewModel?.CurrentPage switch
        {
            "WorkCalendar" => "工作日历",
            "Tether" => "联机拍摄现场监看工作区",
            _ => string.Empty
        };
        var page = string.IsNullOrEmpty(automationName) ? null : FindVisualChildren<FrameworkElement>(RootGrid)
            .FirstOrDefault(element => element.Visibility == Visibility.Visible &&
                string.Equals(AutomationProperties.GetName(element), automationName, StringComparison.Ordinal));
        (page ?? this).Focus();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var darkTheme = Application.Current.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.Contains("Theme.Dark", StringComparison.OrdinalIgnoreCase) == true);
        NativeWindowTheme.Apply(this, darkTheme);
#if UI_REVIEW_BUILD
        StartUiReviewController();
#endif
        _viewModel?.UpdateSidebarForWidth(ActualWidth);
        _viewModel?.SetQuickToolsCompact(ActualWidth < 1180);
        UpdateWorkbenchResponsiveLayout();
        ScheduleTutorialLayout();
        if (_viewModel?.NeedsUpgradeTutorialOffer == true)
        {
            var offer = new UpgradeTutorialWindow { Owner = this };
            offer.ShowDialog();
            await _viewModel.RespondToUpgradeOfferAsync(offer.Accepted);
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.IsTetherPage == true && e.Key == Key.F11 && _viewModel.TetherPage?.ToggleFullScreenCommand.CanExecute(null) == true)
        {
            _viewModel.TetherPage.ToggleFullScreenCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (_tetherFullScreen && e.Key == Key.Escape && _viewModel?.TetherPage?.ToggleFullScreenCommand.CanExecute(null) == true)
        {
            _viewModel.TetherPage.ToggleFullScreenCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (TryCloseActiveInputPopup())
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            await RequestEscapeCloseAsync();
            return;
        }

        if (_viewModel?.IsOnboardingActive == true && e.Key == Key.Tab)
        {
            var target = ResolveTutorialTarget(_viewModel.TutorialTarget);
            if (target is not null) target.Focus();
            else TutorialPrimaryButton.Focus();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.B)
        {
            if (_viewModel?.ToggleSidebarCommand.CanExecute(null) == true)
            {
                _viewModel.ToggleSidebarCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.F)
        {
            if (_viewModel?.FeedbackCommand.CanExecute(null) == true)
            {
                _viewModel.FeedbackCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left)
        {
            e.Handled = true;
            await RequestEscapeCloseAsync();
            return;
        }
    }

    private bool TryCloseActiveInputPopup()
    {
        var openCombo = FindVisualChildren<ComboBox>(RootGrid).FirstOrDefault(combo => combo.IsDropDownOpen);
        if (openCombo is not null)
        {
            openCombo.IsDropDownOpen = false;
            openCombo.Focus();
            return true;
        }

        var openDatePicker = FindVisualChildren<DatePicker>(RootGrid).FirstOrDefault(picker => picker.IsDropDownOpen);
        if (openDatePicker is null) return false;
        openDatePicker.IsDropDownOpen = false;
        openDatePicker.Focus();
        return true;
    }

    private async Task RequestEscapeCloseAsync()
    {
        if (_viewModel is null) return;

        if (_viewModel.IsOnboardingActive)
        {
            await _viewModel.CloseCurrentSurfaceAsync();
            return;
        }

        if (_viewModel.IsSettingsModalOpen)
        {
            await RequestModalActionAsync(
                closeAsync: () => { _viewModel.IsSettingsModalOpen = false; return Task.CompletedTask; },
                cancelAsync: () => { _viewModel.IsSettingsModalOpen = false; return Task.CompletedTask; });
            return;
        }

        if (BookingEditorOverlay.Visibility == Visibility.Visible)
        {
            TryCloseBookingEditorVisual();
            return;
        }

        if (_viewModel.OnlineSelectionPage.IsCreateModalOpen)
        {
            _viewModel.OnlineSelectionPage.CloseCreateSurface();
            return;
        }

        if (_viewModel.FinancePage?.IsEditorOpen == true)
        {
            _viewModel.FinancePage.CloseEditorSurface();
            return;
        }

        if (_viewModel.TaskCenter.IsTaskDetailsOpen)
        {
            _viewModel.TaskCenter.CloseDetailsSurface();
            return;
        }

        if (_viewModel.WorkCalendarPage.IsDetailsOpen)
        {
            _viewModel.WorkCalendarPage.CloseDetailsSurface();
            return;
        }

        if (WorkbenchToolboxPopup.IsOpen)
        {
            await RequestModalActionAsync(
                closeAsync: () => { WorkbenchToolboxPopup.IsOpen = false; return Task.CompletedTask; },
                cancelAsync: () => { WorkbenchToolboxPopup.IsOpen = false; return Task.CompletedTask; });
            return;
        }

        if (QuickToolsOverflowPopup.IsOpen)
        {
            await RequestModalActionAsync(
                closeAsync: () => { QuickToolsOverflowPopup.IsOpen = false; return Task.CompletedTask; },
                cancelAsync: () => { QuickToolsOverflowPopup.IsOpen = false; return Task.CompletedTask; });
            return;
        }

        await _viewModel.CloseCurrentSurfaceAsync();
    }

    private async void CloseCurrentSurface_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await RequestEscapeCloseAsync();
    }

    private async Task RequestModalActionAsync(Func<Task> closeAsync, Func<Task> cancelAsync)
    {
        using var session = new ModalSession(closeAsync: closeAsync, cancelAsync: cancelAsync);
        _modalHost.Show(session);
        await _modalHost.RequestCancelAsync();
    }

    private void TetherCaptureView_FullScreenChanged(object? sender, TetherFullScreenChangedEventArgs e)
    {
        if (e.IsFullScreen == _tetherFullScreen) return;
        if (e.IsFullScreen)
        {
            _tetherPreviousWindowStyle = WindowStyle;
            _tetherPreviousWindowState = WindowState;
            _tetherPreviousResizeMode = ResizeMode;
            _tetherFullScreen = true;
            TopMenu.Visibility = Visibility.Collapsed;
            SidebarContainer.Visibility = Visibility.Collapsed;
            BottomStatusBar.Visibility = Visibility.Collapsed;
            TaskCenterPanel.Visibility = Visibility.Collapsed;
            RootGrid.RowDefinitions[0].Height = new GridLength(0);
            RootGrid.RowDefinitions[2].Height = new GridLength(0);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            _tetherFullScreen = false;
            WindowStyle = _tetherPreviousWindowStyle;
            ResizeMode = _tetherPreviousResizeMode;
            WindowState = _tetherPreviousWindowState;
            TopMenu.Visibility = Visibility.Visible;
            SidebarContainer.Visibility = Visibility.Visible;
            BottomStatusBar.Visibility = Visibility.Visible;
            RootGrid.RowDefinitions[0].Height = new GridLength(36);
            RootGrid.RowDefinitions[2].Height = new GridLength(34);
            UpdateWorkbenchResponsiveLayout();
        }
    }

    private void ToolboxQuickButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.IsWorkbenchPage != true)
        {
            _viewModel?.NavigateCommand.Execute("Workbench");
            Dispatcher.BeginInvoke(() => WorkbenchToolboxPopup.IsOpen = true, DispatcherPriority.Loaded);
            e.Handled = true;
            return;
        }
        WorkbenchToolboxPopup.IsOpen = !WorkbenchToolboxPopup.IsOpen;
        e.Handled = true;
    }

    private void WorkbenchToolboxPopup_Closed(object? sender, EventArgs e) => ToolboxQuickButton.Focus();

    private void LocalSplitHelpButton_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        LocalSplitHelpToolTip.IsOpen = true;
    }

    private void LocalSplitHelpButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        LocalSplitHelpToolTip.IsOpen = false;
    }

    private void LocalSplitHelpButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        LocalSplitHelpToolTip.IsOpen = false;
        e.Handled = true;
    }

    private void OpenToolboxPage_Click(object sender, RoutedEventArgs e)
    {
        WorkbenchToolboxPopup.IsOpen = false;
        _viewModel?.OpenToolboxPageCommand.Execute(null);
        e.Handled = true;
    }

    private void ToolboxItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string targetPage }) return;
        WorkbenchToolboxPopup.IsOpen = false;
        Keyboard.ClearFocus();
        _viewModel?.NavigateCommand.Execute(targetPage);
        e.Handled = true;
    }

    private void QuickToolsOverflowButton_Click(object sender, RoutedEventArgs e)
    {
        QuickToolsOverflowPopup.IsOpen = !QuickToolsOverflowPopup.IsOpen;
        e.Handled = true;
    }

    private void PinnedQuickTools_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _quickDragStart = e.GetPosition(PinnedQuickToolsList);
        _quickDraggedId = FindAncestor<Button>(e.OriginalSource as DependencyObject)?.DataContext is ToolboxItemViewModel item ? item.Id : null;
    }

    private void PinnedQuickTools_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || string.IsNullOrWhiteSpace(_quickDraggedId)) return;
        var point = e.GetPosition(PinnedQuickToolsList);
        if (Math.Abs(point.X - _quickDragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - _quickDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop(PinnedQuickToolsList, _quickDraggedId, DragDropEffects.Move);
        ClearQuickInsertionIndicator();
        _quickDraggedId = null;
    }

    private void PinnedQuickTools_DragOver(object sender, DragEventArgs e)
    {
        var target = FindAncestor<Button>(e.OriginalSource as DependencyObject);
        if (target?.DataContext is ToolboxItemViewModel)
        {
            if (!ReferenceEquals(target, _quickInsertionTarget))
            {
                ClearQuickInsertionIndicator();
                _quickInsertionTarget = target;
                target.BorderBrush = FindResource("AccentBrush") as Brush;
                target.BorderThickness = new Thickness(2);
            }
            e.Effects = DragDropEffects.Move;
        }
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void PinnedQuickTools_Drop(object sender, DragEventArgs e)
    {
        var sourceId = e.Data.GetData(typeof(string)) as string;
        var targetId = FindAncestor<Button>(e.OriginalSource as DependencyObject)?.DataContext is ToolboxItemViewModel target ? target.Id : null;
        _viewModel?.MovePinnedToolTo(sourceId, targetId);
        ClearQuickInsertionIndicator();
        e.Handled = true;
    }

    private void PinnedQuickTools_KeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == 0 || FindAncestor<Button>(e.OriginalSource as DependencyObject)?.DataContext is not ToolboxItemViewModel item) return;
        if (e.Key == Key.Left) { _viewModel?.MovePinnedTool(item.Id, -1); e.Handled = true; }
        else if (e.Key == Key.Right) { _viewModel?.MovePinnedTool(item.Id, 1); e.Handled = true; }
    }

    private void MovePinnedToolLeft_Click(object sender, RoutedEventArgs e) => _viewModel?.MovePinnedTool((sender as FrameworkElement)?.Tag?.ToString(), -1);
    private void MovePinnedToolRight_Click(object sender, RoutedEventArgs e) => _viewModel?.MovePinnedTool((sender as FrameworkElement)?.Tag?.ToString(), 1);
    private void RemovePinnedTool_Click(object sender, RoutedEventArgs e) => _viewModel?.RemovePinnedToolCommand.Execute((sender as FrameworkElement)?.Tag?.ToString());
    private void ManageQuickTools_Click(object sender, RoutedEventArgs e) => _viewModel?.ManageQuickToolsCommand.Execute(null);

    private void ClearQuickInsertionIndicator()
    {
        if (_quickInsertionTarget is null) return;
        _quickInsertionTarget.ClearValue(Border.BorderBrushProperty);
        _quickInsertionTarget.ClearValue(Border.BorderThicknessProperty);
        _quickInsertionTarget = null;
    }

    private static T? FindAncestor<T>(DependencyObject? value) where T : DependencyObject
    {
        while (value is not null)
        {
            if (value is T found) return found;
            value = value is Visual ? VisualTreeHelper.GetParent(value) : LogicalTreeHelper.GetParent(value);
        }
        return null;
    }

    private void TaskDrawerButton_Click(object sender, RoutedEventArgs e)
    {
        _taskCenterDrawerOpen = !_taskCenterDrawerOpen;
        UpdateWorkbenchResponsiveLayout();
    }

    private void RecentProjectTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button selected) return;
        foreach (var button in FindVisualChildren<Button>(RecentProjectsArea).Where(button => button.Tag is not null))
        {
            button.BorderThickness = new Thickness(0);
            button.FontWeight = FontWeights.Normal;
        }
        selected.BorderBrush = FindResource("AccentBrush") as Brush;
        selected.BorderThickness = new Thickness(0, 0, 0, 2);
        selected.FontWeight = FontWeights.SemiBold;
        var completed = string.Equals(selected.Tag?.ToString(), "Completed", StringComparison.Ordinal);
        RecentProjectsScroll.Visibility = completed ? Visibility.Collapsed : Visibility.Visible;
        RecentProjectsEmptyState.Visibility = !completed && _viewModel?.ProjectHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CompletedProjectsEmptyState.Visibility = completed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWorkbenchResponsiveLayout()
    {
        if (!IsLoaded) return;
        var compact = ActualWidth < 1350;
        var quickOverflow = ActualWidth < 1180;
        var shortWorkbench = ActualHeight < 820;
        var veryShortWorkbench = ActualHeight < 760;
        var taskCenterWidth = ActualWidth >= 1920 ? 360d : 320d;
        _viewModel?.SetQuickToolsCompact(quickOverflow);
        QuickToolsOverflowButton.Visibility = quickOverflow ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(PinnedQuickToolsList, quickOverflow ? 3 : 4);
        WorkbenchTaskColumn.Width = compact ? new GridLength(0) : new GridLength(taskCenterWidth);
        TaskCenterPanel.Visibility = compact && !_taskCenterDrawerOpen ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumn(TaskCenterPanel, compact ? 0 : 1);
        Grid.SetColumnSpan(TaskCenterPanel, compact ? 2 : 1);
        TaskCenterPanel.Width = compact ? 300 : double.NaN;
        TaskCenterPanel.HorizontalAlignment = compact ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        if (WorkbenchOverviewRow is not null)
        {
            WorkbenchOverviewRow.Height = new GridLength(veryShortWorkbench ? 170 : shortWorkbench ? 190 : 230);
            WorkbenchScheduleRow.Height = new GridLength(veryShortWorkbench ? 115 : shortWorkbench ? 140 : 170);
        }
        TaskDrawerButton.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        WorkbenchQuickActions.Margin = compact ? new Thickness(0, 0, 116, 0) : new Thickness(0);
        TaskDrawerButton.Content = _taskCenterDrawerOpen ? "收起任务中心" : "任务中心";
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dependencyObject) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(dependencyObject); index++)
        {
            var child = VisualTreeHelper.GetChild(dependencyObject, index);
            if (child is T result) yield return result;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

#if UI_REVIEW_BUILD
    private void StartUiReviewController()
    {
        _uiReviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _uiReviewTimer.Tick += (_, _) => ApplyUiReviewState();
        _uiReviewTimer.Start();
        ApplyUiReviewState();
    }

    private async void ApplyUiReviewState()
    {
        var path = System.IO.Path.Combine(AppDataPaths.Root, "ui-review-state.json");
        if (!System.IO.File.Exists(path)) return;

        string content;
        try
        {
            content = System.IO.File.ReadAllText(path);
        }
        catch (IOException)
        {
            return;
        }
        if (string.Equals(content, _uiReviewStateContent, StringComparison.Ordinal)) return;
        _uiReviewStateContent = content;

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var width = root.GetProperty("Width").GetDouble();
        var height = root.GetProperty("Height").GetDouble();
        var themeName = root.GetProperty("Theme").GetString() ?? "Dark";
        var dark = string.Equals(themeName, "Dark", StringComparison.OrdinalIgnoreCase);
        var highContrast = string.Equals(themeName, "HighContrast", StringComparison.OrdinalIgnoreCase);
        var collapsed = root.GetProperty("SidebarCollapsed").GetBoolean();
        var reviewState = root.GetProperty("State").GetString();
        var outputPath = root.GetProperty("OutputPath").GetString();
        ConfigureAutomatedDpiAcceptance(root);

        WindowState = WindowState.Normal;
        Width = width;
        Height = height;
        new AppearanceService().Apply(new AppearanceSettings
        {
            Theme = dark ? RAWSelectionAssistant.Core.Models.ThemeMode.Dark : RAWSelectionAssistant.Core.Models.ThemeMode.Light,
            SidebarCollapsed = collapsed
        });
        if (highContrast)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var current = dictionaries.FirstOrDefault(dictionary =>
                dictionary.Source?.OriginalString.Contains("DesignSystem/Theme.", StringComparison.OrdinalIgnoreCase) == true);
            var replacement = new ResourceDictionary { Source = new Uri("Resources/DesignSystem/Theme.HighContrast.xaml", UriKind.Relative) };
            if (current is null) dictionaries.Insert(0, replacement);
            else dictionaries[dictionaries.IndexOf(current)] = replacement;
        }
        NativeWindowTheme.Apply(this, dark && !highContrast);
        if (_viewModel is null) return;
        if (_viewModel.IsSidebarCollapsed != collapsed)
        {
            _viewModel.ToggleSidebarCommand.Execute(null);
            _viewModel.DismissToastCommand.Execute(null);
        }

        _viewModel.IsSettingsModalOpen = false;
        _viewModel.NavigateCommand.Execute("Workbench");
        RecentAllTab.Content = "最近项目";
        TaskCenterRuntimeContent.Visibility = Visibility.Visible;
        TaskCenterReviewContent.Visibility = Visibility.Collapsed;
        WorkbenchToolboxPopup.IsOpen = false;

        var automatedStateHandled = await PrepareAutomatedDpiAcceptanceStateAsync(reviewState);

        if (!automatedStateHandled && string.Equals(reviewState, "ToolboxFullPage", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.NavigateCommand.Execute("Toolbox");
        }
        else if (string.Equals(reviewState, "Settings", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.NavigateCommand.Execute("Settings");
        }
        else if (string.Equals(reviewState, "TaskCenterWithTasks", StringComparison.OrdinalIgnoreCase))
        {
            TaskCenterRuntimeContent.Visibility = Visibility.Collapsed;
            TaskCenterReviewContent.Visibility = Visibility.Visible;
        }
        else if (string.Equals(reviewState, "RecentProjects", StringComparison.OrdinalIgnoreCase))
        {
            RecentAllTab.Content = "最近项目 · 演示数据";
        }
        else if (string.Equals(reviewState, "OrganizePhotos", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.NavigateCommand.Execute("PhotoGrouping");
            var demoDirectory = System.IO.Path.Combine(AppDataPaths.Root, "DemoImages");
            await _viewModel.OrganizePhotosPage.AddPathsAsync(System.IO.Directory.Exists(demoDirectory) ? System.IO.Directory.GetFiles(demoDirectory, "*.png") : []);
        }
        else if (string.Equals(reviewState, "Collage", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.NavigateCommand.Execute("Collage");
            var demoDirectory = System.IO.Path.Combine(AppDataPaths.Root, "DemoImages");
            _viewModel.CollagePage.AddPaths(System.IO.Directory.Exists(demoDirectory) ? System.IO.Directory.GetFiles(demoDirectory, "*.png") : []);
            _viewModel.CollagePage.SelectedTemplate = CollageTemplateCatalog.Get("4-grid");
        }
        if (string.Equals(reviewState, "TetherTaskCenter", StringComparison.OrdinalIgnoreCase))
        {
            TaskCenterRuntimeContent.Visibility = Visibility.Collapsed;
            TaskCenterReviewContent.Visibility = Visibility.Visible;
        }
        if (reviewState?.StartsWith("WorkbenchTaskCenter", StringComparison.OrdinalIgnoreCase) == true)
            ApplyTaskCenterReviewState(reviewState);

        var tab = RecentAllTab;
        if (string.Equals(reviewState, "CompletedProjectsEmpty", StringComparison.OrdinalIgnoreCase))
        {
            tab = FindVisualChildren<Button>(RecentProjectsArea).FirstOrDefault(button => string.Equals(button.Tag?.ToString(), "Completed", StringComparison.Ordinal));
        }
        if (tab is not null) RecentProjectTab_Click(tab, new RoutedEventArgs());
        RootGrid.UpdateLayout();
        UpdateWorkbenchResponsiveLayout();
        if (IsTetherColorReviewState(reviewState))
            TetherMonitorView.ApplyReviewPresentation(reviewState!, width);
        FinalizeAutomatedDpiAcceptanceState(reviewState);

        if (string.Equals(reviewState, "ToolboxPopup", StringComparison.OrdinalIgnoreCase))
        {
            WorkbenchToolboxPopup.IsOpen = true;
        }
        if (string.Equals(reviewState, "QuickToolsOverflow", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.SetQuickToolsCompact(true);
            QuickToolsOverflowButton.Visibility = Visibility.Visible;
            Grid.SetColumnSpan(PinnedQuickToolsList, 3);
            QuickToolsOverflowPopup.IsOpen = true;
        }
        if (string.Equals(reviewState, "Feedback", StringComparison.OrdinalIgnoreCase))
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => _viewModel.FeedbackCommand.Execute(null));
            return;
        }
        if (!string.IsNullOrWhiteSpace(outputPath) && !string.Equals(outputPath, "KEEP_OPEN", StringComparison.OrdinalIgnoreCase))
        {
            var captureDelay = IsTetherColorReviewState(reviewState) ? 2200 : 550;
            var captureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(captureDelay) };
            captureTimer.Tick += (_, _) =>
            {
                captureTimer.Stop();
                if (IsTetherColorReviewState(reviewState))
                    TetherMonitorView.ApplyReviewPresentation(reviewState!, width);
                CaptureUiReviewFrame(outputPath);
            };
            captureTimer.Start();
        }
    }

    private static bool IsTetherColorReviewState(string? state) => state?.StartsWith("Tether", StringComparison.OrdinalIgnoreCase) == true || state?.StartsWith("Lut", StringComparison.OrdinalIgnoreCase) == true || state?.StartsWith("ColorProfile", StringComparison.OrdinalIgnoreCase) == true || state?.StartsWith("ClientMonitor", StringComparison.OrdinalIgnoreCase) == true || state == "MixedDpi";

    private void ApplyTaskCenterReviewState(string state)
    {
        if (string.Equals(state, "WorkbenchTaskCenterEmpty", StringComparison.OrdinalIgnoreCase)) return;
        var count = state.Contains("20Tasks", StringComparison.OrdinalIgnoreCase) || state.Contains("Scrolled", StringComparison.OrdinalIgnoreCase) ? 20 : 5;
        TaskCenterRuntimeContent.Visibility = Visibility.Collapsed;
        TaskCenterReviewContent.Visibility = Visibility.Visible;
        TaskCenterReviewList.ItemsSource = Enumerable.Range(1, count).Select(index => new TaskCenterReviewItem(
            $"界面验收任务 {index:00}", (index % 4) switch { 0 => "来源：联机拍摄", 1 => "来源：文件复制", 2 => "来源：批量压缩", _ => "来源：归片工作区" },
            index % 5 == 0 ? "等待确认" : "处理中", Math.Min(96, 12 + index * 4), $"更新 08-07 {14 + index / 6:00}:{index * 3 % 60:00}" )).ToArray();
        if (state.Contains("Scrolled", StringComparison.OrdinalIgnoreCase))
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => TaskCenterReviewContent.ScrollToVerticalOffset(520));
    }

    private sealed record TaskCenterReviewItem(string DisplayName, string Source, string StateLabel, double Progress, string UpdatedAt);

    private void CaptureUiReviewFrame(string outputPath)
    {
        if (_automatedDpiAcceptanceEnabled)
        {
            CaptureAutomatedDpiFrame(outputPath);
            return;
        }
        RootGrid.UpdateLayout();
        if (RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0) return;

        var dpi = VisualTreeHelper.GetDpi(RootGrid);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(RootGrid.ActualWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(RootGrid.ActualHeight * dpi.DpiScaleY));
        var contentBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        contentBitmap.Render(RootGrid);

        var composition = new DrawingVisual();
        using (var drawing = composition.RenderOpen())
        {
            drawing.DrawImage(contentBitmap, new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));
            if (WorkbenchToolboxPopup.IsOpen && WorkbenchToolboxPopup.Child is FrameworkElement popupChild && popupChild.ActualWidth > 0)
            {
                popupChild.UpdateLayout();
                var popupWidth = Math.Max(1, (int)Math.Ceiling(popupChild.ActualWidth * dpi.DpiScaleX));
                var popupHeight = Math.Max(1, (int)Math.Ceiling(popupChild.ActualHeight * dpi.DpiScaleY));
                var popupBitmap = new RenderTargetBitmap(popupWidth, popupHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
                popupBitmap.Render(popupChild);
                var screenPoint = popupChild.PointToScreen(new Point(0, 0));
                var rootPoint = RootGrid.PointFromScreen(screenPoint);
                drawing.DrawImage(popupBitmap, new Rect(rootPoint.X, rootPoint.Y, popupChild.ActualWidth, popupChild.ActualHeight));
            }
            if (QuickToolsOverflowPopup.IsOpen && QuickToolsOverflowPopup.Child is FrameworkElement overflowChild && overflowChild.ActualWidth > 0)
            {
                overflowChild.UpdateLayout();
                var popupWidth = Math.Max(1, (int)Math.Ceiling(overflowChild.ActualWidth * dpi.DpiScaleX));
                var popupHeight = Math.Max(1, (int)Math.Ceiling(overflowChild.ActualHeight * dpi.DpiScaleY));
                var popupBitmap = new RenderTargetBitmap(popupWidth, popupHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
                popupBitmap.Render(overflowChild);
                var screenPoint = overflowChild.PointToScreen(new Point(0, 0));
                var rootPoint = RootGrid.PointFromScreen(screenPoint);
                drawing.DrawImage(popupBitmap, new Rect(rootPoint.X, rootPoint.Y, overflowChild.ActualWidth, overflowChild.ActualHeight));
            }
        }

        var finalBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        finalBitmap.Render(composition);
        var directory = System.IO.Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) System.IO.Directory.CreateDirectory(directory);
        var temporaryPath = outputPath + ".tmp";
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(finalBitmap));
        using (var stream = System.IO.File.Create(temporaryPath)) encoder.Save(stream);
        System.IO.File.Move(temporaryPath, outputPath, true);
    }
#endif

    private void ViewModel_TutorialVisualStateChanged(object? sender, EventArgs e) => ScheduleTutorialLayout();
    private void ViewModel_CloseRequested(object? sender, EventArgs e) => Close();

    private void ScheduleTutorialLayout()
    {
        if (_tutorialLayoutPending) return;
        _tutorialLayoutPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _tutorialLayoutPending = false;
            UpdateTutorialLayout();
        });
    }

    private void UpdateTutorialLayout()
    {
        if (_viewModel?.IsOnboardingActive != true || RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
        {
            TutorialOverlay.Visibility = Visibility.Collapsed;
            _lastTutorialTarget = null;
            return;
        }

        TutorialOverlay.Visibility = Visibility.Visible;
        TutorialOverlay.Width = RootGrid.ActualWidth;
        TutorialOverlay.Height = RootGrid.ActualHeight;
        var tutorialTarget = _viewModel.TutorialTarget;
        var targetChanged = _lastTutorialTarget != tutorialTarget;
        if (targetChanged)
        {
            _lastTutorialTarget = tutorialTarget;
            PrepareTutorialTarget(tutorialTarget);
        }

        var target = ResolveTutorialTarget(tutorialTarget);
        if (target is null || !target.IsVisible || _viewModel.TutorialTarget is TutorialTarget.Welcome or TutorialTarget.Completed)
        {
            SetMask(TutorialMaskTop, 0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight);
            SetMask(TutorialMaskLeft, 0, 0, 0, 0);
            SetMask(TutorialMaskRight, 0, 0, 0, 0);
            SetMask(TutorialMaskBottom, 0, 0, 0, 0);
            TutorialHighlight.Visibility = Visibility.Collapsed;
            TutorialPointer.Visibility = Visibility.Collapsed;
            Canvas.SetLeft(TutorialCard, Math.Max(16, (RootGrid.ActualWidth - TutorialCard.Width) / 2));
            Canvas.SetTop(TutorialCard, Math.Max(16, (RootGrid.ActualHeight - 280) / 2));
            TutorialPrimaryButton.Focus();
            return;
        }

        if (targetChanged) target.BringIntoView();

        var point = target.TransformToAncestor(RootGrid).Transform(new Point(0, 0));
        var layout = _spotlightLayoutService.Calculate(
            RootGrid.ActualWidth,
            RootGrid.ActualHeight,
            point.X,
            point.Y,
            target.ActualWidth,
            target.ActualHeight,
            TutorialCard.Width,
            TutorialCard.ActualHeight > 0 ? TutorialCard.ActualHeight : 300);
        SetMask(TutorialMaskTop, 0, 0, RootGrid.ActualWidth, layout.TargetTop);
        SetMask(TutorialMaskLeft, 0, layout.TargetTop, layout.TargetLeft, layout.TargetHeight);
        SetMask(TutorialMaskRight, layout.TargetLeft + layout.TargetWidth, layout.TargetTop, Math.Max(0, RootGrid.ActualWidth - layout.TargetLeft - layout.TargetWidth), layout.TargetHeight);
        SetMask(TutorialMaskBottom, 0, layout.TargetTop + layout.TargetHeight, RootGrid.ActualWidth, Math.Max(0, RootGrid.ActualHeight - layout.TargetTop - layout.TargetHeight));
        TutorialHighlight.Visibility = Visibility.Visible;
        TutorialHighlight.Width = layout.TargetWidth;
        TutorialHighlight.Height = layout.TargetHeight;
        Canvas.SetLeft(TutorialHighlight, layout.TargetLeft);
        Canvas.SetTop(TutorialHighlight, layout.TargetTop);
        Canvas.SetLeft(TutorialCard, layout.CardLeft);
        Canvas.SetTop(TutorialCard, layout.CardTop);
        TutorialPointer.Visibility = Visibility.Visible;
        var cardIsRight = layout.CardLeft > layout.TargetLeft;
        TutorialPointer.X1 = cardIsRight ? layout.CardLeft : layout.CardLeft + TutorialCard.Width;
        TutorialPointer.Y1 = layout.CardTop + 54;
        TutorialPointer.X2 = cardIsRight ? layout.TargetLeft + layout.TargetWidth : layout.TargetLeft;
        TutorialPointer.Y2 = layout.TargetTop + layout.TargetHeight / 2;
        target.Focus();
    }

    private FrameworkElement? ResolveTutorialTarget(TutorialTarget target) => target switch
    {
        TutorialTarget.AddSourceButton => AddSourceButton,
        TutorialTarget.RemoveSourceButton => RemoveSourceButton,
        TutorialTarget.CollectionCategorySelector => CategorySelector,
        TutorialTarget.ScanButton => ScanButton,
        TutorialTarget.CancelButton => CancelButton,
        TutorialTarget.CustomerDropArea => CustomerDropArea,
        TutorialTarget.PasteButton => PasteButton,
        TutorialTarget.ParseButton => ParseButton,
        TutorialTarget.ClearSelectionsButton => ClearSelectionsButton,
        TutorialTarget.MatchButton => MatchButton,
        TutorialTarget.ResultsGrid => ResultsGrid,
        TutorialTarget.FirstDetailsButton => ResolveFirstDetailsButton(),
        TutorialTarget.JpegQualityArea => JpegQualityArea,
        TutorialTarget.BrowseOutputButton => BrowseOutputButton,
        TutorialTarget.ProjectNameInput => ProjectNameInput,
        TutorialTarget.OutputModeSelector => OutputModeSelector,
        TutorialTarget.CopyButton => CopyButton,
        TutorialTarget.ExportButton => ExportButton,
        TutorialTarget.OpenOutputButton => OpenOutputButton,
        TutorialTarget.ClearTaskButton => ClearTaskButton,
        TutorialTarget.EditionStatusArea => EditionStatusArea,
        _ => null
    };

    private void PrepareTutorialTarget(TutorialTarget target)
    {
        if (target != TutorialTarget.FirstDetailsButton || ResultsGrid.Items.Count == 0) return;
        ResultsGrid.ScrollIntoView(ResultsGrid.Items[0], DetailsColumn);
        ResultsGrid.UpdateLayout();
    }

    private FrameworkElement ResolveFirstDetailsButton()
    {
        if (ResultsGrid.Items.Count == 0) return ResultsGrid;
        var content = DetailsColumn.GetCellContent(ResultsGrid.Items[0]);
        return FindVisualChild<Button>(content) ?? content ?? ResultsGrid;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null) return null;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            if (FindVisualChild<T>(child) is { } descendant) return descendant;
        }
        return null;
    }

    private static void SetMask(Shape shape, double left, double top, double width, double height)
    {
        shape.Width = Math.Max(0, width);
        shape.Height = Math.Max(0, height);
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
    }
}
