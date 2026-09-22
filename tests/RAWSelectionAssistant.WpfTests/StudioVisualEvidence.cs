using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

// DEV-only gallery and software layout audit. Never registered in product navigation.
internal static class StudioVisualEvidence
{
    internal static void AssertNoShellCloseCollision(FrameworkElement root, string state, string output)
    {
        var close = Walk<SurfaceCloseButton>(root).FirstOrDefault(element =>
            element.IsVisible && element.IsHitTestVisible &&
            (element.Name == "ShellSurfaceCloseButton" || AutomationProperties.GetAutomationId(element) == "ShellEmergencyCloseButton"));
        if (close is null) return;
        var closeRect = close.TransformToAncestor(root).TransformBounds(new Rect(close.RenderSize));
        var collisions = new List<object>();
        foreach (var element in Walk<FrameworkElement>(root).Where(IsHeaderCollisionCandidate))
        {
            if (ReferenceEquals(element, close) || IsDescendantOf(element, close)) continue;
            Rect bounds;
            try { bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize)); }
            catch (InvalidOperationException) { continue; }
            if (!bounds.IntersectsWith(closeRect)) continue;
            collisions.Add(new
            {
                Type = element.GetType().Name,
                Name = element.Name,
                AutomationId = AutomationProperties.GetAutomationId(element),
                Text = element is TextBlock text ? text.Text : element is ContentControl content ? content.Content?.ToString() : null,
                Bounds = new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height }
            });
        }
        Directory.CreateDirectory(output);
        File.AppendAllText(Path.Combine(output, "close-collision-runtime.jsonl"), JsonSerializer.Serialize(new
        {
            ProductSourceSha = Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA") ?? "UNFROZEN_WORKTREE",
            State = state,
            CloseBounds = new[] { closeRect.X, closeRect.Y, closeRect.Width, closeRect.Height },
            CollisionCount = collisions.Count,
            Collisions = collisions
        }) + Environment.NewLine);
        if (collisions.Count > 0) throw new InvalidOperationException($"Shell close collision at {state}: {JsonSerializer.Serialize(collisions)}");

        static bool IsHeaderCollisionCandidate(FrameworkElement element) =>
            element.IsVisible && element.IsEnabled && element.IsHitTestVisible && element.ActualWidth > 0 && element.ActualHeight > 0 &&
            element is ButtonBase or Selector or TextBoxBase or TextBlock;
        static bool IsDescendantOf(DependencyObject element, DependencyObject ancestor)
        {
            for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
                if (ReferenceEquals(current, ancestor)) return true;
            return false;
        }
    }

    internal static void AuditGeometry(FrameworkElement root, string state, double scale, string output)
    {
        var rows = new List<object>();
        foreach (var text in Walk<TextBlock>(root).Where(t => t.IsVisible && t.ActualWidth > 0 && !string.IsNullOrWhiteSpace(t.Text)))
        {
            var width = Math.Max(1, text.ActualWidth - text.Padding.Left - text.Padding.Right);
            var measured = new FormattedText(text.Text, System.Globalization.CultureInfo.CurrentCulture, text.FlowDirection,
                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, Brushes.Black, scale);
            if (text.TextWrapping != TextWrapping.NoWrap) measured.MaxTextWidth = width;
            if (!double.IsNaN(text.LineHeight)) measured.LineHeight = text.LineHeight;
            var own = new Rect(0, 0, text.ActualWidth, text.ActualHeight);
            var bounds = text.TransformToAncestor(root).TransformBounds(own);
            var clipped = false; var scrollable = false; string? control = null;
            for (DependencyObject? parent = VisualTreeHelper.GetParent(text); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is ScrollViewer) scrollable = true;
                if (parent is Control c && control is null) control = c.GetType().Name;
                if (parent is FrameworkElement e && (e.ClipToBounds || e is ScrollContentPresenter))
                {
                    var relative = text.TransformToAncestor(e).TransformBounds(own);
                    if (relative.Left < -2 || relative.Top < -2 || relative.Right > e.ActualWidth + 2 || relative.Bottom > e.ActualHeight + 2) clipped = true;
                }
                if (ReferenceEquals(parent, root)) break;
            }
            var outside = bounds.Left < -2 || bounds.Top < -2 || bounds.Right > root.ActualWidth + 2 || bounds.Bottom > root.ActualHeight + 2;
            var overflow = measured.WidthIncludingTrailingWhitespace > width + 2.1 || measured.Height > text.ActualHeight - text.Padding.Top - text.Padding.Bottom + 3;
            var disposition = scrollable && (outside || clipped) ? "SCROLL_REACHABLE_REQUIRES_INTERACTION_CHECK" :
                text.TextTrimming != TextTrimming.None ? "EXPLICIT_ELLIPSIS" : overflow || outside || clipped ? "REVIEW_REQUIRED" : "WITHIN_RECORDED_BOUNDS";
            rows.Add(new { State = state, LogicalScale = scale, Control = control, text.Text, TextBounds = new { bounds.X, bounds.Y, bounds.Width, bounds.Height }, Desired = new[] { measured.WidthIncludingTrailingWhitespace, measured.Height }, OutsideRoot = outside, AncestorClipped = clipped, ScrollAncestor = scrollable, Disposition = disposition });
        }
        Directory.CreateDirectory(output);
        File.AppendAllText(Path.Combine(output, "geometry-observations.jsonl"), JsonSerializer.Serialize(new { ProductSourceSha = Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA") ?? "UNFROZEN_WORKTREE", State = state, Rows = rows }) + Environment.NewLine);
    }
    internal static async Task MeasureOperation(string output, string name, Func<Task> action, Func<bool> feedback, FrameworkElement? visualRoot = null)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew(); double? feedbackAt=null; double? indicatorAt=null; var beats=0;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>{beats++;if(feedbackAt is null && feedback())feedbackAt=clock.Elapsed.TotalMilliseconds;
            if(indicatorAt is null && visualRoot is not null && Walk<FrameworkElement>(visualRoot).Any(e=>e.IsVisible && PixelTart.Modules.AssetLibrary.StudioBusyFeedback.GetIsVisible(e))) indicatorAt=clock.Elapsed.TotalMilliseconds;};
        timer.Start();
        try {var task=action();if(feedback())feedbackAt=clock.Elapsed.TotalMilliseconds;await task;}
        finally {
            timer.Stop();clock.Stop();Directory.CreateDirectory(output);
            File.AppendAllText(Path.Combine(output,"loading-timing.jsonl"),JsonSerializer.Serialize(new {ProductSourceSha=Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA")??"UNFROZEN_WORKTREE",Operation=name,ElapsedMs=clock.Elapsed.TotalMilliseconds,BusyStateStartMs=feedbackAt,IndicatorStartMs=indicatorAt,IndicatorObserved=visualRoot is not null,DispatcherHeartbeats=beats,Responsive=beats>0?"OBSERVED":"TOO_SHORT_OR_NO_HEARTBEAT",SyntheticData=true})+Environment.NewLine);
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
    internal static async Task AccentComparison(FrameworkElement root, string output)
    {
        var resources = Application.Current.Resources;
        var keys = new[] { "AccentBrush", "ToolAccentBrush", "AccentValueBrush", "PrimaryBrush", "Brush.Accent", "Brush.Accent.Hover", "Brush.Accent.Active", "Brush.Accent.Subtle" };
        var original = keys.ToDictionary(key => key, key => resources.Contains(key) ? resources[key] : Application.Current.TryFindResource(key));
        var directory = Path.Combine(output, "accent-ab"); Directory.CreateDirectory(directory);
        var emerald = Path.Combine(directory, "A_EMERALD.png"); Png(root, emerald);
        var violet = new Dictionary<string, Brush>
        {
            ["AccentBrush"] = new SolidColorBrush(Color.FromRgb(132, 108, 162)), ["ToolAccentBrush"] = new SolidColorBrush(Color.FromRgb(132, 108, 162)),
            ["AccentValueBrush"] = new SolidColorBrush(Color.FromRgb(190, 168, 214)), ["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(132, 108, 162)),
            ["Brush.Accent"] = new SolidColorBrush(Color.FromRgb(132, 108, 162)), ["Brush.Accent.Hover"] = new SolidColorBrush(Color.FromRgb(148, 124, 178)),
            ["Brush.Accent.Active"] = new SolidColorBrush(Color.FromRgb(112, 91, 139)), ["Brush.Accent.Subtle"] = new SolidColorBrush(Color.FromArgb(42, 132, 108, 162))
        };
        try
        {
            foreach (var item in violet) { item.Value.Freeze(); resources[item.Key] = item.Value; }
            await root.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle); await Task.Delay(120);
            Png(root, Path.Combine(directory, "B_MUTED_VIOLET.png"));
        }
        finally
        {
            foreach (var item in original) if (item.Value is not null) resources[item.Key] = item.Value;
            await root.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle);
        }
        var frames = new[] { ("A · Emerald", emerald), ("B · Muted Violet", Path.Combine(directory, "B_MUTED_VIOLET.png")) };
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 18, 21)), null, new Rect(0, 0, 3840, 1080));
            for (var index = 0; index < frames.Length; index++)
            {
                var bitmap = new BitmapImage(new Uri(frames[index].Item2)); var scale = Math.Min(1880d / bitmap.PixelWidth, 1000d / bitmap.PixelHeight);
                var x = index * 1920d + 20; context.DrawText(new FormattedText(frames[index].Item1, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 24, Brushes.White, 1), new Point(x, 12));
                context.DrawImage(bitmap, new Rect(x, 60, bitmap.PixelWidth * scale, bitmap.PixelHeight * scale));
            }
        }
        var sheet = new RenderTargetBitmap(3840, 1080, 96, 96, PixelFormats.Pbgra32); sheet.Render(drawing); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(sheet));
        using var file = File.Create(Path.Combine(directory, "10_ACCENT_AB.png")); encoder.Save(file);
    }
    internal static async Task Dpi(Window window,RAWSelectionAssistant.ViewModels.MainViewModel vm,string output) {
        var findings=new List<object>();var captures=new List<object>();int textCount=0;
        foreach(var route in Environment.GetEnvironmentVariable("PIXEL_TART_GLOBAL_ROLLOUT") == "1" ? new[]{"Workbench","Workflow","Finance","WorkCalendar","Publishing","PhotoGrouping","Collage","AssetLibrary","Planning","ReferenceColor","Tether"} : new[]{"AssetLibrary","Planning","ReferenceColor","Tether"}) {
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
                AssertNoShellCloseCollision(root, route + "/" + width + "x" + height + "/" + (int)(scale * 100), output);
                AuditGeometry(root, route + "/" + width + "x" + height, scale, output);
                if (Environment.GetEnvironmentVariable("PIXEL_TART_GLOBAL_ROLLOUT") == "1") StudioGlobalRolloutEvidence.AuditControlText(root, route + "/" + width + "x" + height + "/" + scale, output);
                if (Environment.GetEnvironmentVariable("PIXEL_TART_GLOBAL_ROLLOUT") == "1" && scale == 2)
                    await StudioGlobalRolloutEvidence.ScrollAndGeometry(root, route + "-200", output);
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
        File.WriteAllText(Path.Combine(output,"TEXT_OVERFLOW_GEOMETRY_AUDIT.json"),JsonSerializer.Serialize(new{ProductSourceSha=Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA")??"UNFROZEN_WORKTREE",Status=findings.Count==0?"PASS_RECORDED_UNWRAPPED_TEXT":"FAIL",PhysicalDpi="NOT_TESTED",Scope=Environment.GetEnvironmentVariable("PIXEL_TART_GLOBAL_ROLLOUT") == "1" ? "Eleven routes across six logical sizes. Separate global-control-text inventory includes template-rendered control text; physical DPI not tested." : "Four core pages; visible unwrapped non-ellipsized TextBlocks only.",Inspected=textCount,Findings=findings,Captures=captures},new JsonSerializerOptions{WriteIndented=true}));
        ((FrameworkElement)window.Content).Width=double.NaN;
        ((FrameworkElement)window.Content).Height=double.NaN;
        window.Width=1920;window.Height=1080;
    }
}
