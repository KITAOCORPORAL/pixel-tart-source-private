using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

// DEV-only gallery and software layout audit. Never registered in product navigation.
internal static class StudioVisualEvidence
{
    internal static async Task MeasureOperation(string output, string name, Func<Task> action, Func<bool> feedback)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew(); double? feedbackAt=null; var beats=0;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>{beats++;if(feedbackAt is null && feedback())feedbackAt=clock.Elapsed.TotalMilliseconds;};
        timer.Start();
        try {var task=action();if(feedback())feedbackAt=clock.Elapsed.TotalMilliseconds;await task;}
        finally {
            timer.Stop();clock.Stop();Directory.CreateDirectory(output);
            File.AppendAllText(Path.Combine(output,"loading-timing.jsonl"),JsonSerializer.Serialize(new {ProductSourceSha=Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA")??"UNFROZEN_WORKTREE",Operation=name,ElapsedMs=clock.Elapsed.TotalMilliseconds,FeedbackStartMs=feedbackAt,DispatcherHeartbeats=beats,Responsive=beats>0?"OBSERVED":"TOO_SHORT_OR_NO_HEARTBEAT",SyntheticData=true})+Environment.NewLine);
        }
    }
    internal static IEnumerable<T> Walk<T>(DependencyObject root) where T:DependencyObject {
        if(root is T t) yield return t;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in Walk<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    internal static void Png(FrameworkElement view,string path,double scale=1) {
        view.UpdateLayout(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(view.ActualWidth*scale)),Math.Max(1,(int)Math.Ceiling(view.ActualHeight*scale)),96*scale,96*scale,PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path); encoder.Save(file);
    }
    internal static async Task Gallery(Window owner,string output) {
        var groups=new[]{"01_colors","02_buttons","03_inputs","04_segmented","05_sliders","06_toggles","07_popups","08_dialogs","09_loading","10_symbols","11_panels","12_focus"};
        foreach(var group in groups) {
            var panel=new WrapPanel{Margin=new Thickness(32)};
            var stack=new StackPanel{Background=(Brush)owner.FindResource("AppBackgroundBrush")}; stack.Children.Add(new TextBlock{Text="Pixel Tart Studio UI · "+group,FontSize=24,Margin=new Thickness(32,24,32,0)}); stack.Children.Add(panel);
            var host=new Window{Owner=owner,Title="Studio UI Gallery (test only)",Width=1100,Height=760,Content=stack,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            void Add(FrameworkElement e) {e.Margin=new Thickness(8);panel.Children.Add(e);}
            Style Style(string key)=>(Style)owner.FindResource(key);
            if(group=="01_colors") foreach(var key in new[]{"AppBackgroundBrush","SurfacePrimaryBrush","SurfaceSecondaryBrush","SurfaceElevatedBrush","SurfaceHoverBrush","DividerBrush","TextPrimaryBrush","TextSecondaryBrush","AccentBrush"}) { var tile=new StackPanel{Width=220};tile.Children.Add(new Border{Height=96,Background=(Brush)owner.FindResource(key)});tile.Children.Add(new TextBlock{Text=key,Margin=new Thickness(0,8,0,0)});Add(tile); }
            if(group is "02_buttons" or "12_focus") foreach(var key in new[]{"Primary","Secondary","Ghost","Danger","Icon"}) foreach(var enabled in new[]{true,false}) Add(new Button{Content=key=="Icon"?"+":enabled?"选择照片":"暂不可用",Style=Style("PixelTart.Button."+key),IsEnabled=enabled});
            if(group=="03_inputs"){Add(new TextBox{Text="项目名称 · 秋日窗光",Width=280});Add(new ComboBox{ItemsSource=new[]{"主视觉","灯光参考","服化道"},SelectedIndex=0,Width=240});Add(new DatePicker{SelectedDate=DateTime.Today,Width=220});}
            if(group=="04_segmented") Add(new ListBox{Style=Style("PixelTart.Segmented"),ItemsSource=new[]{"原片","仿色结果","左右对比","并排对比"},SelectedIndex=2});
            if(group=="05_sliders")foreach(var value in new[]{0d,25,70,100})Add(new Slider{Style=Style("PixelTart.Slider"),Minimum=0,Maximum=100,Value=value,Width=220});
            if(group=="06_toggles")foreach(var enabled in new[]{true,false})foreach(var check in new[]{true,false})Add(new CheckBox{Style=Style("PixelTart.Toggle"),Content="保持原片影调",IsChecked=check,IsEnabled=enabled,Width=230});
            if(group=="07_popups"){var menu=new ContextMenu{Style=Style("PixelTart.Menu.Context")};foreach(var label in new[]{"查看大图","用于灯光","用于造型","从策划中移除"})menu.Items.Add(new MenuItem{Header=label});var b=new Button{Content="参考图片菜单",ContextMenu=menu};Add(b);}
            if(group=="08_dialogs"){var sheet=new StackPanel();sheet.Children.Add(new TextBlock{Text="保存当前调整",FontSize=24});sheet.Children.Add(new TextBlock{Text="仅保存色彩方案，源照片保持不变。",Margin=new Thickness(0,16,0,24)});sheet.Children.Add(new Button{Content="保存为色彩方案",Style=Style("PixelTart.Button.Primary")});Add(new Border{Style=Style("PixelTart.Sheet"),Child=sheet,Width=620});}
            if(group=="09_loading"){Add(new StudioLoadingRing());Add(new ProgressBar{Style=Style("PixelTart.ProgressBar"),Width=260,Value=62});Add(new ProgressBar{Style=Style("PixelTart.ProgressBar"),Width=260,IsIndeterminate=true});Add(new Border{Style=Style("PixelTart.LoadingOverlay"),Child=new TextBlock{Text="正在生成预览…"}});}
            if(group=="10_symbols") {
                // Resource geometries are the same ones used in production.
                foreach(var name in new[]{"Workbench","Library","Ingest","Calendar","Planning","Tether","Selection","Finance","History","Toolbox","ReferenceColor","Publish","RawJpg","Organize","Collage","Search","Filter","Sort","Grid","Waterfall","List","More","Add","Close","Forward","Back","Refresh","Preview","Lighting","Pose","Storyboard","Styling","Palette","Import","Export","Link"}) {
                    var row=new StackPanel{Width=140};var icons=new StackPanel{Orientation=Orientation.Horizontal};
                    foreach(var size in new[]{16d,20,24})icons.Children.Add(new System.Windows.Shapes.Path{Data=(Geometry)owner.FindResource("PixelTart.Symbol."+name),Style=Style("PixelTart.Symbol"),Width=size,Height=size,Margin=new Thickness(5)});
                    row.Children.Add(icons);row.Children.Add(new TextBlock{Text=name,Margin=new Thickness(5)});Add(row);
                }
            }
            if(group=="11_panels")foreach(var name in new[]{"Panel","Sheet","Inspector"})Add(new Border{Style=Style("PixelTart."+name),Width=280,Height=340,Child=new TextBlock{Text=name+"\n\n图片信息\n\n1200 × 800\n\n拍摄 / 来源",TextWrapping=TextWrapping.Wrap}});
            host.Show();host.UpdateLayout();await Task.Delay(100);
            if(group=="12_focus") Walk<Button>(host).First().Focus();
            if(group=="07_popups"){var button=Walk<Button>(host).First();button.ContextMenu!.PlacementTarget=button;button.ContextMenu.IsOpen=true;await Task.Delay(150);Png(button.ContextMenu,Path.Combine(output,"components","07_popups.png"));button.ContextMenu.IsOpen=false;}
            else Png((FrameworkElement)host.Content,Path.Combine(output,"components",group+".png"));
            host.Close();
        }
    }
    internal static void ContactSheet(string output, string name, IEnumerable<string> paths)
    {
        var files=paths.ToArray(); const int columns=3, cellWidth=640, cellHeight=390;
        var height=(int)Math.Ceiling(files.Length/(double)columns)*cellHeight;
        var drawing=new DrawingVisual();
        using(var dc=drawing.RenderOpen()) {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(16,17,19)),null,new Rect(0,0,columns*cellWidth,height));
            for(var i=0;i<files.Length;i++) {
                var bitmap=new BitmapImage(new Uri(Path.GetFullPath(files[i])));
                var scale=Math.Min(1,Math.Min(616d/bitmap.PixelWidth,340d/bitmap.PixelHeight));
                var x=i%columns*cellWidth+12;var y=i/columns*cellHeight+12;
                dc.DrawText(new FormattedText(Path.GetRelativePath(output,files[i]),System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI, Microsoft YaHei UI"),13,Brushes.White,1),new Point(x,y));
                dc.DrawImage(bitmap,new Rect(x,y+28,bitmap.PixelWidth*scale,bitmap.PixelHeight*scale));
            }
        }
        var sheet=new RenderTargetBitmap(columns*cellWidth,height,96,96,PixelFormats.Pbgra32);sheet.Render(drawing);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(sheet));
        Directory.CreateDirectory(Path.Combine(output,"contact-sheets"));
        using var file=File.Create(Path.Combine(output,"contact-sheets",name+".png"));encoder.Save(file);
    }
    internal static async Task Dpi(Window window,RAWSelectionAssistant.ViewModels.MainViewModel vm,string output) {
        var findings=new List<object>();var captures=new List<object>();int textCount=0;
        foreach(var route in new[]{"AssetLibrary","Planning","ReferenceColor","Tether"}) {
            vm.NavigateCommand.Execute(route);
            if(route=="Planning")vm.PlanningPage!.ContentPage="文字";
            foreach(var (width,height,scale) in new[]{(1920,1080,1d),(1920,1080,1.25),(1920,1080,1.5),(1920,1080,2d),(2560,1440,1d),(3840,2160,1d)}) {
                window.MinWidth=0;window.MinHeight=0;window.Width=width/scale;window.Height=height/scale;
                await window.Dispatcher.InvokeAsync(window.UpdateLayout,DispatcherPriority.ApplicationIdle);await Task.Delay(150);
                // Arrange the production client tree explicitly. Native window sizing is
                // capped by the current monitor, so Width=3840 alone is not a 4K render.
                var root=(FrameworkElement)window.Content;
                root.Width=width/scale;root.Height=height/scale;
                root.Measure(new Size(root.Width,root.Height));
                root.Arrange(new Rect(0,0,root.Width,root.Height));root.UpdateLayout();
                if(Math.Abs(root.ActualWidth*scale-width)>.5 || Math.Abs(root.ActualHeight*scale-height)>.5)
                    throw new InvalidOperationException("Production client render dimensions do not match requested pixels");
                var path=Path.Combine(output,"dpi",route+"-"+width+"x"+height+"-"+(int)(scale*100)+".png");Png(root,path,scale);
                captures.Add(new{Route=route,RequestedPixels=new[]{width,height},LogicalScale=scale,ActualDip=new[]{root.ActualWidth,root.ActualHeight},Filename=Path.GetRelativePath(output,path)});
                foreach(var text in Walk<TextBlock>(root).Where(t=>t.IsVisible && t.ActualWidth>0 && !string.IsNullOrWhiteSpace(t.Text))) {
                    textCount++;
                    if(text.TextWrapping!=TextWrapping.NoWrap || text.TextTrimming!=TextTrimming.None)continue;
                    var measured=new FormattedText(text.Text,System.Globalization.CultureInfo.CurrentCulture,text.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,Brushes.Black,scale);
                    if(measured.WidthIncludingTrailingWhitespace>text.ActualWidth-text.Padding.Left-text.Padding.Right+2)
                        findings.Add(new{Route=route,Scale=scale,Text=text.Text,Available=text.ActualWidth,Desired=measured.WidthIncludingTrailingWhitespace,Kind="TEXT_DESIRED_WIDTH_EXCEEDS_ARRANGED_WIDTH"});
                }
            }
        }
        File.WriteAllText(Path.Combine(output,"TEXT_OVERFLOW_GEOMETRY_AUDIT.json"),JsonSerializer.Serialize(new{ProductSourceSha=Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA")??"UNFROZEN_WORKTREE",Status=findings.Count==0?"PASS_RECORDED_UNWRAPPED_TEXT":"FAIL",PhysicalDpi="NOT_TESTED",Scope="Four core pages; visible unwrapped non-ellipsized TextBlocks only. Not all popups or control content.",Inspected=textCount,Findings=findings,Captures=captures},new JsonSerializerOptions{WriteIndented=true}));
        ((FrameworkElement)window.Content).Width=double.NaN;
        ((FrameworkElement)window.Content).Height=double.NaN;
        window.Width=1920;window.Height=1080;
    }
}
