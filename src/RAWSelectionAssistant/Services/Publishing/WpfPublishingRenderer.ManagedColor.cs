using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services.Publishing;
public sealed partial class WpfPublishingRenderer
{
 private static bool IsManagedDocumentSource(string path)
 {
  if(!File.Exists(path+".kitao-export.json"))return false;
  var receipt=JsonSerializer.Deserialize<StudioExportReceipt>(File.ReadAllText(path+".kitao-export.json"));
  return receipt?.RecipeSnapshot?.RawDefinition is{ColorState:not null}||receipt?.RecipeSnapshot?.RawDefinition?.InstantInput?.ColorDocument is not null;
 }
 private static string ManagedTarget(string value)=>value switch{"sRGB"=>"sRGB","DisplayP3" or "Display P3"=>"DisplayP3","AdobeRGB" or "Adobe RGB (1998)"=>"AdobeRGB","ProPhotoRGB"=>"ProPhotoRGB",_=>throw new NotSupportedException("未知出版RGB输出配置")};
 private static void RenderManagedDocument(string source,string destination,PublishingOptions options,CancellationToken token)
 {
  options.Validate();if(File.Exists(destination))throw new IOException("Output exists");if(options.OutputFormat==PublishingOutputFormat.Jpeg&&options.OutputBitDepth!=PublishingOutputBitDepth.Eight)throw new NotSupportedException("JPEG实际仅8位");
  using var cms=new LcmsRgbEngine();var sourceInspection=DocumentColorWorkflowV1.InspectInput(source,cms);if(sourceInspection.Policy!="Embedded")throw new InvalidDataException("颜色管理出版源缺少嵌入ICC，未按sRGB猜测。");var sourceProfile=sourceInspection.Profile;
  // A receipt freezes upstream edit identity; the already exported file's actual ICC describes its pixels.
  var receipt=JsonSerializer.Deserialize<StudioExportReceipt>(File.ReadAllText(source+".kitao-export.json"));
  if(receipt is not null&&!string.Equals(receipt.OutputHash,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))),StringComparison.OrdinalIgnoreCase))throw new IOException("上游成片hash改变");
  using var input=DocumentColorWorkflowV1.ReadIndependentAlphaAsync(source,sourceProfile,cms.StandardProfile("sRGB"),receipt?.EditVersionId,token).GetAwaiter().GetResult();input.RequireOpaque();using var upright=input.NormalizeOrientation(token);
  var size=OutputSize(upright.Pixels.Width,upright.Pixels.Height,options.EffectiveDimensions);using var resized=ResizeRgb48(upright.Pixels,size.Width,size.Height,token);
  var data=resized.Rgb32.ToArray();
  if(options.WatermarksEnabled&&options.EffectiveWatermarkLayers.Any(x=>x.Enabled))
  {
   // Explicit legacy-compatible encoded-sRGB composition. WPF produces only a watermark coverage/color layer.
   var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())foreach(var layer in options.EffectiveWatermarkLayers.Where(x=>x.Enabled))DrawLayer(dc,layer,size.Width,size.Height,token,managed:true);
   var overlay=new RenderTargetBitmap(size.Width,size.Height,96,96,PixelFormats.Pbgra32);overlay.Render(drawing);var row=new byte[size.Width*4];
   for(int y=0;y<size.Height;y++){token.ThrowIfCancellationRequested();overlay.CopyPixels(new Int32Rect(0,y,size.Width,1),row,row.Length,0);for(int x=0;x<size.Width;x++){double a=row[x*4+3]/255d;for(int c=0;c<3;c++){int i=(y*size.Width+x)*3+c;data[i]=(float)(data[i]*(1-a)+row[x*4+2-c]/255d);}}}
  }
  var target=cms.StandardProfile(ManagedTarget(options.ColorSpaceProfile));using var result=new DocumentRenderResultV1(new(size.Width,size.Height,data,"32f","sRGB"),cms.StandardProfile("sRGB"),receipt?.EditVersionId,"PublishingEncodedSrgbBilinearV1");using var converted=result.ConvertTo(target,token);var rgb=converted.Pixels.Rgb32.ToArray();
  bool high=options.OutputBitDepth==PublishingOutputBitDepth.Sixteen;
  void Fill(int y,Memory<byte> row){token.ThrowIfCancellationRequested();for(int x=0;x<size.Width*3;x++){ushort code=(ushort)Math.Clamp(Math.Round(rgb[y*size.Width*3+x]*65535),0,65535);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.Span[(x*2)..],code);}}
  if(options.OutputFormat==PublishingOutputFormat.Tiff)AtomicTiffWriter.WriteRowsAsync(destination,size.Width,size.Height,Fill,new(IccProfile:target.Bytes(),Dpi:options.EffectiveDimensions.Dpi,Software:"光镜 Publishing document float32 → encoded sRGB resample/composite → targetICC"),token).GetAwaiter().GetResult();
  else ProfessionalWicRowEncoder.WriteRows(destination,size.Width,size.Height,options.EffectiveDimensions.JpegQuality,(y,row)=>{var words=new byte[size.Width*6];Fill(y,words);if(options.OutputFormat==PublishingOutputFormat.Png&&high)words.CopyTo(row,0);else for(int x=0;x<size.Width;x++)for(int c=0;c<3;c++){int sourceChannel=2-c;row[x*3+c]=(byte)Math.Round(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(words.AsSpan(x*6+sourceChannel*2))/257d);}},token,png:options.OutputFormat==PublishingOutputFormat.Png,highDepth:high,dpi:options.EffectiveDimensions.Dpi,outputProfile:target.Bytes());
 }
 private static string VerifyManagedDocument(string source,string output,PublishingOptions options,CancellationToken token)
 {
  using var stream=File.OpenRead(output);var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];using var cms=new LcmsRgbEngine();var expected=cms.StandardProfile(ManagedTarget(options.ColorSpaceProfile));if(frame.ColorContexts is not{Count:1})throw new InvalidDataException("出版ICC缺失");using var icc=frame.ColorContexts[0].OpenProfileStream();using var bytes=new MemoryStream();icc.CopyTo(bytes);if(!bytes.ToArray().AsSpan().SequenceEqual(expected.Bytes()))throw new InvalidDataException("出版ICC不一致");using var sourceStream=File.OpenRead(source);var input=BitmapDecoder.Create(sourceStream,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];var orientation=ReadOrientation(input);var size=OutputSize(orientation>=5?input.PixelHeight:input.PixelWidth,orientation>=5?input.PixelWidth:input.PixelHeight,options.EffectiveDimensions);if(frame.PixelWidth!=size.Width||frame.PixelHeight!=size.Height||options.OutputBitDepth==PublishingOutputBitDepth.Sixteen&&frame.Format.BitsPerPixel<48)throw new InvalidDataException("出版尺寸/位深不一致");var decoded=new FormatConvertedBitmap(frame,options.OutputBitDepth==PublishingOutputBitDepth.Sixteen?PixelFormats.Rgb48:PixelFormats.Rgb24,null,0);var row=new byte[frame.PixelWidth*(options.OutputBitDepth==PublishingOutputBitDepth.Sixteen?6:3)];for(int y=0;y<frame.PixelHeight;y++){token.ThrowIfCancellationRequested();decoded.CopyPixels(new Int32Rect(0,y,frame.PixelWidth,1),row,row.Length,0);}return JsonSerializer.Serialize(new{mode="DocumentResultStructureIntegrityV1",frame.PixelWidth,frame.PixelHeight,outputIcc=expected.Sha256,resampling="encoded sRGB float32 bilinear",watermark="encoded sRGB coverage compositing",secondRender=false});
 }
}
