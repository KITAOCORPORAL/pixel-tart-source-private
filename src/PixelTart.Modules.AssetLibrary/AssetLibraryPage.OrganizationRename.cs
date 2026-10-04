using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetLibraryPage
{
    private static object? FindOrganizationNode(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement { DataContext: AssetLibraryFolderNodeView or AssetLibraryTagNodeView or AssetLibraryTagGroupNodeView } row)
                return row.DataContext;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return null;
    }

    private void RenameOrganization_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: { } node }) ShowOrganizationRename(node);
    }

    private void ShowOrganizationRename(object node)
    {
        var name = node switch
        {
            AssetLibraryFolderNodeView folder => folder.Name,
            AssetLibraryTagNodeView tag => tag.Name,
            AssetLibraryTagGroupNodeView { Group: not null } group => group.Name,
            _ => null
        };
        if (name is null) return;
        var kind = node is AssetLibraryFolderNodeView ? "文件夹" : node is AssetLibraryTagNodeView ? "标签" : "标签组";
        var panel = new StackPanel { Margin = new Thickness(20) };
        var input = new TextBox { Text = name, MinHeight = 32, Margin = new Thickness(0, 8, 0, 8) };
        System.Windows.Automation.AutomationProperties.SetName(input, "新的" + kind + "名称");
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        error.SetResourceReference(TextBlock.ForegroundProperty, "WarningBrush");
        panel.Children.Add(new TextBlock { Text = "重命名" + kind }); panel.Children.Add(input); panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "取消", IsCancel = true, MinWidth = 72, Margin = new Thickness(4) };
        var save = new Button { Content = "保存", IsDefault = true, MinWidth = 72, Margin = new Thickness(4) };
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
        var dialog = new Window { Title = "重命名" + kind, Owner = Window.GetWindow(this), Content = panel,
            Width = 430, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MaxWidth = SystemParameters.WorkArea.Width, ShowInTaskbar = false };
        dialog.SetResourceReference(BackgroundProperty, "Brush.Panel");
        dialog.SetResourceReference(ForegroundProperty, "Brush.Text.Primary");
        save.Click += async (_, _) =>
        {
            save.IsEnabled = false;
            try
            {
                await _viewModel.RenameOrganizationAsync(node, input.Text);
                dialog.DialogResult = true;
            }
            catch (Exception exception) { error.Text = exception.Message; input.Focus(); }
            finally { save.IsEnabled = true; }
        };
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        dialog.ShowDialog();
    }
}
