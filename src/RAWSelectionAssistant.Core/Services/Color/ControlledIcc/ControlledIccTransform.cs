using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Services.Color.ControlledIcc;

public sealed record ControlledIccConfiguration(bool ExplicitResearchOptIn,string NativeLibraryPath,string ExpectedLibrarySha256,
    IReadOnlyList<string> AllowedProfileHashes,int Intent=1,bool BlackPointCompensation=false,double AdaptationState=1);

/// <summary>External, hash-pinned LCMS research adapter. No native dependency is shipped or implicitly loaded.</summary>
public sealed class ControlledIccTransform : IDisposable
{
    public const string AlgorithmVersion="ControlledIccLinearV1";
    private readonly IntPtr library;
    private readonly ControlledIccConfiguration config;
    private readonly HashSet<string> allowed;
    private readonly object gate=new();
    private bool disposed;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint VersionFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ContextFn(IntPtr plugins,IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DeleteContextFn(IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate double AdaptFn(IntPtr context,double state);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr OpenFn(IntPtr context,IntPtr bytes,uint size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CloseFn(IntPtr profile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr CreateFn(IntPtr context,IntPtr source,uint input,IntPtr target,uint output,uint intent,uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DeleteFn(IntPtr transform);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DoFn(IntPtr transform,IntPtr input,IntPtr output,uint count);
    private readonly ContextFn createContext;
    private readonly DeleteContextFn deleteContext;
    private readonly AdaptFn adapt;
    private readonly OpenFn open;
    private readonly CloseFn close;
    private readonly CreateFn create;
    private readonly DeleteFn delete;
    private readonly DoFn execute;
    public uint EngineVersion { get; }
    public string EngineSha256 { get; }
    public ControlledIccTransform(ControlledIccConfiguration configuration)
    {
        config=configuration;
        if(!config.ExplicitResearchOptIn)throw new ControlledIccException("ICC_DISABLED","Explicit research opt-in required; legacy workflow unchanged.");
        if(config.Intent!=1||config.BlackPointCompensation||config.AdaptationState!=1)throw new ControlledIccException("ICC_INTENT","V1 only supports relative intent1, BPCoff, adaptation1.");
        if(!Path.IsPathFullyQualified(config.NativeLibraryPath))throw new ControlledIccException("ICC_LIBRARY_PATH","Absolute external library path required.");
        using(var file=File.OpenRead(config.NativeLibraryPath))EngineSha256=Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
        if(!EngineSha256.Equals(config.ExpectedLibrarySha256,StringComparison.OrdinalIgnoreCase)||EngineSha256!="2f864b9f1bf5839fbc963265a0c9c94a4709124439e5ab5bfcae5f522483a1c4")throw new ControlledIccException("ICC_LIBRARY_HASH","Only evaluated LCMS2.16 artifact accepted.");
        var evaluated=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"dafbff7207d22b526baa8f61a280e197b639af68cbe2e48529455157612b7407","954e3bb304651951681989671ef99eed11183e9179a61a036243d655986c5ea1"};
        allowed=new(config.AllowedProfileHashes,StringComparer.OrdinalIgnoreCase);
        if(allowed.Any(h=>!evaluated.Contains(h)))throw new ControlledIccException("ICC_PROFILE_NOT_EVALUATED","Configuration cannot expand built-in evaluated hashes.");
        if(allowed.Count==0)throw new ControlledIccException("ICC_PROFILE_POLICY","Explicit evaluated profile SHA allowlist required.");
        library=NativeLibrary.Load(config.NativeLibraryPath);
        try
        {
            T Load<T>(string name) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
            EngineVersion=Load<VersionFn>("cmsGetEncodedCMMversion")();if(EngineVersion!=2160)throw new ControlledIccException("ICC_LIBRARY_VERSION","Expected2160.");
            createContext=Load<ContextFn>("cmsCreateContext");deleteContext=Load<DeleteContextFn>("cmsDeleteContext");adapt=Load<AdaptFn>("cmsSetAdaptationStateTHR");
            open=Load<OpenFn>("cmsOpenProfileFromMemTHR");close=Load<CloseFn>("cmsCloseProfile");create=Load<CreateFn>("cmsCreateTransformTHR");delete=Load<DeleteFn>("cmsDeleteTransform");execute=Load<DoFn>("cmsDoTransform");
        }
        catch { NativeLibrary.Free(library);throw; }
    }
    public float[] ConvertRgb(ReadOnlySpan<float> input,ControlledIccProfile source,ControlledIccProfile target,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(input.Length==0||input.Length%3!=0)throw new ControlledIccException("ICC_PIXELS","RGB sample length invalid.");
        if(!allowed.Contains(source.Sha256)||!allowed.Contains(target.Sha256))throw new ControlledIccException("ICC_PROFILE_NOT_EVALUATED","Profile SHA not evaluated in this experiment.");
        foreach(var v in input)if(!float.IsFinite(v)||v<0||v>1)throw new ControlledIccException("ICC_DOMAIN","V1 RGB domain is finite0..1; no hidden clipping.");
        var output=new float[input.Length];var copy=input.ToArray();var src=source.CopyBytes();var dst=target.CopyBytes();
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            IntPtr context=IntPtr.Zero,sp=IntPtr.Zero,dp=IntPtr.Zero,t=IntPtr.Zero;
            var s=GCHandle.Alloc(src,GCHandleType.Pinned);var d=GCHandle.Alloc(dst,GCHandleType.Pinned);var i=GCHandle.Alloc(copy,GCHandleType.Pinned);var o=GCHandle.Alloc(output,GCHandleType.Pinned);
            try
            {
                context=createContext(IntPtr.Zero,IntPtr.Zero);if(context==IntPtr.Zero)throw new ControlledIccException("ICC_CONTEXT","LCMS context creation failed.");
                adapt(context,1);sp=open(context,s.AddrOfPinnedObject(),(uint)src.Length);dp=open(context,d.AddrOfPinnedObject(),(uint)dst.Length);
                if(sp==IntPtr.Zero||dp==IntPtr.Zero)throw new ControlledIccException("ICC_ENGINE_PROFILE","LCMS refused profile.");
                const uint rgbFloat=(1u<<22)|(4u<<16)|(3u<<3)|4u;
                t=create(context,sp,rgbFloat,dp,rgbFloat,1,0x144);if(t==IntPtr.Zero)throw new ControlledIccException("ICC_TRANSFORM","LCMS transform refused.");
                for(int pixel=0;pixel<copy.Length/3;pixel+=4096)
                {
                    token.ThrowIfCancellationRequested();int n=Math.Min(4096,copy.Length/3-pixel);
                    execute(t,IntPtr.Add(i.AddrOfPinnedObject(),pixel*12),IntPtr.Add(o.AddrOfPinnedObject(),pixel*12),(uint)n);
                }
                token.ThrowIfCancellationRequested();
                if(output.Any(v=>!float.IsFinite(v)||v<0||v>1))throw new ControlledIccException("ICC_OUTPUT_DOMAIN","Out-of-gamut output is outside V1 contract; no silent clipping.");
                return output;
            }
            finally{if(t!=IntPtr.Zero)delete(t);if(dp!=IntPtr.Zero)close(dp);if(sp!=IntPtr.Zero)close(sp);if(context!=IntPtr.Zero)deleteContext(context);o.Free();i.Free();d.Free();s.Free();}
        }
    }
    public void Dispose(){lock(gate){if(!disposed){disposed=true;NativeLibrary.Free(library);}}GC.SuppressFinalize(this);}
}

public sealed record ControlledIccRecipe(string AlgorithmVersion,string SourceFileSha256,string InputProfileSha256,
    string WorkingProfileSha256,string OutputProfileSha256,double LinearExposureEv=0,int Intent=1,bool Bpc=false,double Adaptation=1)
{
    public void Validate()
    {
        if(AlgorithmVersion!=ControlledIccTransform.AlgorithmVersion||Intent!=1||Bpc||Adaptation!=1||!double.IsFinite(LinearExposureEv)||LinearExposureEv is < -3 or >0)
            throw new ControlledIccException("ICC_RECIPE_VERSION","Unknown version/configuration; V1 only permits bounded non-positive linear exposure.");
        foreach(var s in new[]{SourceFileSha256,InputProfileSha256,WorkingProfileSha256,OutputProfileSha256})
            if(s.Length!=64||s.Any(c=>!Uri.IsHexDigit(c)))throw new ControlledIccException("ICC_RECIPE_IDENTITY","SHA256 identity missing.");
    }
    public float[] ApplyFrozenEdit(ReadOnlySpan<float> linear,CancellationToken token=default)
    {
        Validate();var result=new float[linear.Length];float scale=(float)Math.Pow(2,LinearExposureEv);
        for(int i=0;i<result.Length;i++){if((i&8191)==0)token.ThrowIfCancellationRequested();var v=linear[i];if(!float.IsFinite(v)||v<0||v>1)throw new ControlledIccException("ICC_DOMAIN","Invalid linear working value.");result[i]=v*scale;}return result;
    }
}
