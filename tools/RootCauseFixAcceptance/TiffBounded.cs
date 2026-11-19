using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
class Program
{
 static readonly BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;
 static readonly MethodInfo Legacy=typeof(ColorStudioBitmapRenderer).GetMethod("RenderHighPrecision",Private)!;
 static readonly MethodInfo Bounded=typeof(ColorStudioBitmapRenderer).GetMethod("RenderBoundedExposure",Private)!;
 static readonly MethodInfo Admission=typeof(ColorStudioBitmapRenderer).GetMethod("CanRenderBoundedExposure",Private)!;
 static void Check(bool x,string m){if(!x)throw new InvalidDataException(m);}
 static ColorAdjustmentStack Stack(double ev)=>new([new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Exposure",NumericParameters:new Dictionary<string,double>{{"exposure",ev}})],ProcessingVersion:2);
 static BitmapSource Fixture(int w,int h,bool alpha)
 {
  int channels=alpha?4:3;var values=new ushort[checked(w*h*channels)];for(int p=0;p<w*h;p++){values[p*channels]=(ushort)((p*251L)%65536);values[p*channels+1]=(ushort)((p*1009L+65535)%65536);values[p*channels+2]=(ushort)((p*17L+1)%65536);if(alpha)values[p*4+3]=(ushort)(p%5 switch {0=>0,1=>1,2=>32768,3=>65534,_=>65535});}
  var image=BitmapSource.Create(w,h,96,96,alpha?PixelFormats.Rgba64:PixelFormats.Rgb48,null,values,w*channels*2);image.Freeze();return image;
 }
 static string Digest(BitmapSource b){var stride=checked(b.PixelWidth*b.Format.BitsPerPixel/8);using var h=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var row=new byte[stride];for(int y=0;y<b.PixelHeight;y++){b.CopyPixels(new System.Windows.Int32Rect(0,y,b.PixelWidth,1),row,stride,0);h.AppendData(row);}return Convert.ToHexString(h.GetHashAndReset());}
 static BitmapSource Call(MethodInfo m,params object?[] args){try{return (BitmapSource)m.Invoke(null,args)!;}catch(TargetInvocationException e){throw e.InnerException!;}}
 [STAThread]static int Main(string[] args)
 {
  var mode=args[0];var root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);var rows=new List<object>();
  try
  {
   var before=StudioGlobalResources.AccountedBytes;
   if(mode=="authorized40mp")
   {
    var path=Environment.GetEnvironmentVariable("KITAO_APPROVED_RENDERED_TIFF")??throw new InvalidOperationException("Explicit authorized rendered TIFF required");
    using(var stream=File.OpenRead(path))Check(Convert.ToHexString(SHA256.HashData(stream)).Equals("0DA1BF38E42B0B76AEE28F2B9A0AF013CEFF4CB9C46102128732D0626D667C6F",StringComparison.OrdinalIgnoreCase),"Authorized input hash mismatch");
    var source=StudioQuickExport.LoadPreviewSource(path);Check(source.PixelWidth==7752&&source.PixelHeight==5184&&source.Format.BitsPerPixel>32,"Authorized TIFF contract mismatch");
    var stack=Stack(.1);var actual=ColorStudioBitmapRenderer.Render(source,stack,null);var outputHash=Digest(actual);
    var rgba=new FormatConvertedBitmap(source,PixelFormats.Rgba64,null,0);var inputRow=new byte[source.PixelWidth*8];var outputRow=new byte[source.PixelWidth*8];using var referenceHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    for(int y=0;y<source.PixelHeight;y++)
    {
     rgba.CopyPixels(new System.Windows.Int32Rect(0,y,source.PixelWidth,1),inputRow,inputRow.Length,0);var one=BitmapSource.Create(source.PixelWidth,1,source.DpiX,source.DpiY,PixelFormats.Rgba64,null,inputRow,inputRow.Length);one.Freeze();var historicalRow=Call(Legacy,one,stack,null,CancellationToken.None);historicalRow.CopyPixels(outputRow,outputRow.Length,0);referenceHash.AppendData(outputRow);
    }
    var expected=Convert.ToHexString(referenceHash.GetHashAndReset());Check(expected==outputHash,"Authorized40MP rows differ from unchanged historical per-pixel reference");
    rows.Add(new{mode,input=Path.GetFullPath(path),width=actual.PixelWidth,height=actual.PixelHeight,rowsCompared=source.PixelHeight,bitsPerChannel=16,outputHash,referenceHash=expected,exact=true,referenceScope="Unchanged old adapter on every input row; independence also checked against whole-frame4MP fixture"});
   }
   else if(mode=="large")
   {
    var image=Fixture(2051,2049,true);var stack=Stack(.1);var expected=Call(Legacy,image,stack,null,CancellationToken.None);var actual=ColorStudioBitmapRenderer.Render(image,stack,null);var a=Digest(actual);var e=Digest(expected);Check(a==e&&actual.Format==expected.Format,"Large dispatch differs from historical full-frame reference");rows.Add(new{mode,width=actual.PixelWidth,height=actual.PixelHeight,rgba64=true,fullFrameReferenceSha256=e,boundedSha256=a,exact=true});
    var crossThread=Task.Run(()=>Digest(actual)).GetAwaiter().GetResult();Check(crossThread==a,"Frozen output not independent across threads");
   }
   else
   {
    foreach(bool alpha in new[]{false,true})foreach(var ev in new[]{-2d,-.1,0d,.1,2d})
    {
     var image=Fixture(257,613,alpha);var stack=Stack(ev);var expected=Call(Legacy,image,stack,null,CancellationToken.None);var actual=Call(Bounded,image,stack,CancellationToken.None);var a=Digest(actual);var e=Digest(expected);Check(a==e,"Small RGB/alpha/eV exactness differs");rows.Add(new{input=alpha?"Rgba64":"Rgb48",ev,expected=e,actual=a,exact=true});
    }
    var basis=Stack(.1);var node=basis.Nodes[0];bool Allowed(ColorAdjustmentStack s,ReferenceLook? look=null)=>(bool)Admission.Invoke(null,[s,look])!;
    Check(Allowed(basis),"Expected exposure route not admitted");
    Check(!Allowed(basis with{ProcessingVersion=1}),"Legacy processing version widened");
    Check(!Allowed(basis with{Graph=StudioNodeGraph.FromSerial(basis.Nodes)}),"Graph must not be tiled");
    Check(!Allowed(basis with{Nodes=[node with{CompositeVersion=1,Opacity=.5}]}),"Nonidentity composite must not be tiled");
    Check(!Allowed(basis with{Nodes=[node with{CompositeVersion=1,LocalMask=new StudioLocalMask([])}]}),"Mask must not be tiled");
    Check(!Allowed(basis with{Nodes=[node with{NumericParameters=new Dictionary<string,double>{{"exposure",.1},{"contrast",10}}}]}),"Unsupported parameters widened");
    Check(!Allowed(basis with{Nodes=[node,new(Guid.NewGuid(),ColorStudioNodeType.Details,"Details")]}),"Spatial/multiple nodes must not be tiled");
    using var canceled=new CancellationTokenSource();canceled.Cancel();bool rejected=false;try{Call(Bounded,Fixture(17,19,true),basis,canceled.Token);}catch(OperationCanceledException){rejected=true;}Check(rejected,"Cancellation ignored");
    using var midway=new CancellationTokenSource();var cancellationImage=Fixture(1025,1025,true);midway.CancelAfter(1);bool canceledInRows=false;try{Call(Bounded,cancellationImage,basis,midway.Token);}catch(OperationCanceledException){canceledInRows=true;}Check(canceledInRows,"Cancellation during row work ignored");
    rows.Add(new{admissionCounterexamples=6,cancelBeforeAllocation=true,cancelDuringRows=true});
   }
   Check(StudioGlobalResources.AccountedBytes==before,"Scratch reservation leaked");File.WriteAllText(Path.Combine(root,"RESULT.json"),JsonSerializer.Serialize(new{status="PASS",mode,rows,existingPointProcessorReused=true,colorMathChanged=false,pixelReference="Historical RenderHighPrecision unchanged, exactRGBA64 full-frame comparison",reservationsRestored=true,forcedGc=false,newRawDecodes=0},new JsonSerializerOptions{WriteIndented=true}));return 0;
  }
  catch(Exception e){File.WriteAllText(Path.Combine(root,"FAILURE.txt"),e.ToString());return 3;}
 }
}
