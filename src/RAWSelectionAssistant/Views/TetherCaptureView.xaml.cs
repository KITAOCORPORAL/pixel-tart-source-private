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
    private Core.Models.Photography.CompareSide _dragCompareSide;
    private bool _isBrowserCollapsed;
    private readonly Services.TetherWorkspacePreferenceStore _workspacePreferences;
    private bool _preferencesLoaded;
    private Task? _preferenceLoad;
    public TetherInspectorPreferenceState WorkspacePreferenceState {get;}=new();
    public string? WorkspacePreferenceError {get;private set;}
    public Exception? WorkspacePreferenceException {get;private set;}
    internal Task PreferenceLifecycleTask {get;private set;}=Task.CompletedTask;
    private ShotReferenceWindow? _shotReferenceWindow;

    public TetherCaptureView() : this(new Services.TetherWorkspacePreferenceStore()) {}
    internal TetherCaptureView(Services.TetherWorkspacePreferenceStore workspacePreferences)
    {
        _workspacePreferences=workspacePreferences;
        InitializeComponent();
        DataContextChanged += View_DataContextChanged;
        Loaded += (_, _) => PreferenceLifecycleTask=ObservePreferenceLifecycleAsync(async()=>{await LoadWorkspacePreferencesAsync();UpdateResponsiveLayout();});
        Unloaded += (_, _) => PreferenceLifecycleTask=ObservePreferenceLifecycleAsync(SaveWorkspacePreferencesAsync);
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

    internal Task LoadWorkspacePreferencesAsync() => _preferenceLoad ??= ReadWorkspacePreferencesAsync();

    private async Task ReadWorkspacePreferencesAsync()
    {
        var value=await _workspacePreferences.LoadAsync();
        // Reading does not require selected content, visibility or DataTemplate generation.
        WorkspacePreferenceState.Restore(value);
        InspectorColumn.Width=new GridLength(value.InspectorWidth);
        _preferencesLoaded=true;
    }

    internal async Task SaveWorkspacePreferencesAsync()
    {
        if(_preferenceLoad is not null)await _preferenceLoad;
        if(!_preferencesLoaded)return;
        // Compact layout can set ActualWidth=0. Preserve the last valid logical width.
        var width=InspectorColumn.ActualWidth is >=320 and <=480 ? InspectorColumn.ActualWidth : InspectorColumn.Width.Value;
        if(!double.IsFinite(width)||width is <320 or >480)width=WorkspacePreferenceState.InspectorWidth;
        await _workspacePreferences.SaveAsync(WorkspacePreferenceState.Snapshot(width));
    }

    private async Task ObservePreferenceLifecycleAsync(Func<Task> operation)
    {
        try{await operation();WorkspacePreferenceError=null;WorkspacePreferenceException=null;}
        catch(Exception error)when(error is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            WorkspacePreferenceError="检查器偏好读写失败；当前展开状态保留。";
            WorkspacePreferenceException=error;
            System.Diagnostics.Trace.TraceError("Tether workspace preference lifecycle: {0}",error);
        }
    }


    public void ApplyReviewPresentation(string state, double windowWidth)
    {
        _isBrowserCollapsed = false;
        ApplyResponsiveLayout(windowWidth);
        if (ViewModel is not null)
            ViewModel.ShowInspectorDrawer = string.Equals(state, "TetherCompact1280", StringComparison.Ordinal);
        SetReviewGroup("相机 / 拍摄会话", true);
        SetReviewGroup("曝光评估", state is not "TetherViewer");
        SetReviewGroup("参考模式", state.Contains("Reference", StringComparison.Ordinal));
        SetReviewGroup("拍摄参考", state.Contains("Shot", StringComparison.Ordinal));
        SetReviewGroup("拍摄信息", state is "TetherInspector" or "TetherExifExpanded");
        SetReviewGroup("标记与备注", state is not "TetherViewer");
        SetReviewGroup("LUT 与色彩", state is "TetherLutExpanded");
        SetReviewGroup("客户监看", state is "TetherClientSingleMonitor");
        SetReviewGroup("文件处理", state is "TetherFileHandling");
        if (state == "TetherInspector")
        {
            SetReviewGroup("LUT 与色彩", true);
            SetReviewGroup("客户监看", true);
        }
        InspectorPanel.UpdateLayout();
        var scroll = FindVisualChildren<ScrollViewer>(InspectorPanel)
            .OrderByDescending(element => element.ScrollableHeight)
            .FirstOrDefault();
        var offset = state switch
        {
            "TetherAnnotations" => 650,
            "TetherSideBySide" or "TetherOverlayCompare" => 880,
            "TetherReference" => 1110,
            "TetherInspector" => 260,
            "TetherHistogram" or "TetherExifCollapsed" => 80,
            "TetherLutCollapsed" or "TetherLutExpanded" => 420,
            "TetherClientSingleMonitor" => 650,
            "TetherFileHandling" => 860,
            "LutNone" or "LutImported" or "LutStrength50" or "LutBeforeAfter" or "LutSplitView" or "LutInvalid" => 370,
            "ColorProfileDetected" or "ColorProfileFallback" or "ClientMonitorSelector" or "ClientMonitorFollowMain" or "ClientMonitorFollowLatest" or "ClientMonitorLocked" or "ClientMonitorPrivacy" or "ClientMonitorFavoriteNote" or "ClientMonitorDisconnected" or "ClientMonitorReconnected" or "MixedDpi" => 750,
            _ => 0
        };
        scroll?.ScrollToVerticalOffset(offset);
        scroll?.UpdateLayout();
        InspectorPanel.UpdateLayout();
    }

    private void SetReviewGroup(string header, bool expanded)
    {
        // Review states update the same logical state even before either template exists.
        switch(header)
        {
            case "曝光评估": WorkspacePreferenceState.ExposureExpanded=expanded;break;
            case "参考模式": WorkspacePreferenceState.ReferenceExpanded=expanded;break;
            case "拍摄参考": WorkspacePreferenceState.ShotExpanded=expanded;break;
            case "相机 / 拍摄会话": WorkspacePreferenceState.SessionExpanded=expanded;break;
            case "拍摄信息": WorkspacePreferenceState.InformationExpanded=expanded;break;
            case "标记与备注": WorkspacePreferenceState.AnnotationExpanded=expanded;break;
            case "LUT 与色彩": WorkspacePreferenceState.LutExpanded=expanded;break;
            case "客户监看": WorkspacePreferenceState.ClientExpanded=expanded;break;
            case "文件处理": WorkspacePreferenceState.FileExpanded=expanded;break;
        }
    }

    private void ApplyResponsiveLayout(double windowWidth)
    {
        var compact = windowWidth < 1350;
        var compactToolbar = windowWidth < 1520;
        ThumbnailColumn.MinWidth = ThumbnailColumn.MaxWidth = 0;
        ThumbnailColumn.Width = new GridLength(0);
        BrowserSplitterColumn.Width = new GridLength(0);
        BrowserPanel.Visibility = _isBrowserCollapsed ? Visibility.Collapsed : Visibility.Visible;
        InspectorColumn.MinWidth = compact ? 0 : 320;
        InspectorColumn.MaxWidth = compact ? 0 : 480;
        if (!compact && (InspectorColumn.Width.Value < 320 || InspectorColumn.Width.Value > 480)) InspectorColumn.Width = new GridLength(360);
        InspectorPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        BrowserToolbarButton.Visibility = compactToolbar ? Visibility.Collapsed : Visibility.Visible;
        ClientMonitorToolbarButton.Visibility = compactToolbar ? Visibility.Collapsed : Visibility.Visible;
        InspectorToolbarButton.Visibility = compactToolbar ? Visibility.Collapsed : Visibility.Visible;
        TaskCenterToolbarButton.Visibility = compactToolbar ? Visibility.Collapsed : Visibility.Visible;
        CompactMoreButton.Visibility = compactToolbar ? Visibility.Visible : Visibility.Collapsed;
        SessionSummaryText.MaxWidth = compactToolbar ? 220 : double.PositiveInfinity;
        SessionSummaryText.TextTrimming = compactToolbar ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        if (ViewModel is not null)
        {
            ViewModel.IsInspectorCollapsed = compact;
            if (!compact) ViewModel.ShowInspectorDrawer = false;
        }
    }

    private void ToggleBrowser_Click(object sender, RoutedEventArgs e)
    {
        _isBrowserCollapsed = !_isBrowserCollapsed;
        UpdateResponsiveLayout();
    }

    private void OpenTaskCenter_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this)?.DataContext is MainViewModel viewModel)
            viewModel.NavigateCommand.Execute("Workbench");
    }

    private void OpenCompactMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.DataContext = DataContext;
        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void ZoneButton_MouseEnter(object sender, MouseEventArgs e) { if(sender is Button { Tag: string text } && int.TryParse(text,out var zone)) ViewModel?.SetZoneHover(zone); }
    private void ZoneStrip_MouseLeave(object sender, MouseEventArgs e) => ViewModel?.SetZoneHover(null);
    private void OpenShotReferenceWindow_Click(object sender, RoutedEventArgs e)
    {
        if(ViewModel is null)return;if(_shotReferenceWindow is { IsVisible:true }){_shotReferenceWindow.Activate();return;}
        _shotReferenceWindow=new ShotReferenceWindow{Owner=Window.GetWindow(this),DataContext=ViewModel.ShotExecution};_shotReferenceWindow.Closed+=(_,_)=>_shotReferenceWindow=null;_shotReferenceWindow.Show();
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
            if (viewModel.ShotExecution.IsQuickPreviewOpen) viewModel.ShotExecution.CloseQuickPreviewCommand.Execute(null);
            else if (viewModel.IsFullScreen) viewModel.ToggleFullScreenCommand.Execute(null);
            else if (viewModel.CompareMode != Core.Models.TetherCompareMode.None) viewModel.ExitComparisonCommand.Execute(null);
            else if (viewModel.ShowInspectorDrawer) viewModel.ToggleInspectorCommand.Execute(null);
            else return;
            e.Handled = true; return;
        }
        if (e.OriginalSource is TextBox or PasswordBox or ComboBox) return;
        if (viewModel.IsRapidCompare)
        {
            if ((e.Key is Key.Left or Key.PageUp) && viewModel.PreviousChallengerCommand.CanExecute(null)) { viewModel.PreviousChallengerCommand.Execute(null); e.Handled = true; return; }
            if ((e.Key is Key.Right or Key.PageDown) && viewModel.NextChallengerCommand.CanExecute(null)) { viewModel.NextChallengerCommand.Execute(null); e.Handled = true; return; }
        }
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
            case Key.J:
                if (!viewModel.IsRapidCompare || !viewModel.KeepChampionCommand.CanExecute(null)) break;
                viewModel.KeepChampionCommand.Execute(null); e.Handled = true; break;
            case Key.N:
                if (!viewModel.IsRapidCompare || !viewModel.PromoteChallengerCommand.CanExecute(null)) break;
                viewModel.PromoteChallengerCommand.Execute(null); e.Handled = true; break;
            case Key.PageUp:
                if (!viewModel.IsRapidCompare || !viewModel.PreviousChallengerCommand.CanExecute(null)) break;
                viewModel.PreviousChallengerCommand.Execute(null); e.Handled = true; break;
            case Key.PageDown:
                if (!viewModel.IsRapidCompare || !viewModel.NextChallengerCommand.CanExecute(null)) break;
                viewModel.NextChallengerCommand.Execute(null); e.Handled = true; break;
            case Key.B:
                if (viewModel.ReferenceMode.Enabled) viewModel.ReferenceMode.HoldOriginal(true);
                else viewModel.ColorSettings.ShowBefore = true;
                e.Handled = true; break;
        }
    }

    private void LutBefore_MouseDown(object sender, MouseButtonEventArgs e) { if (ViewModel is not null) ViewModel.ColorSettings.ShowBefore = true; }
    private void LutBefore_MouseUp(object sender, MouseButtonEventArgs e) { if (ViewModel is not null) ViewModel.ColorSettings.ShowBefore = false; }
    private void LutBefore_MouseLeave(object sender, MouseEventArgs e) { if (ViewModel is not null) ViewModel.ColorSettings.ShowBefore = false; }
    private void View_PreviewKeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.B && ViewModel is not null) { ViewModel.ReferenceMode.HoldOriginal(false); ViewModel.ColorSettings.ShowBefore = false; e.Handled = true; } }

    private static void SetRating(TetherCaptureViewModel viewModel, int rating, KeyEventArgs e)
    {
        if (viewModel.SetRatingCommand.CanExecute(rating.ToString())) viewModel.SetRatingCommand.Execute(rating.ToString());
        e.Handled = true;
    }

    private void PreviewViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel is { IsSideBySide: true } viewModel)
        {
            var point = e.GetPosition(PreviewViewport);
            var side = point.X <= PreviewViewport.ActualWidth / 2 ? Core.Models.Photography.CompareSide.Primary : Core.Models.Photography.CompareSide.Secondary;
            viewModel.AdjustCompareZoom(side, e.Delta);
        }
        else ViewModel?.AdjustZoom(e.Delta);
        e.Handled = true;
    }

    private void ComparePrimary_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.SetActiveCompareSide(Core.Models.Photography.CompareSide.Primary);
    }

    private void CompareSecondary_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.SetActiveCompareSide(Core.Models.Photography.CompareSide.Secondary);
    }

    private void PreviewViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null || e.ClickCount > 1) return;
        _dragCompareSide = ViewModel.IsSideBySide && e.GetPosition(PreviewViewport).X > PreviewViewport.ActualWidth / 2
            ? Core.Models.Photography.CompareSide.Secondary : Core.Models.Photography.CompareSide.Primary;
        ViewModel.SetActiveCompareSide(_dragCompareSide);
        (double X, double Y) pan = ViewModel.IsSideBySide ? ViewModel.GetComparePan(_dragCompareSide) : (ViewModel.PanX, ViewModel.PanY);
        _isDragging = true; _dragStart = e.GetPosition(PreviewViewport); _panStartX = pan.X; _panStartY = pan.Y;
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
        if (ViewModel.IsSideBySide) ViewModel.SetComparePan(_dragCompareSide, _panStartX + current.X - _dragStart.X, _panStartY + current.Y - _dragStart.Y);
        else ViewModel.SetPan(_panStartX + current.X - _dragStart.X, _panStartY + current.Y - _dragStart.Y);
    }

    private void PreviewViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDrag();
    private void PreviewViewport_MouseLeave(object sender, MouseEventArgs e) { if (e.LeftButton != MouseButtonState.Pressed) EndDrag(); }
    private void EndDrag() { if (!_isDragging) return; _isDragging = false; PreviewViewport.ReleaseMouseCapture(); ViewModel?.EndCanvasInteraction(); }
    private void Note_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel?.BeginNoteEditing();
    private void Note_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel?.EndNoteEditing();
}
