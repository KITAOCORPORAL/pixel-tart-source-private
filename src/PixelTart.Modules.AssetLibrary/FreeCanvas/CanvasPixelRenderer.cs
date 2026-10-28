using System.IO;
using System.Globalization;
using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.FreeCanvas;using RAWSelectionAssistant.Core.Services.Projects;
namespace PixelTart.Modules.AssetLibrary.FreeCanvas;
/// <summary>Shared source/brush raster contract. WPF viewport; float-premultiplied document-size PNG export with one final unpremultiply.</summary>
public static class CanvasPixelRenderer
{
 public static BitmapSource ReadSource(CanvasObject item,CancellationToken token=default,int? edge=null)
 {
  using var stream=item.PlanningSource is{} owned?owned.OpenVerified(token):OpenExternal(item,token);var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation|BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand).Frames[0];if(frame.PixelWidth<1||frame.PixelHeight<1||frame.PixelWidth>16384||frame.PixelHeight>16384||(long)frame.PixelWidth*frame.PixelHeight>64000000)throw new InvalidDataException("素材尺寸超过6400万像素/最长边16384预算");BitmapSource source=frame;
  if(edge is>0&&Math.Max(frame.PixelWidth,frame.PixelHeight)>edge){stream.Position=0;var small=new BitmapImage();small.BeginInit();small.CacheOption=BitmapCacheOption.OnLoad;small.CreateOptions=BitmapCreateOptions.IgnoreColorProfile;small.StreamSource=stream;if(frame.PixelWidth>=frame.PixelHeight)small.DecodePixelWidth=edge.Value;else small.DecodePixelHeight=edge.Value;small.EndInit();source=small;}
  if(frame.ColorContexts is{Count:>0})source=new ColorConvertedBitmap(source,frame.ColorContexts[0],new ColorContext(PixelFormats.Bgra32),PixelFormats.Bgra32);
  int orientation=1;if(frame.Metadata is BitmapMetadata metadata)foreach(var query in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})try{if(metadata.GetQuery(query) is ushort o&&o is>=1 and<=8)orientation=o;}catch(NotSupportedException){}
  if(orientation!=1){var matrix=orientation switch{2=>new Matrix(-1,0,0,1,0,0),3=>new Matrix(-1,0,0,-1,0,0),4=>new Matrix(1,0,0,-1,0,0),5=>new Matrix(0,1,1,0,0,0),6=>new Matrix(0,1,-1,0,0,0),7=>new Matrix(0,-1,-1,0,0,0),_=>new Matrix(0,-1,1,0,0,0)};source=new TransformedBitmap(source,new MatrixTransform(matrix));}
  var rgba=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int stride=rgba.PixelWidth*4;var bytes=new byte[stride*rgba.PixelHeight];rgba.CopyPixels(bytes,stride,0);var result=BitmapSource.Create(rgba.PixelWidth,rgba.PixelHeight,96,96,PixelFormats.Bgra32,null,bytes,stride);result.Freeze();return result;
 }
 private static FileStream OpenExternal(CanvasObject item,CancellationToken token){var stream=new FileStream(item.SourcePath,FileMode.Open,FileAccess.Read,FileShare.Read);try{if(!string.IsNullOrEmpty(item.ContentHash)){var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));if(!hash.Equals(item.ContentHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("素材内容已变化，未使用旧缓存。");stream.Position=0;}token.ThrowIfCancellationRequested();return stream;}catch{stream.Dispose();throw;}}
 public static async Task<CanvasObject> ImportAsync(string file,CanvasDocument document,string root,CancellationToken token=default)
 {
  var id=Guid.NewGuid();var copy=await PlanningProjectCopyStorage.ImportCanvasAsync(root,document.CanvasId,id,file,token);var owned=CanvasPlanningSource.From(copy);var draft=new CanvasObject{CanvasId=document.CanvasId,PlanningSource=owned,ContentHash=copy.Sha256,Name=Path.GetFileName(file)};var pixels=ReadSource(draft,token,1024);using var header=owned.OpenVerified(token);var frame=BitmapDecoder.Create(header,BitmapCreateOptions.DelayCreation,BitmapCacheOption.OnDemand).Frames[0];double sourceWidth=frame.PixelWidth,sourceHeight=frame.PixelHeight;if((pixels.PixelWidth>pixels.PixelHeight)!=(sourceWidth>sourceHeight))(sourceWidth,sourceHeight)=(sourceHeight,sourceWidth);double width=Math.Min(sourceWidth,document.PixelWidth*.6);return draft with{SourceWidth=sourceWidth,SourceHeight=sourceHeight,Width=width,Height=width*sourceHeight/sourceWidth};
 }
 public static void DrawBrush(DrawingContext dc,CanvasObject item)
 {
  dc.PushClip(new RectangleGeometry(new Rect(item.X,item.Y,item.Width,item.Height)));dc.PushTransform(new ScaleTransform(item.FlipX?-1:1,item.FlipY?-1:1,item.X+item.Width/2,item.Y+item.Height/2));
  foreach(var stroke in item.BrushStrokes??[]){var rgb=(Color)ColorConverter.ConvertFromString(stroke.Color);double radius=stroke.Size/2;int left=(int)Math.Floor(stroke.Points.Min(p=>p.X)*item.SourceWidth-radius-1),top=(int)Math.Floor(stroke.Points.Min(p=>p.Y)*item.SourceHeight-radius-1),right=(int)Math.Ceiling(stroke.Points.Max(p=>p.X)*item.SourceWidth+radius+1),bottom=(int)Math.Ceiling(stroke.Points.Max(p=>p.Y)*item.SourceHeight+radius+1);left=Math.Max(0,left);top=Math.Max(0,top);right=Math.Min((int)Math.Ceiling(item.SourceWidth),right);bottom=Math.Min((int)Math.Ceiling(item.SourceHeight),bottom);int w=right-left,h=bottom-top;if(w<=0||h<=0)continue;if((long)w*h>64000000)throw new InvalidDataException("画笔栅格范围超出预算。");var pixels=new byte[w*h*4];
   var mask=new StudioLocalMask([new(stroke.Points.Select(p=>new StudioMaskPoint(p.X,p.Y)).ToArray(),radius/Math.Min(item.SourceWidth,item.SourceHeight),1-stroke.Hardness,1,1)]).Compile(item.SourceWidth/item.SourceHeight);
   Parallel.For(0,h,new ParallelOptions{MaxDegreeOfParallelism=4},y=>{for(int x=0;x<w;x++){byte alpha=(byte)Math.Round(mask((left+x+.5)/item.SourceWidth,(top+y+.5)/item.SourceHeight)*stroke.Opacity*255);int i=(y*w+x)*4;pixels[i]=(byte)((rgb.B*alpha+127)/255);pixels[i+1]=(byte)((rgb.G*alpha+127)/255);pixels[i+2]=(byte)((rgb.R*alpha+127)/255);pixels[i+3]=alpha;}});var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Pbgra32,null,pixels,w*4);bitmap.Freeze();dc.DrawImage(bitmap,new Rect(item.X+left/item.SourceWidth*item.Width,item.Y+top/item.SourceHeight*item.Height,w/item.SourceWidth*item.Width,h/item.SourceHeight*item.Height));}
  dc.Pop();dc.Pop();
 }
 public static void DrawObject(DrawingContext dc,CanvasObject item,BitmapSource? source)
 {
  dc.PushTransform(new RotateTransform(item.Rotation,item.X+item.Width/2,item.Y+item.Height/2));dc.PushOpacity(item.Opacity??1);var rect=new Rect(item.X,item.Y,item.Width,item.Height);
  if(item.IsBrush)DrawBrush(dc,item);
  else if(item.IsText){var text=new FormattedText(item.Text??"",CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),item.FontSize,new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.TextColor)),1){MaxTextWidth=item.Width,MaxTextHeight=item.Height};dc.DrawText(text,new(item.X,item.Y));}
  else if(item.Palette is{} palette){double total=Math.Max(.0001,palette.Colors.Sum(c=>c.Weight)),x=item.X;foreach(var color in palette.Colors){double width=item.Width*color.Weight/total;dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Hex)),null,new Rect(x,item.Y,width,item.Height));x+=width;}}
  else if(source is not null){dc.PushClip(new RectangleGeometry(rect));dc.PushTransform(new ScaleTransform(item.FlipX?-1:1,item.FlipY?-1:1,rect.X+rect.Width/2,rect.Y+rect.Height/2));var c=item.CropRect;if(item.Monochrome)source=new FormatConvertedBitmap(source,PixelFormats.Gray8,null,0);dc.DrawImage(source,new(rect.X-c.X*rect.Width/c.Width,rect.Y-c.Y*rect.Height/c.Height,rect.Width/c.Width,rect.Height/c.Height));dc.Pop();dc.Pop();}dc.Pop();dc.Pop();
 }
 private static BitmapSource ObjectRaster(CanvasObject item)
 {
  int w=Math.Max(1,(int)Math.Ceiling(item.IsBrush?item.SourceWidth:item.Width)),h=Math.Max(1,(int)Math.Ceiling(item.IsBrush?item.SourceHeight:item.Height));if((long)w*h>64000000)throw new InvalidDataException("图层栅格超出预算");var local=item with{X=0,Y=0,Width=w,Height=h,Rotation=0,FlipX=false,FlipY=false,Opacity=1};var visual=new DrawingVisual();using(var dc=visual.RenderOpen())DrawObject(dc,local,null);var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
 }
 private static BitmapSource StraightExport(CanvasDocument document,CancellationToken token)
 {
  // All layers use float premultiplied interpolation/compositing before ONE final unpremultiply.
  // WPF text/brush/palette remains in the same document renderer; raster sticker edges never repeatedly quantize alpha.

  int w=document.PixelWidth,h=document.PixelHeight;var result=new float[w*h*4];foreach(var item in document.Objects.Where(o=>!o.Hidden).OrderBy(o=>o.ZIndex)){
   var layerSource=item.IsImage?ReadSource(item,token):ObjectRaster(item);var source=new FormatConvertedBitmap(layerSource,PixelFormats.Bgra32,null,0);int sw=source.PixelWidth,sh=source.PixelHeight;var bytes=new byte[sw*sh*4];source.CopyPixels(bytes,sw*4,0);double rad=-item.Rotation*Math.PI/180,cos=Math.Cos(rad),sin=Math.Sin(rad),cx=item.X+item.Width/2,cy=item.Y+item.Height/2;
   double radius=Math.Sqrt(item.Width*item.Width+item.Height*item.Height)/2;int firstX=Math.Max(0,(int)Math.Floor(cx-radius)),lastX=Math.Min(w,(int)Math.Ceiling(cx+radius)),firstY=Math.Max(0,(int)Math.Floor(cy-radius)),lastY=Math.Min(h,(int)Math.Ceiling(cy+radius));
   for(int y=firstY;y<lastY;y++){token.ThrowIfCancellationRequested();for(int x=firstX;x<lastX;x++){double dx=x+.5-cx,dy=y+.5-cy,u=(dx*cos-dy*sin+item.Width/2)/item.Width,v=(dx*sin+dy*cos+item.Height/2)/item.Height;if(u<0||u>=1||v<0||v>=1)continue;if(item.FlipX)u=1-u;if(item.FlipY)v=1-v;double sx=(item.CropRect.X+u*item.CropRect.Width)*sw-.5,sy=(item.CropRect.Y+v*item.CropRect.Height)*sh-.5;int ix=(int)Math.Floor(sx),iy=(int)Math.Floor(sy);double fx=sx-ix,fy=sy-iy;double sb=0,sg=0,sr=0,sa0=0;for(int py=0;py<2;py++)for(int px=0;px<2;px++){int xx=ix+px,yy=iy+py;if(xx<0||yy<0||xx>=sw||yy>=sh)continue;double weight=(px==0?1-fx:fx)*(py==0?1-fy:fy);int i=(yy*sw+xx)*4;double alpha=bytes[i+3]/255d;sb+=bytes[i]/255d*alpha*weight;sg+=bytes[i+1]/255d*alpha*weight;sr+=bytes[i+2]/255d*alpha*weight;sa0+=alpha*weight;}if(item.Monochrome&&item.IsImage){double gray=.0722*sb+.7152*sg+.2126*sr;sb=sg=sr=gray;}double opacity=item.Opacity??1,sa=sa0*opacity;int o=(y*w+x)*4;result[o]=(float)(sb*opacity+result[o]*(1-sa));result[o+1]=(float)(sg*opacity+result[o+1]*(1-sa));result[o+2]=(float)(sr*opacity+result[o+2]*(1-sa));result[o+3]=(float)(sa+result[o+3]*(1-sa));}}
  }
  var output=new byte[w*h*4];for(int i=0;i<w*h;i++){double a=result[i*4+3];output[i*4+3]=(byte)Math.Clamp(Math.Round(a*255),0,255);for(int c=0;c<3;c++)output[i*4+c]=a==0?(byte)0:(byte)Math.Clamp(Math.Round(result[i*4+c]/a*255),0,255);}var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,output,w*4);bitmap.Freeze();return bitmap;
 }
 public static void ExportPng(CanvasDocument document,string path,CancellationToken token=default)
 {
  CanvasDocumentStore.Validate(document);if(File.Exists(path))throw new IOException("输出已存在，未覆盖。");var straight=StraightExport(document,token);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(straight,null,null,new System.Collections.ObjectModel.ReadOnlyCollection<ColorContext>(new[]{new ColorContext(PixelFormats.Bgra32)})));var full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);var temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){encoder.Save(f);f.Flush(true);}token.ThrowIfCancellationRequested();File.Move(temp,full,false);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
