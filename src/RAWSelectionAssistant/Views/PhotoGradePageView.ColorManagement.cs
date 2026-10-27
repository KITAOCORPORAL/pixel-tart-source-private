using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
 private void OpenColorManagement(object sender,RoutedEventArgs e)
 {
  if(model is null)return;var vm=model;var panel=new StackPanel{Margin=new Thickness(16)};var window=new Window{Title="颜色管理 · 显式新变体",Width=540,Height=570,Owner=Window.GetWindow(this),Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},DataContext=vm};
  var status=new TextBlock{TextWrapping=TextWrapping.Wrap};status.SetBinding(TextBlock.TextProperty,new Binding("ColorManagementStatus"));panel.Children.Add(status);
  panel.Children.Add(new TextBlock{Text="输入ICC → V1 encoded sRGB / V2 线性ProPhoto D50 → 输出ICC",Margin=new Thickness(0,12,0,8),ToolTip="V1兼容sRGB；V2仅线性曝光/Normal蒙版/几何，其他工具未适配；不静默迁移旧项目"});
  panel.Children.Add(new TextBlock{Text="输出目标（实际转换像素并嵌入ICC）"});var output=new ComboBox{ItemsSource=vm.RgbOutputSpaces};output.SetBinding(ComboBox.SelectedItemProperty,new Binding("ManagedOutputSpace"){Mode=BindingMode.TwoWay});panel.Children.Add(output);
  var convert=new Button{Content="依嵌入ICC创建颜色管理变体",Margin=new Thickness(0,8,0,4)};convert.Click+=async(_,_)=>{convert.IsEnabled=false;try{await vm.CreateManagedColorVariantAsync();}finally{convert.IsEnabled=true;}};panel.Children.Add(convert);var import=new Button{Content="导入带ICC照片到颜色变体"};import.Click+=async(_,_)=>{var file=new Microsoft.Win32.OpenFileDialog{Filter="RGB照片|*.jpg;*.jpeg;*.png;*.tif;*.tiff"};if(file.ShowDialog(window)==true){await vm.LoadTargetAsync(file.FileName);await vm.CreateManagedColorVariantAsync();}};panel.Children.Add(import);
  var wide=new Button{Content="新建线性ProPhoto D50 V2（仅曝光/蒙版/几何）",ToolTip="显式float32工作空间；其他旧sRGB工具拒绝，不暗迁当前变体"};wide.Click+=async(_,_)=>{wide.IsEnabled=false;try{await vm.CreateManagedColorVariantAsync(linearWide:true);}finally{wide.IsEnabled=true;}};panel.Children.Add(wide);
  var update=new Button{Content="转换到目标配置（当前颜色变体输出）"};update.Click+=(_,_)=>vm.SetManagedOutputProfile();panel.Children.Add(update);
  panel.Children.Add(new TextBlock{Text="指定配置：仅重解释原始数值，再转换到编辑域",Margin=new Thickness(0,12,0,4)});var assign=new ComboBox{ItemsSource=vm.RgbOutputSpaces};assign.SetBinding(ComboBox.SelectedItemProperty,new Binding("AssignedInputSpace"){Mode=BindingMode.TwoWay});panel.Children.Add(assign);
  var go=new Button{Content="明确指定此输入ICC并另建变体"};go.Click+=async(_,_)=>{go.IsEnabled=false;try{await vm.CreateManagedColorVariantAsync(true);}finally{go.IsEnabled=true;}};panel.Children.Add(go);
  var message=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)};message.SetBinding(TextBlock.TextProperty,new Binding("StatusText"));panel.Children.Add(message);var display=new CheckBox{Content="显式 SDR 显示ICC转换（确认系统未再次执行同一转换）",Margin=new Thickness(0,10,0,6),ToolTip="默认关闭；HDR/系统ACM未验证。文档sRGB→显示器设备RGB，未带ICC位图进入WPF；不影响照片或导出"};display.SetBinding(CheckBox.IsCheckedProperty,new Binding("ExplicitSdrDisplayColor"){Mode=BindingMode.TwoWay});panel.Children.Add(display);var monitor=new Button{Content="手动指定显示器ICC（留空用Windows关联）"};monitor.Click+=(_,_)=>{var picker=new Microsoft.Win32.OpenFileDialog{Filter="RGB ICC|*.icc;*.icm"};if(picker.ShowDialog(window)==true){try{vm.SetManualMonitorProfile(picker.FileName);}catch(Exception error){MessageBox.Show(window,error.Message,"显示ICC未应用");}}};panel.Children.Add(monitor);var auto=new Button{Content="恢复Windows显示器关联"};auto.Click+=(_,_)=>vm.SetManualMonitorProfile(null);panel.Children.Add(auto);window.ShowDialog();
 }
}
