using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

/// <summary>One entry point to the existing quick export and publishing recipe commands.</summary>
public sealed class StudioExportMenu : Button
{
    public StudioExportMenu()
    {
        StudioTextExtension.Bind(this, ContentProperty, "Export");
        StudioTextExtension.Bind(this, System.Windows.Automation.AutomationProperties.NameProperty, "Export");
        SetResourceReference(StyleProperty, "PrimaryButton");
        SetBinding(ToolTipProperty, new Binding("QuickExportFormatSummary"));
        Click += (_, _) =>
        {
            if (DataContext is not ReferenceColorWorkspaceViewModel workspace) return;
            var menu = CreateMenu(workspace); menu.PlacementTarget = this;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        };
    }
    internal static ContextMenu CreateMenu(ReferenceColorWorkspaceViewModel workspace)
    {
        var menu = new ContextMenu { DataContext = workspace };
        var summary = new MenuItem { IsEnabled = false, MaxWidth = 420 };
        summary.Header = new TextBlock { TextWrapping = TextWrapping.Wrap };
        ((TextBlock)summary.Header).SetBinding(TextBlock.TextProperty, new Binding("QuickExportFormatSummary") { Source = workspace });
        menu.Items.Add(summary);
        var format = new MenuItem { StaysOpenOnClick = true, Header = new StudioExportFormatSelector { DataContext = workspace } };
        menu.Items.Add(format); menu.Items.Add(new Separator());
        void Add(string key, System.Windows.Input.ICommand command)
        {
            var action = new MenuItem { Command = command };
            StudioTextExtension.Bind(action, HeaderedItemsControl.HeaderProperty, key); menu.Items.Add(action);
        }
        Add("快速导出所选", workspace.ExportSelectedCommand);
        Add("快速导出可见照片", workspace.ExportAllCommand);
        Add("发布图片 / 导出配方…", workspace.PreparePublishingCommand);
        return menu;
    }
}
