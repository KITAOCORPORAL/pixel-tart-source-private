using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

/// <summary>One export entry point over the existing, independently verified encoder pipeline.</summary>
public sealed class DarkroomExportDialog : Window
{
    public DarkroomExportDialog(ReferenceColorWorkspaceViewModel vm)
    {
        Title="导出"; Width=640; Height=350; ResizeMode=ResizeMode.NoResize;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(29,29,32)); Foreground=Brushes.Gainsboro;
        Closing+=(_,e)=>{if(vm.IsExporting)e.Cancel=true;};
        FontFamily=new FontFamily("Microsoft YaHei UI"); FontSize=11; DataContext=vm;
        var grid=new Grid { Margin=new Thickness(12) };
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(310)}); grid.ColumnDefinitions.Add(new());
        grid.RowDefinitions.Add(new(){Height=GridLength.Auto}); grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var title=new TextBlock {Text="导出",FontSize=14,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)};
        Grid.SetColumnSpan(title,2); grid.Children.Add(title);
        var settings=new StackPanel {Margin=new Thickness(0,0,18,0)}; Grid.SetRow(settings,1);
        settings.Children.Add(new TextBlock {Text="配方",Foreground=Brushes.Gray});
        settings.Children.Add(new TextBlock {Text="当前所选照片 · 原尺寸",Margin=new Thickness(0,6,0,16)});
        settings.Children.Add(new TextBlock {Text="格式",Foreground=Brushes.Gray});
        settings.Children.Add(new StudioExportFormatSelector {DataContext=vm,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,5,0,12)});
        settings.Children.Add(new TextBlock {Text="输出色彩",Foreground=Brushes.Gray});
        settings.Children.Add(new TextBlock {Text="sRGB · 嵌入输出配置",Margin=new Thickness(0,6,0,6)});
        settings.Children.Add(new TextBlock {Text="PNG16 / TIFF16 / JPEG8",Foreground=Brushes.Gray}); grid.Children.Add(settings);
        var details=new StackPanel {Margin=new Thickness(14,0,0,0)}; Grid.SetColumn(details,1); Grid.SetRow(details,1);
        details.Children.Add(new TextBlock {Text="照片与位置",Foreground=Brushes.Gray});
        var count=new TextBlock {Margin=new Thickness(0,6,0,12)};
        count.SetBinding(TextBlock.TextProperty,new Binding("SelectedTargetCount"){StringFormat="所选照片 {0} 张"}); details.Children.Add(count);
        details.Children.Add(new TextBlock {Text="文件名：原文件名 + 仿色",Margin=new Thickness(0,0,0,8)});
        details.Children.Add(new TextBlock {Text="位置：确认后选择目录",Margin=new Thickness(0,0,0,12)});
        details.Children.Add(new TextBlock {Text="已显影光片导出相框与画面合成；使用完整冻结定义，不导出预览截图。",TextWrapping=TextWrapping.Wrap});
        var status=new TextBlock {TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray,Margin=new Thickness(0,12,0,0)};
        status.SetBinding(TextBlock.TextProperty,new Binding("StatusText")); details.Children.Add(status); grid.Children.Add(details);
        var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        Grid.SetRow(actions,2); Grid.SetColumnSpan(actions,2);
        var cancel=new Button {Content="取消",IsCancel=true,Padding=new Thickness(14,5,14,5),Margin=new Thickness(0,0,8,0)};
        cancel.Click+=(_,_)=>Close();
        var go=new Button {Content="导出所选",Padding=new Thickness(14,5,14,5),Background=new SolidColorBrush(Color.FromRgb(74,118,108)),Foreground=Brushes.White};
        go.Click+=async(_,_)=>
        {
            if(!vm.ExportSelectedCommand.CanExecute(null))return;
            go.IsEnabled=false; cancel.IsEnabled=false;
            try {await vm.ExportSelectedCommand.ExecuteAsync(null);}
            finally {go.IsEnabled=true;cancel.IsEnabled=true;}
        };
        actions.Children.Add(cancel); actions.Children.Add(go); grid.Children.Add(actions); Content=grid;
    }
}
