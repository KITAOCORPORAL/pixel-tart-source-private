using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;
namespace RAWSelectionAssistant.Services;
/// <summary>Explicit color-managed RGB variant; working encoded sRGB, CPU exact LCMS input/output transforms.</summary>
public static class DocumentColorWorkflowV1
{
 public static (RgbIccIdentityV1 Profile,string Policy) InspectInput(string file,LcmsRgbEngine cms)
 {
  using var stream=File.OpenRead(file);var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
  if(frame.ColorContexts is not{Count:>0})return(cms.StandardProfile("sRGB"),"UntaggedExplicitSrgb");
  if(frame.ColorContexts.Count!=1)throw new InvalidDataException("Ambiguous multiple input profiles; assign explicitly in a new variant");
  using var profile=frame.ColorContexts[0].OpenProfileStream();using var bytes=new MemoryStream();profile.CopyTo(bytes);var result=RgbIccIdentityV1.FromBytes(bytes.ToArray(),"Embedded RGB ICC");cms.Validate(result);return(result,"Embedded");
 }
 public static async Task<ProfessionalRawMaster> ReadAsync(string file,ProfessionalRawDefinition definition,CancellationToken token)
 {
  var state=definition.ColorState!.Validate();definition.Validate();return await Task.Run(()=>{
   token.ThrowIfCancellationRequested();using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);var hash=Convert.ToHexString(SHA256.HashData(stream));stream.Position=0;var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];
   using var cms=new LcmsRgbEngine();if(state.Working.Sha256!=cms.StandardProfile("sRGB").Sha256)throw new NotSupportedException("ColorV1 working edits are encoded sRGB; no fake ProPhoto editing");
   if(state.InputPolicy=="Embedded"){var actual=InspectInput(file,cms);if(actual.Policy!="Embedded"||actual.Profile.Sha256!=state.Input.Sha256)throw new IOException("Embedded ICC identity changed");}
   ushort orientation=1;try{if(frame.Metadata is BitmapMetadata md){var raw=md.GetQuery("/app1/ifd/{ushort=274}")??md.GetQuery("/ifd/{ushort=274}");if(raw is ushort o)orientation=o;}}catch(NotSupportedException){}
   int w=frame.PixelWidth,h=frame.PixelHeight;using var reservation=StudioGlobalResources.ReserveWork(checked((long)w*h*40));var rawPixels=new FormatConvertedBitmap(frame,PixelFormats.Rgba64,null,0);var row=new ushort[w*4];var data=new float[w*h*3];using var transform=cms.CreateTransform(state.Input,state.Working);
   for(int y=0;y<h;y++){token.ThrowIfCancellationRequested();rawPixels.CopyPixels(new System.Windows.Int32Rect(0,y,w,1),row,w*8,0);var rgb=new float[w*3];for(int x=0;x<w;x++)for(int c=0;c<3;c++)rgb[x*3+c]=row[x*4+c]/65535f;var converted=transform.Convert(rgb,token);for(int x=0;x<w;x++){double alpha=row[x*4+3]/65535d;for(int c=0;c<3;c++)data[(y*w+x)*3+c]=(float)(converted[x*3+c]*alpha+1-alpha);}}
   // RGB V1 explicitly flattens alpha over white in working encoding, as prior rendered input did.
   var image=new HighBitDepthImageBuffer(w,h,data,"16","sRGB",orientation);var contract=new ProfessionalRawContract("DocumentColorLcms212V1","Rendered","Raster",hash,definition.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB",Characterization:"Explicit embedded/assigned ICC converted once by LCMS; not sensor RAW or calibrated monitor");return new ProfessionalRawMaster(image,contract);
  },token);
 }
 public static async Task<ProfessionalOutputInspection> WriteAsync(ProfessionalRawMaster master,ProfessionalRawDefinition definition,string destination,StudioExportFormat format,CancellationToken token)
 {
  if(format==StudioExportFormat.Jpeg)throw new NotSupportedException("颜色管理V1目前只放行TIFF16/PNG16；宽色域JPEG编码读回未通过固定误差门槛，未发布或放宽预算。");var state=definition.ColorState!.Validate();ProfessionalRawPipeline.ValidateMaster(master,definition);if(!Enum.IsDefined(format))throw new NotSupportedException("Unknown output format");if(File.Exists(destination))throw new IOException("Output exists");var size=ProfessionalRawPipeline.OutputSize(master,definition);using var budget=StudioGlobalResources.ReserveWork(checked((long)size.Width*96));var temp=destination+".cms-"+Guid.NewGuid().ToString("N");using var cms=new LcmsRgbEngine();using var transform=cms.CreateTransform(state.Working,state.Output);var prepared=ProfessionalRawLocalRenderer.Active(definition)?new ProfessionalRawLocalRenderer.Prepared(master,definition,token):null;
  void Fill(int y,Memory<byte> bytes){using var pixels=prepared is not null?ProfessionalRawLocalRenderer.Region(master,definition,0,y,size.Width,1,token,prepared):ProfessionalRawPipeline.RenderRegion(master,definition,0,y,size.Width,1,token);var output=transform.Convert(pixels.Rgb32.Span,token);for(int i=0;i<output.Length;i++)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.Span[(i*2)..],Q16(output[i]));}
  try{if(format is StudioExportFormat.Tiff or StudioExportFormat.Source){await AtomicTiffWriter.WriteRowsAsync(temp,size.Width,size.Height,Fill,new(IccProfile:state.Output.Bytes(),Software:"Guangjing DocumentColorV1 / LCMS2.12",Orientation:1),token);}
   else ProfessionalWicRowEncoder.WriteRows(temp,size.Width,size.Height,95,(y,row)=>{var words=new byte[size.Width*6];Fill(y,words);if(format==StudioExportFormat.Png)words.CopyTo(row,0);else for(int x=0;x<size.Width;x++){row[x*3]=Q8(words,x*6+4);row[x*3+1]=Q8(words,x*6+2);row[x*3+2]=Q8(words,x*6);}},token,png:format==StudioExportFormat.Png,outputProfile:state.Output.Bytes());
   var result=Inspect(temp,master,definition,format,token);token.ThrowIfCancellationRequested();File.Move(temp,destination,false);return result;
  }catch{if(File.Exists(temp))File.Delete(temp);throw;}
 }
 private static ushort Q16(float x)=>(ushort)Math.Clamp(Math.Round(x*65535),0,65535);
 private static byte Q8(byte[] a,int offset)=>(byte)Math.Clamp(Math.Round(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(a.AsSpan(offset))/257d),0,255);
 public static ProfessionalOutputInspection Inspect(string file,ProfessionalRawMaster master,ProfessionalRawDefinition definition,StudioExportFormat format,CancellationToken token=default)
 {
  var state=definition.ColorState!.Validate();var size=ProfessionalRawPipeline.OutputSize(master,definition);using var stream=File.OpenRead(file);var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];if(frame.PixelWidth!=size.Width||frame.PixelHeight!=size.Height||frame.ColorContexts is not{Count:1})throw new InvalidDataException("CMS output dimensions/profile missing");using var icc=frame.ColorContexts[0].OpenProfileStream();using var memory=new MemoryStream();icc.CopyTo(memory);if(!memory.ToArray().AsSpan().SequenceEqual(state.Output.Bytes()))throw new InvalidDataException("CMS output ICC differs");
  bool jpeg=format==StudioExportFormat.Jpeg;var image=new FormatConvertedBitmap(frame,jpeg?PixelFormats.Rgb24:PixelFormats.Rgb48,null,0);var bytes=new byte[size.Width*(jpeg?3:6)];using var cms=new LcmsRgbEngine();using var transform=cms.CreateTransform(state.Working,state.Output);var prepared=ProfessionalRawLocalRenderer.Active(definition)?new ProfessionalRawLocalRenderer.Prepared(master,definition,token):null;double sum=0,squares=0;int maximum=0;
  for(int y=0;y<size.Height;y++){token.ThrowIfCancellationRequested();image.CopyPixels(new System.Windows.Int32Rect(0,y,size.Width,1),bytes,bytes.Length,0);using var pixels=prepared is not null?ProfessionalRawLocalRenderer.Region(master,definition,0,y,size.Width,1,token,prepared):ProfessionalRawPipeline.RenderRegion(master,definition,0,y,size.Width,1,token);var expected=transform.Convert(pixels.Rgb32.Span,token);for(int i=0;i<expected.Length;i++){int code=jpeg?(int)Math.Round(Q16(expected[i])/257d):Q16(expected[i]);int actual=jpeg?bytes[i]:System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i*2));int e=Math.Abs(code-actual);sum+=e;squares+=(double)e*e;maximum=Math.Max(maximum,e);}}
  double count=(double)size.Width*size.Height*3,mean=sum/count,rmse=Math.Sqrt(squares/count);if(!jpeg&&maximum!=0||jpeg&&(mean>5||rmse>15))throw new InvalidDataException($"CMS encoded pixels violate frozen transform: mean={mean}, rmse={rmse}, max={maximum}");return new(size.Width,size.Height,jpeg?8:16,1,state.Output.Sha256==cms.StandardProfile("sRGB").Sha256,jpeg?95:null,mean,rmse,maximum){EncoderConfiguration="DocumentColorV1-"+state.Output.Sha256};
 }
}
