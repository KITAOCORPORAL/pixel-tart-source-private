using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;

internal static class NativeRawAudit
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Init(uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Open(IntPtr raw,[MarshalAs(UnmanagedType.LPWStr)] string file);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Progress(IntPtr data,int stage,int iteration,int expected);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetProgress(IntPtr raw,Progress callback,IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Close(IntPtr raw);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Version();
    private const string NativeSha="F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9";
    private static readonly HashSet<string> Admitted=[SonyProbe.AuthorizedSha,"52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865","B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401"];
    private static unsafe string Dump(IntPtr data,long bytes,string path)
    {
        if(data==IntPtr.Zero||bytes<=0||bytes>2_000_000_000)throw new InvalidDataException("Unobserved or invalid native buffer");
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write);
        for(long offset=0;offset<bytes;offset+=1048576)
        {var block=new ReadOnlySpan<byte>((byte*)data+offset,(int)Math.Min(1048576,bytes-offset));hash.AppendData(block);output.Write(block);}
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static void Run(string copy,string native,string output,int cancelMs)
    {
        if(Directory.Exists(output))throw new IOException("New audit directory required");
        var sourceSha=SonyProbe.Hash(copy);if(!Admitted.Contains(sourceSha)||SonyProbe.Hash(native)!=NativeSha)throw new InvalidDataException("Unadmitted source/native");
        Directory.CreateDirectory(output);
        var module=NativeLibrary.Load(Path.GetFullPath(native));
        T Load<T>(string name)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,name));
        var clock=Stopwatch.StartNew();var rows=new List<object>();var seen=new HashSet<string>();var process=Process.GetCurrentProcess();string status="FAILED";object? failure=null;object? geometry=null;string? outputSha=null;string? callbackError=null;
        var pointer=Load<Init>("libraw_init")(0);if(pointer==IntPtr.Zero)throw new IOException("init failed");
        void Snapshot(string stage,bool sensor=false)
        {
            if(!seen.Add(stage))return;
            var data=Marshal.PtrToStructure<LibRawData>(pointer);var sizes=data.ImageSizes;
            var width=sensor?sizes.RawWidth:sizes.Width;var height=sensor?sizes.RawHeight:sizes.Height;
            var bytes=sensor?(long)sizes.RawHeight*(sizes.RawPitch==0?sizes.RawWidth*2:sizes.RawPitch):(long)width*height*8;
            var buffer=sensor?data.RawData.RawImage:Marshal.ReadIntPtr(pointer);
            if(buffer==IntPtr.Zero){rows.Add(new{stage,status="UNAVAILABLE",width,height});return;}
            var file=stage+".private-pixels.bin";var sha=Dump(buffer,bytes,Path.Combine(output,file));process.Refresh();
            rows.Add(new{stage,width,height,channels=sensor?1:4,bits=16,bytes,sha256=sha,localFile=file,ms=clock.Elapsed.TotalMilliseconds,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64,black=data.Color.Black,maximum=data.Color.Maximum});
        }
        Progress callback=(_,stage,iteration,expected)=>
        {
            try
            {
                if(cancelMs>0&&clock.ElapsedMilliseconds>=cancelMs)return 1;
                if(stage==512&&iteration==0)Snapshot("before-scale-colors");
                if(stage==512&&iteration==1)Snapshot("after-scale-colors");
                if(stage==1024&&iteration==1)Snapshot("after-pre-interpolate-before-demosaic");
                if(stage==262144&&iteration==0)Snapshot("after-demosaic-before-color");
                if(stage==262144&&iteration==1)Snapshot("after-color-transform");
                return 0;
            }
            catch(Exception error){callbackError=error.ToString();return 1;}
        };
        bool rawOwned=false;
        try
        {
            var open=Load<Open>("libraw_open_wfile")(pointer,Path.GetFullPath(copy));if(open!=0)throw new IOException("OpenFile nativeCode="+open);
            var ownedPointer=pointer;pointer=IntPtr.Zero;
            using(var raw=new RawContext(ownedPointer))
            {
                rawOwned=true;
                pointer=typeof(RawContext).GetField("_r",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(raw) is IntPtr p?p:IntPtr.Zero;
                var data=Marshal.PtrToStructure<LibRawData>(pointer);var sizes=data.ImageSizes;
                geometry=new{raw.RawWidth,raw.RawHeight,width=sizes.Width,height=sizes.Height,sizes.TopMargin,sizes.LeftMargin,sizes.Flip,raw.DecoderInfo,model=raw.ImageParams.Model,make=raw.ImageParams.Make};
                Load<SetProgress>("libraw_set_progress_handler")(pointer,callback,IntPtr.Zero);
                raw.Unpack();Snapshot("unpacked-sensor",true);
                raw.DcrawProcess(options=>{options.HalfSize=false;options.UseCameraWb=true;options.UseAutoWb=false;options.UseCameraMatrix=true;options.OutputColor=LibRawColorSpace.SRGB;options.OutputBps=16;options.OutputTiff=false;options.Interpolation=true;options.UserFlip=0;});
                Snapshot("native-process-complete");using var image=raw.MakeDcrawMemoryImage();
                outputSha=Dump(image.DataPointer,image.DataSize,Path.Combine(output,"processed-rgb16.private-pixels.bin"));
                rows.Add(new{stage="processed-rgb16",image.Width,image.Height,image.Bits,image.Channels,bytes=image.DataSize,sha256=outputSha,ms=clock.Elapsed.TotalMilliseconds});status="DECODED_AUDIT_NOT_PRODUCT_FIX";
            }
            pointer=IntPtr.Zero;
        }
        catch(LibRawException error){failure=new{nativeCode=(int)error.ErrorCode,nativeName=error.ErrorCode.ToString(),error.Message};status=error.ErrorCode.ToString().Contains("Cancelled")?"CANCELLED":"REFUSED";pointer=IntPtr.Zero;}
        catch(Exception error){failure=new{error=error.ToString()};if(rawOwned)pointer=IntPtr.Zero;}
        finally
        {
            if(!rawOwned && pointer!=IntPtr.Zero){Load<Close>("libraw_close")(pointer);pointer=IntPtr.Zero;}
            // RawContext owns the successfully opened pointer. Never double-close it.
            process.Refresh();rows.Add(new{stage="after-native-close",ms=clock.Elapsed.TotalMilliseconds,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64});
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{status,sourceSha256=sourceSha,nativeSha256=NativeSha,wrapperSha256=SonyProbe.Hash(typeof(RawContext).Assembly.Location),version=RawContext.Version,OMP_NUM_THREADS=Environment.GetEnvironmentVariable("OMP_NUM_THREADS"),cancelMs,geometry,outputSha,callbackError,failure,rows,earliestStageClaim="Only actually captured buffers; stage names tied to pinned upstream progress callback sites; no unobserved stage inferred.",contract="LegacyRenderedSrgb16UnrotatedAudit1; WB/matrix/gamma defaults baked, not sensor-linear; sensor dump is separately labelled.",productModified=false},new JsonSerializerOptions{WriteIndented=true}));
            GC.KeepAlive(callback);
        }
    }
}
