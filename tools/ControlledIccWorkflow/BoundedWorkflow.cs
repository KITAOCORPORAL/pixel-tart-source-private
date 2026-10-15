using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Export;
internal static class BoundedWorkflow
{
    internal static float EncodeDisplay(float v)
    {if(!float.IsFinite(v)||v<0||v>1)throw new ControlledIccException("ICC_OUTPUT_DOMAIN","Display conversion refuses outofrange.");return v<=.0031308f?12.92f*v:(float)(1.055*Math.Pow(v,1/2.4)-.055);}
    static string HashFile(string path){using var s=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();}
    static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    public static int Run(string[] args)
    {
        if(args.Length!=6)throw new ArgumentException("--bounded-research source newdir externalDLL workingICC outputICC");
        string destination=Path.GetFullPath(args[2]);if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("Destination already exists.");
        string staging=destination+".partial-"+Guid.NewGuid().ToString("N");var isolated=(string[])args.Clone();isolated[2]=staging;
        try{int result=RunCore(isolated);Directory.Move(staging,destination);return result;}
        catch{if(Directory.Exists(staging))Console.Error.WriteLine("Diagnostic partial output retained, never published: "+staging);throw;}
    }
    static int RunCore(string[] args)
    {
        using var cancellation=new CancellationTokenSource();
        if(int.TryParse(Environment.GetEnvironmentVariable("KITAO_ICC_CANCEL_AFTER_MS"),out int cancelMs)&&cancelMs>0)cancellation.CancelAfter(cancelMs);
        var token=cancellation.Token;token.ThrowIfCancellationRequested();
        if(args.Length!=6)throw new ArgumentException("--bounded-research source newdir externalDLL workingICC outputICC");
        string source=Path.GetFullPath(args[1]),dir=Path.GetFullPath(args[2]);if(Directory.Exists(dir))throw new IOException("New directory required");
        var input=EmbeddedIccInspector.Read(source,token);var working=ControlledIccProfile.Read(File.ReadAllBytes(args[4]));var output=ControlledIccProfile.Read(File.ReadAllBytes(args[5]));
        if(output.Sha256!="dafbff7207d22b526baa8f61a280e197b639af68cbe2e48529455157612b7407")throw new ControlledIccException("ICC_PROFILE_NOT_EVALUATED","Displaycontractrequireslinear-sRGBoutput.");
        using var engine=new ControlledIccTransform(new(true,args[3],"2f864b9f1bf5839fbc963265a0c9c94a4709124439e5ab5bfcae5f522483a1c4",new[]{input.Sha256,working.Sha256,output.Sha256}));
        WorkflowMemory.Stage("bounded-wic-cache");using var f=File.OpenRead(source);var frame=BitmapDecoder.Create(f,BitmapCreateOptions.IgnoreColorProfile|BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];
        if(frame.Format!=PixelFormats.Rgb48&&frame.Format!=PixelFormats.Rgb24&&frame.Format!=PixelFormats.Bgr24)throw new ControlledIccException("ICC_PIXEL_FORMAT","OpaqueRGB24/48only");
        if(frame.Metadata is BitmapMetadata md)foreach(var q in new[]{"/ifd/{ushort=274}","/app1/ifd/{ushort=274}"})if(md.ContainsQuery(q)&&Convert.ToInt32(md.GetQuery(q))!=1)throw new ControlledIccException("ICC_ORIENTATION","Orientation1required");
        int w=frame.PixelWidth,h=frame.PixelHeight;long pixels=(long)w*h;
        // OutputushortRGB + displayRGB8 + WICconservative32bytes/pixel + encoder32bytes/pixel + fixedscratch.
        long estimated=checked(pixels*73+64L*1024*1024);const long budget=2L*1024*1024*1024;
        if(pixels<=0||estimated>budget)throw new ControlledIccException("ICC_IMAGE_BUDGET","Conservative working+codec plan exceeds2GiB; pixelcount alone insufficient.");
        var converted=new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);var final=new ushort[checked(w*h*3)];var recipe=new ControlledIccRecipe(ControlledIccTransform.AlgorithmVersion,HashFile(source),input.Sha256,working.Sha256,output.Sha256,-1);
        int bandHeight=Math.Min(32,h);var band=new ushort[checked(w*bandHeight*3)];double reopenMax=0;using var inputHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var outputHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for(int y=0;y<h;y+=bandHeight)
        {
            int bh=Math.Min(bandHeight,h-y),n=w*bh*3;token.ThrowIfCancellationRequested();WorkflowMemory.Stage("bounded-decode-band");converted.CopyPixels(new Int32Rect(0,y,w,bh),band,w*6,0);var x=new float[n];for(int i=0;i<n;i++)x[i]=band[i]/65535f;inputHash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(x.AsSpan()));
            WorkflowMemory.Stage("bounded-lcms-edit");var l=engine.ConvertRgb(x,input,working,token);var edited=recipe.ApplyFrozenEdit(l,token);var encoded=engine.ConvertRgb(edited,working,output,token);outputHash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(encoded.AsSpan()));
            WorkflowMemory.Stage("bounded-reopen-band");var reopened=engine.ConvertRgb(recipe.ApplyFrozenEdit(engine.ConvertRgb(x,input,working,token),token),working,output,token);for(int i=0;i<n;i++){reopenMax=Math.Max(reopenMax,Math.Abs(encoded[i]-reopened[i]));final[y*w*3+i]=(ushort)Math.Round(encoded[i]*65535);}
        }
        if(reopenMax!=0)throw new InvalidDataException("Reopenbanddiffers");token.ThrowIfCancellationRequested();Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"recipe.json"),JsonSerializer.Serialize(recipe));var bmp=BitmapSource.Create(w,h,96,96,PixelFormats.Rgb48,null,final,w*6);bmp.Freeze();
        var profilePath=Path.Combine(dir,"output.icc");File.WriteAllBytes(profilePath,output.CopyBytes());var cc=new System.Collections.ObjectModel.ReadOnlyCollection<ColorContext>(new[]{new ColorContext(new Uri(Path.GetFullPath(args[5])))});
        WorkflowMemory.Stage("bounded-tiff");using(var stream=new FileStream(Path.Combine(dir,"output.tif"),FileMode.CreateNew))TiffExport.WriteRgb48Rows(stream,w,h,(row,bytes)=>System.Runtime.InteropServices.MemoryMarshal.AsBytes(final.AsSpan(row*w*3,w*3)).CopyTo(bytes.Span),new(IccProfile:output.CopyBytes(),Software:ControlledIccTransform.AlgorithmVersion),token);
        WorkflowMemory.Stage("bounded-png16");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp,null,null,cc));using(var stream=new FileStream(Path.Combine(dir,"output.png"),FileMode.CreateNew))png.Save(stream);
        WorkflowMemory.Stage("display-oetf-rgb8");var display=new byte[final.Length];for(int i=0;i<final.Length;i++){if((i&8191)==0)token.ThrowIfCancellationRequested();display[i]=(byte)Math.Round(EncodeDisplay(final[i]/65535f)*255);}
        var displayProfile=new ColorContext(PixelFormats.Bgra32);using(var profileStream=displayProfile.OpenProfileStream()){using var target=File.Create(Path.Combine(dir,"display-srgb.icc"));profileStream.CopyTo(target);}
        var screen=BitmapSource.Create(w,h,96,96,PixelFormats.Rgb24,null,display,w*3);screen.Freeze();WorkflowMemory.Stage("display-jpeg8");var jpeg=new JpegBitmapEncoder{QualityLevel=100};jpeg.Frames.Add(BitmapFrame.Create(screen,null,null,new System.Collections.ObjectModel.ReadOnlyCollection<ColorContext>(new[]{displayProfile})));using(var stream=new FileStream(Path.Combine(dir,"display.jpg"),FileMode.CreateNew))jpeg.Save(stream);
        var displayPng=new PngBitmapEncoder();displayPng.Frames.Add(BitmapFrame.Create(screen,null,null,new System.Collections.ObjectModel.ReadOnlyCollection<ColorContext>(new[]{displayProfile})));using(var stream=new FileStream(Path.Combine(dir,"display.png"),FileMode.CreateNew))displayPng.Save(stream);
        token.ThrowIfCancellationRequested();File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(new{status="ISOLATED_BOUNDED_EXPERIMENT",w,h,estimatedBudgetBytes=estimated,budget,bandHeight,recipe,inputFloatSha256=Convert.ToHexString(inputHash.GetHashAndReset()),outputFloatSha256=Convert.ToHexString(outputHash.GetHashAndReset()),linearCodeSha256=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(final.AsSpan()))).ToLowerInvariant(),displayCodeSha256=Hash(display),reopenMax,displayContract="linear-sRGB -> IECsRGBOETF -> RGB8; explicitstandardsRGBICC; notphysicalmonitorcalibration",workingOutput="linearTIFF16/PNG16ICCunchanged",CalibrationEligible=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(Path.Combine(dir,"result.json"));return 0;
    }
}
