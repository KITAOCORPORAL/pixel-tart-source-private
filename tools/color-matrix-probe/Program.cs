using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Projects;

if(args.Length!=3) throw new ArgumentException("Usage: <RAW> <SHA256> <new empty local output directory>");
var path=Path.GetFullPath(args[0]);var root=Path.GetFullPath(args[2]);var sourceDirectory=Path.GetDirectoryName(path)!;if(root.Equals(sourceDirectory,StringComparison.OrdinalIgnoreCase)||root.StartsWith(sourceDirectory+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Output cannot be within the source directory");
if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("Output directory must be empty");
Directory.CreateDirectory(root);
string HashFile(string f){using var s=File.OpenRead(f);return Convert.ToHexString(SHA256.HashData(s));}
var sourceHash=HashFile(path);if(!sourceHash.Equals(args[1],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Source hash mismatch");
if(!Environment.Is64BitProcess||RawContext.VersionNumber.ToString(3)!="0.21.1"||typeof(RawContext).Assembly.GetName().Version!=new Version(0,21,1,7))throw new InvalidOperationException("Requires validated x64 Sdcb 0.21.1.7 and LibRaw 0.21.1");
var allowedSources = new[]{"3A0C1672ABDA62CC26349C088CB87A268EF0E5B46B1F4CCC18C99EE936E447B5","593E1B0EE73EF7E699D73465E3BD8222AD0ED79BEB69E9265E440756D4B50A1A"};
if(!allowedSources.Contains(sourceHash))throw new NotSupportedException("Only the two explicitly inventoried D800 development files are permitted");
var timer=Stopwatch.StartNew();using var ctx=RawContext.OpenFile(path);var handle=ctx.UnsafeGetHandle();
var module=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(x=>x.ModuleName.Equals("raw_r.dll",StringComparison.OrdinalIgnoreCase));
var nativePath=Path.GetFullPath(module.FileName);var nativeHash=HashFile(nativePath);var managedPath=typeof(RawContext).Assembly.Location;var managedHash=HashFile(managedPath);
if(nativeHash!="F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9"||managedHash!="6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3"||RuntimeInformation.ProcessArchitecture!=Architecture.X64)throw new InvalidOperationException("Unrecognized actual DLL hash or architecture; ABI access refused");
if(ctx.ImageParams.Make.Trim()!="Nikon"||ctx.ImageParams.Model.Trim()!="D800")throw new NotSupportedException("Only inventoried Nikon D800 allowed");
var unpackOffset=checked((int)Marshal.OffsetOf<LibRawData>(nameof(LibRawData.RawUnpackParams)));var unpackPtr=IntPtr.Add(handle,unpackOffset);var unpack=Marshal.PtrToStructure<RawUnpackParams>(unpackPtr);unpack.UseRawSpeed=0;unpack.UseDngSdk=0;unpack.Options=2;unpack.ShotSelect=0;unpack.Specials=0;unpack.MaxRawMemoryMb=2048;unpack.SonyArw2PosterizationThr=0;unpack.CoolscanNefGamma=1;unpack.CustomCameraStrings=IntPtr.Zero;Marshal.StructureToPtr(unpack,unpackPtr,false);ctx.Unpack();
var sizesOffset=checked((int)Marshal.OffsetOf<LibRawData>(nameof(LibRawData.ImageSizes)));var colorsOffset=checked((int)Marshal.OffsetOf<LibRawData>(nameof(LibRawData.Color)));var rawOffset=checked((int)Marshal.OffsetOf<LibRawData>(nameof(LibRawData.RawData)));
LibRawImageSizes Sizes()=>Marshal.PtrToStructure<LibRawImageSizes>(IntPtr.Add(handle,sizesOffset));
LibRawColorData Color()=>Marshal.PtrToStructure<LibRawColorData>(IntPtr.Add(handle,colorsOffset));
var sizes=Sizes();if(sizes.TopMargin!=0||sizes.LeftMargin!=0||sizes.Flip!=0||sizes.RawPitch!=sizes.RawWidth*2)throw new NotSupportedException("Research probe supports only the verified zero-margin, unrotated, tightly packed layout");var initialColor=Color();var raw=Marshal.PtrToStructure<LibRawRawData>(IntPtr.Add(handle,rawOffset));if(raw.RawImage==IntPtr.Zero)throw new NotSupportedException("Only uint16 CFA raw buffers supported");
var rawSamples=new short[checked(sizes.RawHeight*(int)(sizes.RawPitch/2))];Marshal.Copy(raw.RawImage,rawSamples,0,rawSamples.Length);File.WriteAllBytes(Path.Combine(root,"cfa.u16"),MemoryMarshal.AsBytes(rawSamples.AsSpan()).ToArray());
var matrixFlagOffset=checked(rawOffset+(int)Marshal.OffsetOf<LibRawRawData>(nameof(LibRawRawData.IOParams)));if(Marshal.PtrToStructure<LibRawInternalOutputParams>(IntPtr.Add(handle,matrixFlagOffset)).RawColor!=0)throw new InvalidDataException("Camera matrix unavailable");if(ctx.ImageParams.Colors!=3||ctx.ImageParams.Filters<=1000||ctx.ImageParams.IsFoveon!=0)throw new NotSupportedException("Bayer 3-color only");
if(initialColor.CBlack.Take(6).Any(x=>x!=0)||sizes.RawWidth!=7424||sizes.RawHeight!=4924||sizes.IWidth!=7378||sizes.IHeight!=4924||(sourceHash.StartsWith("3A0C")&&initialColor.Maximum!=4095)||(sourceHash.StartsWith("593E")&&initialColor.Maximum!=16383))throw new NotSupportedException("Unverified dimensions or black/white layout");
var config=typeof(ProfessionalRawDecoder).GetMethod("Configure",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)??throw new MissingMethodException("ProfessionalRawDecoder.Configure");
var gain=ProfessionalRawDecoder.ResolveWhiteBalance(ctx.CameraMultipler.Select(x=>(double)x).ToArray(),initialColor.AsShotWbApplied!=0,new ProfessionalRawDefinition());
var observations=new List<object>();var stageNames=new HashSet<string>();Exception? callbackError=null;
unsafe void WriteStage(string name){if(!stageNames.Add(name))throw new InvalidDataException("Duplicate stage "+name);var z=Sizes();if(z.IWidth!=sizes.IWidth||z.IHeight!=sizes.IHeight)throw new InvalidDataException("Stage dimensions changed");int count=checked(z.IWidth*z.IHeight*4);IntPtr p=Marshal.ReadIntPtr(handle,0);if(p==IntPtr.Zero)throw new InvalidOperationException("image is null");var bytes=new ReadOnlySpan<byte>((void*)p,checked(count*2));var stagePath=Path.Combine(root,name+".u16");using(var stream=new FileStream(stagePath,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);}if(new FileInfo(stagePath).Length!=count*2L)throw new IOException("Stage length mismatch");var c=Color();observations.Add(new{name,width=z.IWidth,height=z.IHeight,layout="uint16 RG B G2, interleaved",c.Black,c.Maximum,cblack=c.CBlack.Take(6).ToArray(),c.PreMul,c.RgbCam});}
unsafe { CallbackHolder.Handler = (user,stage,iteration,expected) => {try{if(iteration==0&&stage.ToString().Contains("ScaleColors"))WriteStage("scale_before");if(iteration==1&&stage.ToString().Contains("ScaleColors"))WriteStage("scale_after");if(iteration==0&&stage.ToString().Contains("ConvertRgb"))WriteStage("matrix_before");if(iteration==1&&stage.ToString().Contains("ConvertRgb"))WriteStage("matrix_after");return 0;}catch(Exception e){callbackError=e;return 1;}}; }
unsafe {LibRawNative.SetProgressHandler(handle,&CallbackHolder.Invoke,IntPtr.Zero);ctx.DcrawProcess(o=>config.Invoke(null,new object[]{o,gain}));LibRawNative.SetProgressHandler(handle,null,IntPtr.Zero);}
if(callbackError!=null)throw new Exception("Stage callback failed",callbackError);if(observations.Count!=4||!stageNames.SetEquals(new[]{"scale_before","scale_after","matrix_before","matrix_after"}))throw new InvalidDataException("Missing required progress stage callbacks");
using var output=ctx.MakeDcrawMemoryImage();var processed=output.AsSpan<ushort>().ToArray();File.WriteAllBytes(Path.Combine(root,"output.u16"),MemoryMarshal.AsBytes(processed.AsSpan()).ToArray());
if(ctx.ImageParams.Filters!=2492765332u)throw new NotSupportedException("Final D800 CFA mapping mismatch");
var verify=await new ProfessionalRawDecoder().DecodeAsync(path,new ProfessionalRawDefinition());long productMismatches=0;try{var values=verify.Image.Rgb32.ToArray();if(values.Length!=processed.Length)throw new InvalidDataException("Product output dimensions differ");for(int i=0;i<values.Length;i++)if(values[i]!=processed[i]/65535f)productMismatches++;}finally{verify.Image.Dispose();}
var files=new[]{"cfa.u16","scale_before.u16","scale_after.u16","matrix_before.u16","matrix_after.u16","output.u16"}.Select(f=>new{name=f,length=new FileInfo(Path.Combine(root,f)).Length,sha256=HashFile(Path.Combine(root,f))}).ToArray();
timer.Stop();var processMetrics=new{elapsedMilliseconds=timer.Elapsed.TotalMilliseconds,peakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,includes="stage file writes, hashing, second product decode; not render-only performance"};
var metadata=new{processMetrics,manifestVersion="StageManifestV2",files,nativePath,nativeHash,managedPath,managedHash,architecture=RuntimeInformation.ProcessArchitecture.ToString(),productBaselineSampleMismatches=productMismatches,version="MatrixStageProbeV1",sourceSha256=sourceHash,sourceUnchanged=HashFile(path)==sourceHash,decoder=RawContext.Version,make=ctx.ImageParams.Make,model=ctx.ImageParams.Model,sizes=new{sizes.RawHeight,sizes.RawWidth,sizes.Height,sizes.Width,sizes.TopMargin,sizes.LeftMargin,sizes.IHeight,sizes.IWidth,sizes.RawPitch,sizes.Flip},filters=ctx.ImageParams.Filters,initialColor.Black,initialColor.Maximum,cblack=initialColor.CBlack.Take(6).ToArray(),initialColor.CamMul,initialColor.RgbCam,gain,stages=observations,output=new{output.Width,output.Height,output.Bits,output.Channels},notes="Stages are copied at LibRaw progress callbacks; not sensor recovery; initial raw uses raw pitch and full frame; intermediate frame orientation is raw processing orientation."};
File.WriteAllText(Path.Combine(root,"metadata.json"),JsonSerializer.Serialize(metadata,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{root,metadata.sourceSha256,metadata.sourceUnchanged,stageNames=observations.Count}));

static unsafe class CallbackHolder {
 public delegate int Callback(void* user,LibRawProgress stage,int iteration,int expected);
 public static Callback? Handler;
 public static int Invoke(void* user,LibRawProgress stage,int iteration,int expected)=>Handler!(user,stage,iteration,expected);
}
