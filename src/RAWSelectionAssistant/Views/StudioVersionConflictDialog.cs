using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public sealed class StudioVersionConflictDialog:Window
{
    public string Choice {get;private set;}="Cancel";
    public StudioVersionConflictDialog(StudioVersionConflict conflict)
    {
        Title="编辑版本冲突";Width=900;Height=640;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(BackgroundProperty,"Brush.Background");
        var root=new DockPanel{Margin=new Thickness(16)};Content=root;
        var description=new TextBlock{Text=conflict.Reason+"\n来源："+conflict.Repository.Identity+"\n仓储修订："+conflict.Repository.Revision,TextWrapping=TextWrapping.Wrap};DockPanel.SetDock(description,Dock.Top);root.Children.Add(description);
        var buttons=new WrapPanel();DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);
        foreach(var option in new[]{("重新载入仓储","Reload"),("将本地保留为新版本","KeepLocal"),("取消并保留快照","Cancel")}){var value=option.Item2;var button=new Button{Content=option.Item1,Margin=new Thickness(4),Padding=new Thickness(10)};button.Click+=(_,_)=>{Choice=value;DialogResult=true;};buttons.Children.Add(button);}
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());root.Children.Add(grid);
        var options=new JsonSerializerOptions{WriteIndented=true};var local=new TextBox{Text="本地快照\n"+JsonSerializer.Serialize(conflict.Local,options),IsReadOnly=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var remote=new TextBox{Text="仓储版本\n"+JsonSerializer.Serialize(conflict.Repository.Versions,options),IsReadOnly=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(remote,1);grid.Children.Add(local);grid.Children.Add(remote);
    }
}
