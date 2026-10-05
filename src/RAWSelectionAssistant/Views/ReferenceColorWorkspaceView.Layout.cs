using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class ReferenceColorWorkspaceView
{
    private void OnAddTransition(object sender, RoutedEventArgs e)
    {
        _editor?.AddAdjustmentNodeCommand.Execute("TransitionBlend");
        AuxModes.SelectedIndex = 3;
    }
    private void OnToggleAuxRail(object sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        var wasVisible = ContextRail.Visibility == Visibility.Visible;
        _editor.ContextRailOpen = !wasVisible;
        if (!wasVisible && GetAvailableWidth() < 950) _editRailOpen = false;
        UpdateResponsiveLayout();
    }
    private void OnAuxModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AuxPages is null || AuxModes.SelectedItem is not ListBoxItem selected) return;
        var id = selected.Tag?.ToString();
        foreach (var (page, mode) in new[] { (ReferencePage, "Reference"), (SpacePage, "Space"), (PresetsPage, "Presets"), (NodesPage, "Nodes") })
            page.Visibility = id == mode ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnToolModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ToolPages is null || ToolModes.SelectedItem is not ListBoxItem selected) return;
        var id = selected.Tag?.ToString();
        foreach (var (page, mode) in new[] { (SpaceToolPage, "Space"), (ColorToolPage, "Color"), (LevelsToolPage, "Levels"), (CurveToolPage, "Curve"), (DetailsToolPage, "Details"), (FilmToolPage, "Film"), (CreativeToolPage, "Creative") })
            page.Visibility = id == mode ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnSurfaceSelection(object? sender, OklabColor color)
    { if (DataContext is ReferenceColorWorkspaceViewModel workspace) workspace.HighlightColor(color); }
    private void OnBasicColorRange(object sender, RoutedEventArgs e)
    {
        if (_editor is null || sender is not Button button) return;
        _editor.AddAdjustmentNodeCommand.Execute("ColorRange");
        var sample = button.Tag?.ToString() switch { "Red" => new VisualRgb24(220, 50, 45), "Green" => new VisualRgb24(55, 160, 60), _ => new VisualRgb24(35, 80, 220) };
        _editor.AddDisplayedSample(sample); AuxModes.SelectedIndex = 3;
    }
    private void OnFilmPresetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_editor is null || sender is not ListBox { SelectedItem: PixelTartFilmProfile profile }) return;
        _editor.FilmProfileId = profile.Id; _editor.FilmEnabled = true; ToolModes.SelectedIndex = 5;
    }
    private void OnFilmstripMore(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
        var menu = new ContextMenu { PlacementTarget = button };
        void Add(string title, System.Windows.Input.ICommand command, object? parameter = null)
            => menu.Items.Add(new MenuItem { Header = title, Command = command, CommandParameter = parameter });
        Add("复制当前调整", workspace.CopyAdjustmentsCommand); Add("应用已复制调整", workspace.ApplyAdjustmentsCommand);
        Add("撤销批量调整", workspace.UndoBatchAdjustmentCommand); Add("重做批量调整", workspace.RedoBatchAdjustmentCommand);
        menu.Items.Add(new Separator());
        var rating = new MenuItem { Header = "为所选评分" };
        for (var i = 0; i <= 5; i++) rating.Items.Add(new MenuItem { Header = i == 0 ? "清除评分" : new string('★', i), Command = workspace.SetSelectedRatingCommand, CommandParameter = i });
        menu.Items.Add(rating);
        var colors = new MenuItem { Header = "为所选设置色标" };
        foreach (var color in new[] { "", "红", "橙", "黄", "绿", "蓝", "紫" }) colors.Items.Add(new MenuItem { Header = color == "" ? "清除色标" : color, Command = workspace.SetSelectedColorLabelCommand, CommandParameter = color });
        menu.Items.Add(colors); menu.Items.Add(new Separator());
        Add("快速导出所选", workspace.ExportSelectedCommand); Add("快速导出可见照片", workspace.ExportAllCommand);
        Add("通过发布配方导出…", workspace.PreparePublishingCommand); Add("重试失败导出", workspace.RetryFailedExportCommand);
        menu.IsOpen = true;
    }
}

