using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class PlanningCenterView
{
    private void ShowExternalReconnect()
    {
        if(_vm is null)return;
        var panel=new StackPanel{Margin=new Thickness(18),MaxWidth=760};
        panel.Children.Add(Text("缺失外链与手动重连",24,true));
        panel.Children.Add(Text("仅处理逐项明确选择的外部参考图和普通附件。不扫描目录、不猜测同名文件；保留原引用ID、原路径及分区。",14));
        foreach(var item in _vm.ExternalDependencies)
        {
            var row=new StackPanel{Margin=new Thickness(0,18,0,0)};
            row.Children.Add(Text(item.Name+" · "+item.Section,18,true));row.Children.Add(Text("ID："+item.Id+"\n原路径："+item.OriginalPath+"\n原因/状态："+item.Reason,13));
            if(item.CanReconnect)
            {
                var actions=new WrapPanel();
                actions.Children.Add(AsyncAction("定位新文件（继续外链）",async()=>{await LocateExternalFromWindowAsync(item,false);}));
                actions.Children.Add(AsyncAction("导入项目副本",async()=>{await LocateExternalFromWindowAsync(item,true);}));row.Children.Add(actions);
            }
            panel.Children.Add(row);
        }
        panel.Children.Add(Text("未支持/原项目包排除依赖（历史清单，不会自动消除）：",16,true));
        foreach(var text in _vm.UnsupportedPackageDependencies)panel.Children.Add(Text(text,12));
        if(_externalReconnectWindow is {IsVisible:true} previous)
        {previous.Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=panel};return;}
        var window=new Window{Title="缺失外链与手动重连",Owner=Window.GetWindow(this),Width=880,Height=740,Background=Brush("SurfacePrimaryBrush"),Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=panel},WindowStartupLocation=WindowStartupLocation.CenterOwner};
        _externalReconnectWindow=window;window.Show();
    }
    private async Task LocateExternalFromWindowAsync(PlanningExternalDependency item,bool importCopy)
    {
        // IDialogService owns native pickers/confirmation on MainWindow. A modeless owned
        // window must not obscure that modal or remain interactive above its owner.
        var window=_externalReconnectWindow;window?.Hide();
        try{if(_vm is not null)await _vm.LocateExternalAsync(item,importCopy);}
        finally{if(window is not null){window.Show();ShowExternalReconnect();window.Activate();}}
    }
    private Window? _externalReconnectWindow;
}

