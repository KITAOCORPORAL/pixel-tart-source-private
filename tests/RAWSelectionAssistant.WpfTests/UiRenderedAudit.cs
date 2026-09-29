using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

internal static class UiRenderedAudit
{
    internal static object Run(FrameworkElement root, string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        var frame = BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
        var bitmap = new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);
        var width=bitmap.PixelWidth; var height=bitmap.PixelHeight;
        var bytes=new byte[width*height*4]; bitmap.CopyPixels(bytes,width*4,0);
        var masked=new bool[width*height];
        var masks=new List<object>();
        foreach(var e in StudioVisualEvidence.Walk<FrameworkElement>(root).Where(e=>e.IsVisible && (e is Image or ColorSpace3DViewport or ColorStudioImageViewport || e.GetType().Name.Contains("Histogram"))))
        {
            var b=Bounds(e,root); masks.Add(new{Element=e.GetType().Name,e.Name,Bounds=b.ToString()});
            for(var y=Math.Max(0,(int)b.Top);y<Math.Min(height,(int)Math.Ceiling(b.Bottom));y++)
            for(var x=Math.Max(0,(int)b.Left);x<Math.Min(width,(int)Math.Ceiling(b.Right));x++) masked[y*width+x]=true;
        }
        var bright=new bool[width*height]; var eligible=0; var count=0;
        for(var i=0;i<bright.Length;i++)
        {
            if(masked[i])continue; eligible++;
            bright[i]=bytes[i*4]>244 && bytes[i*4+1]>244 && bytes[i*4+2]>244;
            if(bright[i]) count++;
        }
        var largest=0; var queue=new Queue<int>();
        for(var i=0;i<bright.Length;i++)
        {
            if(!bright[i])continue; bright[i]=false; queue.Enqueue(i);var area=0;
            while(queue.Count>0)
            {
                var p=queue.Dequeue();area++;var x=p%width;var y=p/width;
                if(x>0)Visit(p-1);if(x+1<width)Visit(p+1);if(y>0)Visit(p-width);if(y+1<height)Visit(p+width);
            }
            largest=Math.Max(largest,area);
        }
        void Visit(int p) { if(bright[p]){bright[p]=false;queue.Enqueue(p);} }
        var contrast=new List<object>();
        foreach(var e in StudioVisualEvidence.Walk<FrameworkElement>(root).Where(e=>e.IsVisible && e.ActualWidth>0 && e.ActualHeight>0))
        {
            Brush? fg=null; double size=13; bool bold=false; string? value=null;
            if(e is TextBlock t){fg=t.Foreground;size=t.FontSize;bold=t.FontWeight.ToOpenTypeWeight()>=700;value=t.Text;}
            else if(e is TextBox tbox){fg=tbox.Foreground;size=tbox.FontSize;value=tbox.Text;}
            else continue;
            if(string.IsNullOrWhiteSpace(value))continue;
            var bg=Background(e); var target=size>=24 || (bold && size>=18.667)?3d:4.5;
            double? ratio=null;
            if(fg is SolidColorBrush f && f.Opacity==1 && f.Color.A==255 && bg is {} b && e.Opacity==1)
                ratio=Contrast(f.Color,b);
            contrast.Add(new {Element=e.GetType().Name,e.Name,Text=value,Ratio=ratio,Required=target,Status=ratio is null?"REVIEW_REQUIRED":ratio>=target?"PASS":"FAIL"});
        }
        object workspace;
        var color=StudioVisualEvidence.Walk<ReferenceColorWorkspaceView>(root).FirstOrDefault(e=>e.IsVisible);
        if(color is null) workspace=new {Status="NOT_APPLICABLE",Reason="No Color Studio inspector/canvas contract on this route; shell navigation alone is not an inspector."};
        else
        {
            var grid=(FrameworkElement)color.FindName("WorkspaceGrid");var left=(FrameworkElement)color.FindName("LeftRail");var right=(FrameworkElement)color.FindName("RightRail");var canvas=(FrameworkElement)color.FindName("PreviewCanvas");
            var w=grid.ActualWidth;var inspector=right.IsVisible?right.ActualWidth:0;var navigation=left.IsVisible?left.ActualWidth:0;
            workspace=new {Status=w<=0?"REVIEW_REQUIRED":inspector/w<=.28&&(navigation+inspector)/w<=.38?"PASS":"FAIL",Workspace=w,Inspector=inspector,Navigation=navigation,InspectorRatio=w>0?inspector/w:0,CombinedRatio=w>0?(navigation+inspector)/w:0,CanvasWidth=canvas.ActualWidth,Scope="Color Studio workspace LeftRail + RightRail; overlay width reported as occupied width"};
        }
        return new { Luminance=new{Status=eligible==0?"REVIEW_REQUIRED":largest/(double)eligible>.01?"BRIGHT_SURFACE_ANOMALY":"PASS",BrightPixelRatio=eligible>0?count/(double)eligible:0,LargestBrightRegionAreaRatio=eligible>0?largest/(double)eligible:0,EligiblePixels=eligible,ExcludedRegions=masks,NearWhiteChannelMinimum=245,AnomalyRegionThreshold=.01},Contrast=contrast,Workspace=workspace };
    }
    internal static Rect Bounds(FrameworkElement e,FrameworkElement root)=>e.TransformToAncestor(root).TransformBounds(new Rect(e.RenderSize));
    private static Color? Background(FrameworkElement e)
    {
        for(DependencyObject? p=e;p!=null;p=VisualTreeHelper.GetParent(p))
        {
            if(p is FrameworkElement element && element.Opacity!=1)return null;
            var brush=p switch{Border b=>b.Background,Panel panel=>panel.Background,Control c=>c.Background,TextBlock t=>t.Background,_=>null};
            if(brush is null || brush is SolidColorBrush{Color.A:0})continue;
            if(brush is SolidColorBrush s && s.Opacity==1 && s.Color.A==255)return s.Color;
            return null;
        }
        return null;
    }
    internal static double Contrast(Color a,Color b)
    {
        static double Linear(byte c){var v=c/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
        static double L(Color c)=>.2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);
        var x=L(a);var y=L(b);return(Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
}
