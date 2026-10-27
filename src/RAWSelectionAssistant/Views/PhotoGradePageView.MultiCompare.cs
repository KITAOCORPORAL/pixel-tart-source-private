using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
    private Window? r53MultiWindow;
    private async void R53MultiCompare(object sender,RoutedEventArgs e)
    {
        if(model is null)return;var workspace=model;var select=new ListBox{ItemsSource=model.R53Variants,DisplayMemberPath="Name",SelectionMode=SelectionMode.Multiple,Height=180};foreach(var v in model.R53Variants.Take(2))select.SelectedItems.Add(v);var root=new DockPanel{Margin=new Thickness(12)};var button=new Button{Content="对比所选2–4个变体"};var close=new Button{Content="退出对比"};var bar=new StackPanel{Orientation=Orientation.Horizontal};bar.Children.Add(button);bar.Children.Add(close);DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);DockPanel.SetDock(select,Dock.Top);root.Children.Add(select);var grid=new Grid();root.Children.Add(grid);var views=new List<ColorStudioImageViewport>();bool syncing=false;Point? pan=null;long request=0;bool closed=false;
        async void Prepare()
        {
            var current=++request;try
            {
                await workspace.PrepareR53VariantComparisonAsync(select.SelectedItems.Cast<RAWSelectionAssistant.Core.Services.Projects.StudioEditVersion>().Select(v=>v.Id).ToArray());if(closed||current!=request)return;grid.Children.Clear();grid.RowDefinitions.Clear();grid.ColumnDefinitions.Clear();views.Clear();var frames=workspace.R53CompareFrames;grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());grid.RowDefinitions.Add(new());if(frames.Count>2)grid.RowDefinitions.Add(new());
                for(int i=0;i<frames.Count;i++)
                {
                    var frame=frames[i];var view=new ColorStudioImageViewport{Original=frame.Image,Mode="原片",SourcePixelSize=new Size(frame.Image.PixelWidth,frame.Image.PixelHeight)};views.Add(view);var panel=new DockPanel();var label=new TextBlock{Text=frame.Name};DockPanel.SetDock(label,Dock.Top);panel.Children.Add(label);panel.Children.Add(view);Grid.SetColumn(panel,i%2);Grid.SetRow(panel,i/2);grid.Children.Add(panel);
                    view.State.Changed+=(_,_)=>{if(syncing)return;syncing=true;try{foreach(var other in views.Where(x=>x!=view))other.State.Follow(view.State);}finally{syncing=false;}};
                    view.MouseWheel+=(_,args)=>{view.State.ZoomAbout(args.GetPosition(view),args.Delta>0?1.15:1/1.15);args.Handled=true;};view.MouseLeftButtonDown+=(_,args)=>{pan=args.GetPosition(view);view.CaptureMouse();};view.MouseMove+=(_,args)=>{if(pan is{} start&&args.LeftButton==MouseButtonState.Pressed){var now=args.GetPosition(view);view.State.PanBy(now-start);pan=now;}};view.MouseLeftButtonUp+=(_,_)=>{pan=null;view.ReleaseMouseCapture();};
                }
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException or System.IO.IOException or NotSupportedException){ToolHint.Text="变体对比未完成："+error.Message;}
        }
        var window=new Window{Title="光镜 · 变体同步对比",Owner=Window.GetWindow(this),Width=1100,Height=780,Content=root,Background=Background};r53MultiWindow?.Close();r53MultiWindow=window;button.Click+=(_,_)=>Prepare();close.Click+=(_,_)=>window.Close();window.Closed+=(_,_)=>{closed=true;request++;workspace.CloseR53VariantComparison();if(r53MultiWindow==window)r53MultiWindow=null;};System.Windows.Automation.AutomationProperties.SetName(select,"选择2至4个对比变体");window.Show();await Task.Yield();if(!closed)Prepare();
    }
}
