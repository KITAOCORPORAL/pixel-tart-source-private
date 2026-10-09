using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class ReferenceColorWorkspaceView
{
    private readonly DispatcherTimer _slideshowTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _slideshowMoving;
    private void InitializeFilmstripLayout()
    {
        _slideshowTimer.Tick += async (_, _) => await MoveSlideshowAsync(1);
        Unloaded += (_, _) => SetSlideshowPlaying(false);
        FilmstripExpander.Collapsed += (_, _) =>
        {
            SetSlideshowPlaying(false);
            if (!_restoringLayout && DataContext is ReferenceColorWorkspaceViewModel workspace)
                workspace.Layout = workspace.Layout with { FilmstripOpen = false };
        };
        FilmstripExpander.Expanded += (_, _) =>
        {
            if (!_restoringLayout && DataContext is ReferenceColorWorkspaceViewModel workspace)
                workspace.Layout = workspace.Layout with { FilmstripOpen = true };
        };
    }
    private void ApplyFilmstripLayout()
    {
        if (FilmstripPanel is null || DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
        var right = workspace.Layout.FilmstripDock == "Right";
        // Dock in a dedicated column, never over the image or editing rail.
        Grid.SetRow(FilmstripPanel, right ? 1 : 2);
        Grid.SetColumn(FilmstripPanel, right ? 1 : 0);
        Grid.SetColumnSpan(FilmstripPanel, right ? 1 : 2);
        FilmstripDockColumn.Width = new(right ? Math.Clamp(GetAvailableWidth() * .22, 240, 300) : 0);
        FilmstripPanel.Margin = right ? new(8, 0, 0, 0) : new(0, 6, 0, 0);
        // Bottom docking must stay compact in every display mode. The right dock
        // can use the full height without taking vertical space from the photo.
        Filmstrip.MaxHeight = right ? double.PositiveInfinity : 90;
        ScrollViewer.SetHorizontalScrollBarVisibility(Filmstrip, right ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(Filmstrip, right ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
        var panel = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
        panel.SetValue(StackPanel.OrientationProperty, right ? Orientation.Vertical : Orientation.Horizontal);
        Filmstrip.ItemsPanel = new ItemsPanelTemplate(panel);
        FilmstripExpander.IsExpanded = workspace.Layout.FilmstripOpen;
        var slideshow = workspace.Layout.FilmstripView == "Slideshow";
        SlideshowPrevious.Visibility = SlideshowPlay.Visibility = SlideshowNext.Visibility = slideshow ? Visibility.Visible : Visibility.Collapsed;
        if (!slideshow) SetSlideshowPlaying(false);
    }
    private void SetSlideshowPlaying(bool playing)
    {
        if (playing) _slideshowTimer.Start(); else _slideshowTimer.Stop();
        if (SlideshowPlay is not null) StudioTextExtension.Bind(SlideshowPlay, ContentControl.ContentProperty, playing ? "PauseSlideshow" : "PlaySlideshow");
    }
    private async Task MoveSlideshowAsync(int delta)
    {
        if (_slideshowMoving || DataContext is not ReferenceColorWorkspaceViewModel workspace || workspace.IsLoading || workspace.IsExporting) return;
        var photos = workspace.VisibleTargets.ToArray();
        if (photos.Length == 0) { SetSlideshowPlaying(false); return; }
        var current = Array.FindIndex(photos, item => ReferenceEquals(item, workspace.ActiveTarget));
        var next = photos[(current + delta + photos.Length) % photos.Length];
        _slideshowMoving = true;
        try
        {
            // Activation changes the current photo only; user selection/filter remain authoritative.
            await workspace.ActivateTargetCommand.ExecuteAsync(next);
            Filmstrip.ScrollIntoView(next);
        }
        finally { _slideshowMoving = false; }
    }
    private void OnSlideshowPlay(object sender, RoutedEventArgs e) => SetSlideshowPlaying(!_slideshowTimer.IsEnabled);
    private async void OnSlideshowPrevious(object sender, RoutedEventArgs e) => await MoveSlideshowAsync(-1);
    private async void OnSlideshowNext(object sender, RoutedEventArgs e) => await MoveSlideshowAsync(1);
    private void OnFilmstripLayoutMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
        var menu = new ContextMenu { PlacementTarget = button };
        void Add(string label, bool selected, Action change)
        {
            var item = new MenuItem { IsCheckable = true, IsChecked = selected };
            StudioTextExtension.Bind(item, HeaderedItemsControl.HeaderProperty, label);
            item.Click += (_, _) => change(); menu.Items.Add(item);
        }
        foreach (var dock in new[] { "Bottom", "Right" })
            Add("FilmstripDock" + dock, workspace.Layout.FilmstripDock == dock, () => workspace.Layout = workspace.Layout with { FilmstripDock = dock });
        menu.Items.Add(new Separator());
        foreach (var mode in new[] { "Grid", "List", "Slideshow" })
            Add("FilmstripView" + mode, workspace.Layout.FilmstripView == mode, () => workspace.Layout = workspace.Layout with { FilmstripView = mode });
        menu.IsOpen = true;
    }
}
