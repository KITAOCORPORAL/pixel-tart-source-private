using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.AssetLibrary.Preview;

public sealed class ClassifierWindow : Window
{
    private readonly AssetLibraryPreviewViewModel _viewModel;
    private readonly ListBox _folders;
    public ClassifierWindow(AssetLibraryPreviewViewModel viewModel)
    {
        _viewModel = viewModel; DataContext = viewModel; Title = "F · 添加到文件夹"; Width = 520; Height = 620; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = System.Windows.Media.Brushes.Black; Foreground = System.Windows.Media.Brushes.White;
        var root = new DockPanel { Margin = new(16) }; var title = new TextBlock { Text = $"添加到文件夹 · 已选 {viewModel.SelectionCount} 项", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 10) }; DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
        var search = new TextBox { Margin = new(0, 0, 0, 8) }; search.SetBinding(TextBox.TextProperty, "FolderSearch"); DockPanel.SetDock(search, Dock.Top); root.Children.Add(search);
        var hint = new TextBlock { Text = "最近使用 / 收藏文件夹 / 层级树 · ↑↓导航 · Space多选 · Enter确认 · Esc关闭", Foreground = System.Windows.Media.Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8) }; DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 8, 0, 0) }; var create = new Button { Content = "新建文件夹", Margin = new(4) }; create.SetBinding(Button.CommandProperty, "NewFolderCommand"); var confirm = new Button { Content = "确认加入", Margin = new(4), IsDefault = true }; confirm.Click += async (_, _) => await ConfirmAsync(); buttons.Children.Add(create); buttons.Children.Add(confirm); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        _folders = new ListBox { SelectionMode = SelectionMode.Multiple, DisplayMemberPath = "Name" }; _folders.SetBinding(ItemsControl.ItemsSourceProperty, "ClassifierFolders"); root.Children.Add(_folders); Content = root;
        PreviewKeyDown += async (_, args) => { if (args.Key == Key.Escape) { Close(); args.Handled = true; } else if (args.Key == Key.Enter) { await ConfirmAsync(); args.Handled = true; } };
        Loaded += (_, _) => search.Focus();
    }
    private async Task ConfirmAsync() { var ids = _folders.SelectedItems.Cast<AssetFolder>().Select(x => x.FolderId).ToArray(); if (ids.Length == 0 && _folders.SelectedItem is AssetFolder single) ids = [single.FolderId]; await _viewModel.ApplyFoldersAsync(ids); Close(); }
}
