using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetLibraryPage
{
    private ContextMenu? _activeToolbarPopup;
    private Button? _dismissedPopupAnchor;
    public string? ActiveToolbarPopup { get; private set; }

    private void OpenToolbarPopup(Button anchor, string name, ContextMenu menu)
    {
        if (_viewModel.HasUnsavedSmartFolderChanges) { _viewModel.RequestSmartFolderClose(() => OpenToolbarPopup(anchor, name, menu)); return; }
        if (_viewModel.P3SmartFolderOpen) _viewModel.RequestSmartFolderClose();
        var toggleOff = ReferenceEquals(_activeToolbarPopup?.PlacementTarget, anchor) || ReferenceEquals(_dismissedPopupAnchor, anchor);
        _dismissedPopupAnchor = null;
        CloseToolbarPopups();
        if (toggleOff) return;
        menu.PlacementTarget = anchor;
        menu.Placement = PlacementMode.Bottom;
        if (TryFindResource("PixelTart.Menu.Context") is Style style) menu.Style = style;
        AutomationProperties.SetAutomationId(menu, "AssetPopup" + name);
        ActiveToolbarPopup = name;
        _activeToolbarPopup = menu;
        menu.Closed += (_, _) =>
        {
            if (!ReferenceEquals(_activeToolbarPopup, menu)) return;
            var pointer = Mouse.GetPosition(anchor);
            if (Mouse.LeftButton == MouseButtonState.Pressed && new Rect(anchor.RenderSize).Contains(pointer)) _dismissedPopupAnchor = anchor;
            _activeToolbarPopup = null;
            ActiveToolbarPopup = null;
        };
        menu.IsOpen = true;
    }

    private void CloseToolbarPopups()
    {
        var menu = _activeToolbarPopup;
        _activeToolbarPopup = null;
        ActiveToolbarPopup = null;
        if (menu is not null) menu.IsOpen = false;
        _viewModel.P3QueryPanelOpen = false;
        _viewModel.DismissP3Suggestions();
    }

    private MenuItem PopupAction(string title, string id, Func<Task> action, bool enabled = true)
    {
        var item = new MenuItem { Header = title, IsEnabled = enabled };
        AutomationProperties.SetAutomationId(item, id);
        item.Click += async (_, e) =>
        {
            e.Handled = true;
            try { await action(); }
            catch (Exception exception) { MessageBox.Show(Window.GetWindow(this), exception.Message, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        return item;
    }

    private void OpenRatingPopup(Button anchor)
    {
        var menu = new ContextMenu();
        for (var rating = 0; rating <= 5; rating++)
        {
            var value = rating;
            menu.Items.Add(PopupAction(value == 0 ? "不限评分" : "至少 " + new string('★', value), $"AssetFilterRating{value}",
                () => _viewModel.SetQuickFilterAsync(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual,
                    value == 0 ? [] : [value.ToString()])));
        }
        OpenToolbarPopup(anchor, "Rating", menu);
    }

    private void OpenColorPopup(Button anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new AssetColorFilterPicker { DataContext = _viewModel });
        OpenToolbarPopup(anchor, "Color", menu);
    }

    private void OpenTagPopup(Button anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(PopupAction("不限标签", "AssetFilterTagAll", () => _viewModel.SetQuickFilterAsync(AssetQueryField.Tag, AssetQueryOperator.AnyOf)));
        foreach (var tag in _viewModel.Tags.Where(tag => !tag.IsArchived && (tag.TagGroupId is null || _viewModel.TagGroups.Any(group => group.TagGroupId == tag.TagGroupId && !group.IsArchived))))
            menu.Items.Add(PopupAction(tag.Name, "AssetFilterTag" + tag.TagId.ToString("N"),
                () => _viewModel.SetQuickFilterAsync(AssetQueryField.Tag, AssetQueryOperator.AnyOf, "id:" + tag.TagId.ToString("D"))));
        OpenToolbarPopup(anchor, "Tag", menu);
    }

    private void InspectorTagPicker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button anchor || !_viewModel.CanEditInspectorRelations) return;
        _viewModel.InspectorTagSearch = "";
        var menu = new ContextMenu();
        // Content is hosted by the existing single-active overlay manager.
        menu.Items.Add(new AssetInspectorTagPicker { DataContext = _viewModel });
        OpenToolbarPopup(anchor, "InspectorTags", menu);
    }

    private async void InspectorColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: string color }) return;
        _viewModel.InspectorColor = color;
        _viewModel.ApplyInspectorColorCommand.Execute(null);
        await _viewModel.ApplyInspectorColorCommand.ExecutionTask;
    }

    private void PinnedFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AssetQueryField field } anchor) OpenQuickFilter(anchor, field);
    }

    private void OpenQuickFilter(Button anchor, AssetQueryField field)
    {
        if (field == AssetQueryField.Rating) OpenRatingPopup(anchor);
        else if (field == AssetQueryField.VisualDominantColor) OpenColorPopup(anchor);
        else if (field == AssetQueryField.Tag) OpenTagPopup(anchor);
        else if (field == AssetQueryField.AddedAt) OpenDatePopup(anchor);
        else { CloseToolbarPopups(); _viewModel.EditQuickFilter(field); ActiveToolbarPopup = "AdvancedFilter"; }
    }

    private void AddQuickFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button anchor) return;
        var menu = new ContextMenu();
        foreach (var field in new[] { AssetQueryField.Rating, AssetQueryField.VisualDominantColor, AssetQueryField.ColorLabel,
            AssetQueryField.Tag, AssetQueryField.Folder, AssetQueryField.Extension, AssetQueryField.AddedAt,
            AssetQueryField.CaptureTime, AssetQueryField.AspectRatio, AssetQueryField.Width, AssetQueryField.Height, AssetQueryField.FileSize })
        {
            var current = field;
            var group = new MenuItem { Header = field == AssetQueryField.VisualDominantColor ? "图片颜色" : P3QueryNodeView.FieldLabel(field) };
            group.Items.Add(PopupAction("编辑条件", "QuickFilterEdit" + field, () => { _dismissedPopupAnchor = null; CloseToolbarPopups(); OpenQuickFilter(anchor, current); return Task.CompletedTask; }));
            var pinned = _viewModel.PinnedFilters.Any(item => item.Value == field);
            group.Items.Add(PopupAction(pinned ? "取消固定" : "固定到顶部", "QuickFilterPin" + field,
                () => { _viewModel.SetFilterPinned(current, !pinned); return Task.CompletedTask; }));
            menu.Items.Add(group);
        }
        OpenToolbarPopup(anchor, "AddFilter", menu);
    }

    private void OpenDatePopup(Button anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(PopupAction("不限日期", "AssetFilterDateAll", () => _viewModel.SetQuickFilterAsync(AssetQueryField.AddedAt, AssetQueryOperator.GreaterThanOrEqual)));
        foreach (var days in new[] { 1, 7, 30 })
            menu.Items.Add(PopupAction($"最近 {days} 天导入", "AssetFilterDate" + days, () => _viewModel.SetQuickFilterAsync(
                AssetQueryField.AddedAt, AssetQueryOperator.GreaterThanOrEqual, DateTimeOffset.Now.AddDays(-days).ToString("O"))));
        OpenToolbarPopup(anchor, "Date", menu);
    }

    private void OpenAdvancedFilter()
    {
        var wasOpen = _viewModel.P3QueryPanelOpen;
        CloseToolbarPopups();
        if (wasOpen) return;
        _viewModel.OpenFilterPanel();
        ActiveToolbarPopup = "AdvancedFilter";
    }

    private void PopupOutsideClick(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.P3QueryPanelOpen || e.OriginalSource is not DependencyObject source) return;
        for (var current = source; current is not null; current = GetInputParent(current))
            if (ReferenceEquals(current, AssetFilterPopover) || current is Button { Name: "AssetAdvancedFilterButton" }) return;
        CloseToolbarPopups();
    }
}
