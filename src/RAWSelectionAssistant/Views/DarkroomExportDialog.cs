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
        Title="导出 · 当前处理链"; Width=720; Height=450; ResizeMode=ResizeMode.NoResize;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(29,29,32)); Foreground=Brushes.Gainsboro;
        Closing+=(_,e)=>{if(vm.IsExporting)e.Cancel=true;};
        FontFamily=new FontFamily("Microsoft YaHei UI"); FontSize=11; DataContext=vm;
        var grid=new Grid { Margin=new Thickness(12) };
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(300)}); grid.ColumnDefinitions.Add(new());
        grid.RowDefinitions.Add(new(){Height=GridLength.Auto}); grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var title=new TextBlock {Text="导出",FontSize=14,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)};
        Grid.SetColumnSpan(title,2); grid.Children.Add(title);
        var settings=new StackPanel {Margin=new Thickness(0,0,18,0)}; Grid.SetRow(settings,1);
        settings.Children.Add(new TextBlock {Text="当前输出",Foreground=Brushes.Gray});
        settings.Children.Add(new TextBlock {Text="当前所选照片 · 原尺寸",Margin=new Thickness(0,6,0,16)});
        settings.Children.Add(new TextBlock {Text="格式",Foreground=Brushes.Gray});
        settings.Children.Add(new StudioExportFormatSelector {DataContext=vm,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,5,0,12)});
        settings.Children.Add(new TextBlock {Text="输出色彩",Foreground=Brushes.Gray});
        var colorLabel=new TextBlock{Text=vm.ActiveTarget?.RawDefinition?.ColorState is{} color?color.Output.Label+" · 实际ICC转换/嵌入":"sRGB · 兼容路径嵌入配置",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};settings.Children.Add(colorLabel);
        settings.Children.Add(new TextBlock {Text=vm.ActiveTarget?.RawDefinition?.ColorState is not null?"ICC V1：PNG16 / TIFF16；JPEG尚未放行":"PNG16 / TIFF16 / JPEG8",Foreground=Brushes.Gray}); grid.Children.Add(settings);
        var details=new StackPanel {Margin=new Thickness(14,0,0,0)}; Grid.SetColumn(details,1); Grid.SetRow(details,1);
        details.Children.Add(new TextBlock {Text="照片与位置",Foreground=Brushes.Gray});
        var count=new TextBlock {Margin=new Thickness(0,6,0,12)};
        count.SetBinding(TextBlock.TextProperty,new Binding("SelectedTargetCount"){StringFormat="所选照片 {0} 张"}); details.Children.Add(count);
        var image=new Image{Height=104,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,10)};image.SetBinding(Image.SourceProperty,new Binding("Editor.MatchedImage"));details.Children.Add(image);
        var name=new TextBlock{TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,0,0,8)};name.SetBinding(TextBlock.TextProperty,new Binding("TargetName"));details.Children.Add(name);
        details.Children.Add(new TextBlock {Text="文件名：原文件名 + 仿色",Margin=new Thickness(0,0,0,8)});
        details.Children.Add(new TextBlock {Text="原始尺寸 · 不缩放\n位置：确认后选择目录",Margin=new Thickness(0,0,0,12)});
        details.Children.Add(new TextBlock {Text="已显影光片导出相框与画面合成；使用完整冻结定义，不导出预览截图。",TextWrapping=TextWrapping.Wrap});
        var status=new TextBlock {TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray,Margin=new Thickness(0,12,0,0)};
        status.SetBinding(TextBlock.TextProperty,new Binding("ExportStatus")); details.Children.Add(status);
        var failures=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Salmon};failures.SetBinding(TextBlock.TextProperty,new Binding("ExportFailureSummary"));details.Children.Add(failures); var detailScroll=new ScrollViewer{Content=details,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(detailScroll,1);Grid.SetRow(detailScroll,1);grid.Children.Add(detailScroll);
        var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        Grid.SetRow(actions,2); Grid.SetColumnSpan(actions,2);
        var cancel=new Button {Content="取消",IsCancel=true,Padding=new Thickness(14,5,14,5),Margin=new Thickness(0,0,8,0)};
        cancel.Click+=(_,_)=>Close();
        var go=new Button {Content="导出所选",Padding=new Thickness(14,5,14,5),Background=new SolidColorBrush(Color.FromRgb(74,118,108)),Foreground=Brushes.White};
        go.Click+=async(_,_)=>
        {
            if(!vm.ExportSelectedCommand.CanExecute(null))return;
            go.IsEnabled=false; cancel.IsEnabled=false;settings.IsEnabled=false;
            try {await vm.ExportSelectedCommand.ExecuteAsync(null);}
            finally {go.IsEnabled=true;cancel.IsEnabled=true;settings.IsEnabled=true;}
        };
        var stop=new Button{Content="停止",Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,0,8,0),Command=vm.StopExportCommand};actions.Children.Add(stop); actions.Children.Add(cancel); actions.Children.Add(go); grid.Children.Add(actions); Content=grid;
    }
}
