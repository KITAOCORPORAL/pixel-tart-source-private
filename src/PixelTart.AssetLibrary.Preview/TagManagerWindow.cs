using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PixelTart.AssetLibrary.Preview;

public sealed class TagManagerWindow : Window
{
    public TagManagerWindow()
    {
        Title = "标签管理"; Width = 760; Height = 620; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var grid = new Grid { Margin = new(16) }; grid.ColumnDefinitions.Add(new() { Width = new(220) }); grid.ColumnDefinitions.Add(new() { Width = new(12) }); grid.ColumnDefinitions.Add(new());
        var groups = new ListBox { DisplayMemberPath = "Name" }; groups.SetBinding(ItemsControl.ItemsSourceProperty, "TagGroups"); Grid.SetColumn(groups, 0); grid.Children.Add(groups);
        var right = new DockPanel(); Grid.SetColumn(right, 2); var header = new TextBlock { Text = "全部标签 · 未分组 · Usage Count · 搜索 / 批量移动 / 重命名 / 删除 / 合并", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 10) }; DockPanel.SetDock(header, Dock.Top); right.Children.Add(header); var list = new DataGrid { AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Extended, IsReadOnly = true }; list.Columns.Add(new DataGridTextColumn { Header = "标签", Binding = new Binding("Name"), Width = new(1, DataGridLengthUnitType.Star) }); list.Columns.Add(new DataGridTextColumn { Header = "使用", Binding = new Binding("UsageCount"), Width = 80 }); list.SetBinding(ItemsControl.ItemsSourceProperty, "Tags"); right.Children.Add(list); grid.Children.Add(right); Content = grid;
    }
}
