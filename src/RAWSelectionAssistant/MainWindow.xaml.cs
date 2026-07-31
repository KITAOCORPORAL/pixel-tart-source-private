using System.ComponentModel;
using System.Runtime.InteropServices;
#if UI_REVIEW_BUILD
using System.Text.Json;
#endif
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Services;
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
#if UI_REVIEW_BUILD
    private DispatcherTimer? _uiReviewTimer;
    private string _uiReviewStateContent = string.Empty;
#endif

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
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
        }
        _viewModel = e.NewValue as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.TutorialVisualStateChanged += ViewModel_TutorialVisualStateChanged;
            _viewModel.CloseRequested += ViewModel_CloseRequested;
        }
        ScheduleTutorialLayout();
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
        UpdateWorkbenchResponsiveLayout();
        ScheduleTutorialLayout();
        if (_viewModel?.NeedsUpgradeTutorialOffer == true)
        {
            var offer = new UpgradeTutorialWindow { Owner = this };
            await _viewModel.RespondToUpgradeOfferAsync(offer.ShowDialog() == true);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel?.IsSettingsModalOpen == true)
        {
            _viewModel.IsSettingsModalOpen = false;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && WorkbenchToolboxPopup.IsOpen)
        {
            WorkbenchToolboxPopup.IsOpen = false;
            e.Handled = true;
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

    private void OpenToolboxPage_Click(object sender, RoutedEventArgs e)
    {
        WorkbenchToolboxPopup.IsOpen = false;
        _viewModel?.OpenToolboxPageCommand.Execute(null);
        e.Handled = true;
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
        WorkbenchTaskColumn.Width = compact ? new GridLength(0) : new GridLength(320);
        TaskCenterPanel.Visibility = compact && !_taskCenterDrawerOpen ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumn(TaskCenterPanel, compact ? 0 : 1);
        Grid.SetColumnSpan(TaskCenterPanel, compact ? 2 : 1);
        TaskCenterPanel.Width = compact ? 320 : double.NaN;
        TaskCenterPanel.HorizontalAlignment = compact ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
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

    private void ApplyUiReviewState()
    {
        var path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KitaoPhotoSelector.UiReview",
            "ui-review-state.json");
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
        var dark = string.Equals(root.GetProperty("Theme").GetString(), "Dark", StringComparison.OrdinalIgnoreCase);
        var collapsed = root.GetProperty("SidebarCollapsed").GetBoolean();
        var reviewState = root.GetProperty("State").GetString();
        var outputPath = root.GetProperty("OutputPath").GetString();

        WindowState = WindowState.Normal;
        Width = width;
        Height = height;
        new AppearanceService().Apply(new AppearanceSettings
        {
            Theme = dark ? RAWSelectionAssistant.Core.Models.ThemeMode.Dark : RAWSelectionAssistant.Core.Models.ThemeMode.Light,
            SidebarCollapsed = collapsed
        });
        NativeWindowTheme.Apply(this, dark);
        if (_viewModel is null) return;
        if (_viewModel.IsSidebarCollapsed != collapsed)
        {
            _viewModel.ToggleSidebarCommand.Execute(null);
        }

        _viewModel.IsSettingsModalOpen = false;
        _viewModel.NavigateCommand.Execute("Workbench");
        TaskCenterRuntimeContent.Visibility = Visibility.Visible;
        TaskCenterReviewContent.Visibility = Visibility.Collapsed;
        WorkbenchToolboxPopup.IsOpen = false;

        if (string.Equals(reviewState, "ToolboxFullPage", StringComparison.OrdinalIgnoreCase))
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

        var tab = RecentAllTab;
        if (tab is not null) RecentProjectTab_Click(tab, new RoutedEventArgs());
        RootGrid.UpdateLayout();
        UpdateWorkbenchResponsiveLayout();

        if (string.Equals(reviewState, "ToolboxPopup", StringComparison.OrdinalIgnoreCase))
        {
            WorkbenchToolboxPopup.IsOpen = true;
        }
        if (string.Equals(reviewState, "Feedback", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => _viewModel.FeedbackCommand.Execute(null));
            return;
        }
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => CaptureUiReviewFrame(outputPath));
        }
    }

    private void CaptureUiReviewFrame(string outputPath)
    {
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
