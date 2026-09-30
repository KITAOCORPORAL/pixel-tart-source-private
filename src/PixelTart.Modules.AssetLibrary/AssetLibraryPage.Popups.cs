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
        var edit = new MenuItem { Header = "评分" };
        var filter = new MenuItem { Header = "评分筛选" };
        for (var rating = 0; rating <= 5; rating++)
        {
            var value = rating;
            edit.Items.Add(PopupAction(value == 0 ? "清除评分" : $"{value} 星", $"AssetSetRating{value}",
                () => _viewModel.RateSelectedAsync(value), _viewModel.HasSelection));
            filter.Items.Add(PopupAction(value == 0 ? "不限评分" : $"至少 {value} 星", $"AssetFilterRating{value}",
                () => _viewModel.SetQuickFilterAsync(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual,
                    value == 0 ? [] : [value.ToString()])));
        }
        menu.Items.Add(edit); menu.Items.Add(filter);
        OpenToolbarPopup(anchor, "Rating", menu);
    }

    private void OpenColorPopup(Button anchor)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "颜色标记" };
        var filter = new MenuItem { Header = "颜色标记筛选" };
        foreach (var color in _viewModel.InspectorColors)
        {
            var value = color;
            edit.Items.Add(PopupAction(value.Length == 0 ? "清除颜色" : value, "AssetSetColor" + value, async () =>
            {
                _viewModel.InspectorColor = value;
                _viewModel.ApplyInspectorColorCommand.Execute(null);
                await _viewModel.ApplyInspectorColorCommand.ExecutionTask;
            }, _viewModel.HasSelection));
            filter.Items.Add(PopupAction(value.Length == 0 ? "不限颜色" : value, "AssetFilterColorLabel" + value,
                () => _viewModel.SetQuickFilterAsync(AssetQueryField.ColorLabel, AssetQueryOperator.Equals, value.Length == 0 ? [] : [value])));
        }
        menu.Items.Add(edit); menu.Items.Add(filter);
        OpenToolbarPopup(anchor, "Color", menu);
    }

    private void OpenTagPopup(Button anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(PopupAction("不限标签", "AssetFilterTagAll", () => _viewModel.SetQuickFilterAsync(AssetQueryField.Tag, AssetQueryOperator.AnyOf)));
        foreach (var tag in _viewModel.Tags.Where(tag => !tag.IsArchived))
            menu.Items.Add(PopupAction(tag.Name, "AssetFilterTag" + tag.TagId.ToString("N"),
                () => _viewModel.SetQuickFilterAsync(AssetQueryField.Tag, AssetQueryOperator.AnyOf, "id:" + tag.TagId.ToString("D"))));
        OpenToolbarPopup(anchor, "Tag", menu);
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
