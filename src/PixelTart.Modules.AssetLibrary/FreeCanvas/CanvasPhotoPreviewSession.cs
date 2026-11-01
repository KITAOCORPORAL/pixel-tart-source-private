using System.IO;using System.Security.Cryptography;using System.Windows.Media;using System.Windows.Media.Imaging;using RAWSelectionAssistant.Core.Services.FreeCanvas;
namespace PixelTart.Modules.AssetLibrary.FreeCanvas;
public sealed record CanvasPhotoPreviewMetrics(int SourceDecodes,int NativeJpegDecodes,int CacheHits,long PeakCacheBytes,long RetainedCacheBytes,int LargestDecodedWidth,int LargestDecodedHeight,string Contract);
internal sealed class CanvasPhotoPreviewSession:IDisposable
{
 internal const string Contract="CanvasJpegNative1024.ArtboardCenters.v3";
 internal sealed record Raster(byte[] Pixels,int Width,int Height);
 readonly Dictionary<string,Raster> cache=[];long cacheBytes,peakCache;int decodes,nativeDecodes,hits,maxW,maxH;
 const long CacheLimit=32L*1024*1024;
 internal CanvasPhotoPreviewMetrics Metrics=>new(decodes,nativeDecodes,hits,peakCache,cacheBytes,maxW,maxH,Contract);
 internal Raster Read(CanvasObject item,int outputW,int outputH,CanvasPreviewBudget budget,CancellationToken token)
 {
  using var stream=CanvasPixelRenderer.OpenSource(item,token);if(stream.Length>64L*1024*1024)throw new InvalidDataException("图片文件超过64MiB预览读取范围，请使用较小副本；旧版本保留。");string identity=item.ContentHash;if(string.IsNullOrEmpty(identity)){using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[81920];int read;while((read=stream.Read(buffer))>0){token.ThrowIfCancellationRequested();digest.AppendData(buffer,0,read);}identity=Convert.ToHexString(digest.GetHashAndReset());stream.Position=0;}var key=(item.PlanningSource is{} owned?RAWSelectionAssistant.Core.Services.Projects.PlanningProjectCopyStorage.Resolve(owned.Bind()):Path.GetFullPath(item.SourcePath))+"/"+identity;
  // A lease is verified on EVERY use, even for a cache hit. No file stamp or thumbnail cache can replace content identity.
  if(cache.TryGetValue(key,out var saved)){hits++;return saved;}
  var header=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation|BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand).Frames[0];int sw=header.PixelWidth,sh=header.PixelHeight;
  if(sw<1||sh<1||sw>16384||sh>16384||(long)sw*sh>64000000)throw new InvalidDataException("照片尺寸超过画布支持范围（最长边16384、6400万像素）。");
  long outputBytes=checked((long)outputW*outputH*32);BitmapSource image;
  stream.Position=0;bool jpeg=stream.ReadByte()==255&&stream.ReadByte()==216;stream.Position=0;
  if((long)sw*sh>4194304&&jpeg){
   if(header.Format.BitsPerPixel>32)throw new NotSupportedException("本轮不支持这张高位深照片，未自动降低精度。");
   EnsureSequentialJpeg(stream); // output + at most 32MiB cache + conservative decoded scratch are admitted before allocation
   image=CanvasJpegNativePreview.Read(stream,sw,sh,budget.MaxWorkingBytes-outputBytes-CacheLimit,token,out var nw,out var nh);decodes++;nativeDecodes++;maxW=Math.Max(maxW,nw);maxH=Math.Max(maxH,nh);
   if(header.ColorContexts is{Count:>0})image=new ColorConvertedBitmap(image,header.ColorContexts[0],new ColorContext(PixelFormats.Bgra32),PixelFormats.Bgra32);
   int orientation=1;if(header.Metadata is BitmapMetadata metadata)foreach(var q in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})try{if(metadata.GetQuery(q) is ushort o&&o is>=1 and<=8)orientation=o;}catch(NotSupportedException){}
   if(orientation!=1){var m=orientation switch{2=>new Matrix(-1,0,0,1,0,0),3=>new Matrix(-1,0,0,-1,0,0),4=>new Matrix(1,0,0,-1,0,0),5=>new Matrix(0,1,1,0,0,0),6=>new Matrix(0,1,-1,0,0,0),7=>new Matrix(0,-1,-1,0,0,0),_=>new Matrix(0,-1,1,0,0,0)};image=new TransformedBitmap(image,new MatrixTransform(m));}
  }else{
   if(outputBytes+(long)sw*sh*32+CacheLimit>budget.MaxWorkingBytes){cache.Clear();cacheBytes=0;}if(outputBytes+(long)sw*sh*32>budget.MaxWorkingBytes)throw new InvalidDataException("此图片格式超过本次预览工作预算；大照片请使用标准8位JPEG副本，旧版本保留。");
   // Same small-image path as v2, including its explicit unsupported RGB48+ICC result.
   image=CanvasPixelRenderer.ReadSource(item,token);decodes++;maxW=Math.Max(maxW,image.PixelWidth);maxH=Math.Max(maxH,image.PixelHeight);
  }
  token.ThrowIfCancellationRequested();var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);var bytes=new byte[checked(converted.PixelWidth*converted.PixelHeight*4)];converted.CopyPixels(bytes,converted.PixelWidth*4,0);token.ThrowIfCancellationRequested();var result=new Raster(bytes,converted.PixelWidth,converted.PixelHeight);
  while(cache.Count>0&&(cache.Count>=8||cacheBytes+bytes.Length>CacheLimit)){var first=cache.Keys.First();cacheBytes-=cache[first].Pixels.Length;cache.Remove(first);}if(bytes.Length<=CacheLimit){cache[key]=result;cacheBytes+=bytes.Length;peakCache=Math.Max(peakCache,cacheBytes);}return result;
 }
 static void EnsureSequentialJpeg(Stream stream){stream.Position=2;while(stream.Position<stream.Length){int marker;do{marker=stream.ReadByte();if(marker<0)throw new InvalidDataException("JPEG文件头不完整。");}while(marker!=255);do{marker=stream.ReadByte();}while(marker==255);if(marker==0xda||marker==0xd9)break;if(marker is 0xd8 or 0x01||marker is>=0xd0 and<=0xd7)continue;int a=stream.ReadByte(),b=stream.ReadByte();int length=(a<<8)|b;if(a<0||b<0||length<2)throw new InvalidDataException("JPEG文件头无效。");if(marker is>=0xc0 and<=0xcf&&marker is not(0xc4 or 0xc8 or 0xcc)){if(marker!=0xc0)throw new NotSupportedException("这张大JPEG使用渐进或其他编码，暂未通过有界内存门禁；请使用标准JPEG副本。旧版本保留。");return;}stream.Seek(length-2,SeekOrigin.Current);}throw new InvalidDataException("未找到受支持的JPEG像素帧。");}
 public void Dispose(){cache.Clear();cacheBytes=0;}
}
