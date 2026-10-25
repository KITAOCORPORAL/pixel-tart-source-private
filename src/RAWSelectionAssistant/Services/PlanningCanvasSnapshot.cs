using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
namespace RAWSelectionAssistant.Services;

public static class PlanningCanvasSnapshot
{
    public static string Render(CanvasDocument document)
    {
        var items = document.Objects;
        if (items.Any(x => !double.IsFinite(x.X) || !double.IsFinite(x.Y) || !double.IsFinite(x.Width) || !double.IsFinite(x.Height) || x.Width <= 0 || x.Height <= 0)) throw new InvalidDataException("画布对象尺寸无效。");
        if (items.Any(x => x.IsImage && x.PlanningSource is null && !File.Exists(x.SourcePath))) throw new IOException("画布源图离线，本次不更新快照；已保存版本仍可查看。");
        var minX = items.Count == 0 ? 0 : items.Min(x => x.X - Math.Max(x.Width,x.Height));
        var minY = items.Count == 0 ? 0 : items.Min(x => x.Y - Math.Max(x.Width,x.Height));
        var maxX = items.Count == 0 ? 800 : items.Max(x => x.X + 2*Math.Max(x.Width,x.Height));
        var maxY = items.Count == 0 ? 600 : items.Max(x => x.Y + 2*Math.Max(x.Width,x.Height));
        var scale = Math.Min(1, 1400 / Math.Max(maxX-minX,maxY-minY));
        var width = Math.Max(1,(int)Math.Ceiling((maxX-minX)*scale)); var height = Math.Max(1,(int)Math.Ceiling((maxY-minY)*scale));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20,23,27)),null,new Rect(0,0,width,height));
            dc.PushTransform(new ScaleTransform(scale,scale)); dc.PushTransform(new TranslateTransform(-minX,-minY));
            foreach (var item in items.OrderBy(x=>x.ZIndex))
            {
                dc.PushTransform(new RotateTransform(item.Rotation,item.X+item.Width/2,item.Y+item.Height/2));
                var rect = new Rect(item.X,item.Y,item.Width,item.Height);
                if (item.IsText)
                    dc.DrawText(new FormattedText(item.Text ?? "", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        new Typeface("Microsoft YaHei"), item.FontSize, new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.TextColor)),1) { MaxTextWidth=Math.Max(1,item.Width), MaxTextHeight=Math.Max(1,item.Height) },new Point(item.X,item.Y));
                else if (item.Palette is { } palette)
                {
                    var total=Math.Max(.0001,palette.Colors.Sum(c=>c.Weight)); var x=rect.X;
                    foreach(var color in palette.Colors.OrderByDescending(c=>c.Weight))
                    {
                        var w=rect.Width*color.Weight/total;
                        dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Hex)),null,new Rect(x,rect.Y,w,rect.Height)); x+=w;
                    }
                }
                else if (item.IsImage && (item.PlanningSource is not null || File.Exists(item.SourcePath)))
                {
                    BitmapSource bitmap;
                    if(item.PlanningSource is {} owned)
                    {var copy=owned.Bind();bitmap=PlanningOwnedReferenceImage.ReadBounded(copy,copy.ProjectId,copy.DocumentId,copy.ReferenceId,1400);}
                    else{var external=new BitmapImage();external.BeginInit();external.CacheOption=BitmapCacheOption.OnLoad;external.DecodePixelWidth=1400;external.UriSource=new Uri(Path.GetFullPath(item.SourcePath));external.EndInit();external.Freeze();bitmap=external;}
                    BitmapSource source = item.Monochrome ? new FormatConvertedBitmap(bitmap,PixelFormats.Gray8,null,0) : bitmap;
                    var crop=item.CropRect.Normalize();
                    dc.PushClip(new RectangleGeometry(rect));
                    dc.PushTransform(new ScaleTransform(item.FlipX?-1:1,item.FlipY?-1:1,rect.X+rect.Width/2,rect.Y+rect.Height/2));
                    dc.DrawImage(source,new Rect(rect.X-crop.X*rect.Width/crop.Width,rect.Y-crop.Y*rect.Height/crop.Height,rect.Width/crop.Width,rect.Height/crop.Height));
                    dc.Pop(); dc.Pop();
                }
                else dc.DrawRectangle(Brushes.LightGray,new Pen(Brushes.Gray,1),rect);
                dc.Pop();
            }
            dc.Pop(); dc.Pop();
        }
        var target = new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); target.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = new MemoryStream(); encoder.Save(stream); return Convert.ToBase64String(stream.ToArray());
    }
}
