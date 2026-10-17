using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;

internal sealed record InstantReviewInput(string Id,string FrozenTiff,string Original,string ExpectedOriginalSha256,string ExpectedFrozenSha256);
internal static class InstantReviewAudit
{
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private static ProfessionalRawDefinition Rendered=>new(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");
    private static ProfessionalRawMaster Master(HighBitDepthImageBuffer image,string hash)=>new(image,new("FrozenProductRenderedSrgb16","Rendered","FrozenReference",hash,Rendered.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB"));
    private static HighBitDepthImageBuffer ReadFrozen(string path)
    {
        using var input=File.OpenRead(path);var frame=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
        if(frame.Format.BitsPerPixel!=48||frame.ColorContexts is not {Count:>0})throw new InvalidDataException("Expected frozen product RGB16 with embedded sRGB.");
        using(var stream=frame.ColorContexts[0].OpenProfileStream()){using var bytes=new MemoryStream();stream.CopyTo(bytes);if(!bytes.ToArray().SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Frozen product ICC differs from expected sRGB; no implicit transform.");}
        ushort orientation=1;if(frame.Metadata is BitmapMetadata meta){try{if(meta.GetQuery("/ifd/{ushort=274}") is ushort o)orientation=o;}catch(NotSupportedException){}}
        if(checked((long)frame.PixelWidth*frame.PixelHeight*24)>1024L*1024*1024)throw new StudioBudgetException(checked((long)frame.PixelWidth*frame.PixelHeight*24),1024L*1024*1024);
        var converted=new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);var data=new float[checked(frame.PixelWidth*frame.PixelHeight*3)];var row=new ushort[frame.PixelWidth*3];
        for(var y=0;y<frame.PixelHeight;y++){converted.CopyPixels(new System.Windows.Int32Rect(0,y,frame.PixelWidth,1),row,row.Length*2,0);for(var x=0;x<row.Length;x++)data[y*row.Length+x]=row[x]/65535f;}
        return new(frame.PixelWidth,frame.PixelHeight,data,"16","sRGB",orientation);
    }
    private static object Statistics(HighBitDepthImageBuffer image)
    {
        long low=0,high=0,nonfinite=0;double sum=0;
        foreach(var v in image.Rgb32.Span){if(!float.IsFinite(v))nonfinite++;if(v<=0)low++;if(v>=1)high++;sum+=v;}
        return new{image.Width,image.Height,samples=image.Rgb32.Length,zeroChannelSamples=low,oneChannelSamples=high,nonfinite,mean=sum/image.Rgb32.Length};
    }
    public static async Task Run(string manifest,string output)
    {
        if(Directory.Exists(output))throw new IOException("New isolated review output required.");Directory.CreateDirectory(output);
        var inputs=JsonSerializer.Deserialize<InstantReviewInput[]>(File.ReadAllText(manifest))!;var reports=new List<object>();
        var process=Process.GetCurrentProcess();var baseline=StudioGlobalResources.AccountedBytes;
        async Task Review(string id,HighBitDepthImageBuffer display,object inputEvidence)
        {
            var dir=Path.Combine(output,id);Directory.CreateDirectory(dir);
            var master=Master(display,"frozen-"+id);var originalTiff=Path.Combine(dir,"original.tif");
            await ProfessionalRawExport.WriteAsync(master,Rendered,originalTiff,StudioExportFormat.Tiff);
            async Task<object> Save(string stem,HighBitDepthImageBuffer image)
            {
                var watch=Stopwatch.StartNew();var m=Master(image,"review-output");var before=GC.GetTotalAllocatedBytes();
                var t=Path.Combine(dir,stem+".tif");var p=Path.Combine(dir,stem+".png");
                await ProfessionalRawExport.WriteAsync(m,Rendered,t,StudioExportFormat.Tiff);var ti=ProfessionalRawExport.Inspect(t,m,Rendered,StudioExportFormat.Tiff);
                await ProfessionalRawExport.WriteAsync(m,Rendered,p,StudioExportFormat.Png);var pi=ProfessionalRawExport.Inspect(p,m,Rendered,StudioExportFormat.Png);
                return new{tiff=Path.GetFileName(t),png=Path.GetFileName(p),tiffSha256=Hash(t),pngSha256=Hash(p),ti,pi,exportMs=watch.Elapsed.TotalMilliseconds,allocatedBytes=GC.GetTotalAllocatedBytes()-before,statistics=Statistics(image)};
            }
            foreach(var profile in CandidateProfiles.Available.Where(p=>p.Id!="modern-600-v3"))
            {
                var recipe=CandidateRecipe.Create(profile.Id);recipe.Validate();var stem=profile.Id;
                File.WriteAllText(Path.Combine(dir,stem+".recipe.json"),JsonSerializer.Serialize(recipe,Json));
                var restored=JsonSerializer.Deserialize<CandidateRecipe>(File.ReadAllText(Path.Combine(dir,stem+".recipe.json")))!;restored.Validate();

                var watch=Stopwatch.StartNew();var allocated=GC.GetTotalAllocatedBytes();process.Refresh();var beforePrivate=process.PrivateMemorySize64;
                using var candidateOwner=CandidateRenderer.Render(display,restored);using var candidateBorrow=candidateOwner.Borrow();var candidate=candidateBorrow.Image;var renderMs=watch.Elapsed.TotalMilliseconds;
                using var reopenedOwner=CandidateRenderer.Render(display,restored);using var reopenedBorrow=reopenedOwner.Borrow();var reopened=reopenedBorrow.Image;if(!candidate.Rgb32.Span.SequenceEqual(reopened.Rgb32.Span))throw new InvalidDataException("Candidate reopen mismatch.");
                var old=InstantFilmProfiles.Available.Single(p=>p.Id==profile.LegacyId);
                var legacyFilm=new InstantFilmDefinition(true,old.Id,old.ChemicalGeneration,AlgorithmVersion:2){PaperGeometry=recipe.Geometry};
                legacyFilm=legacyFilm with{DiceEnabled=true,Snapshot=recipe.Random};
                File.WriteAllText(Path.Combine(dir,stem+".legacy-recipe.json"),JsonSerializer.Serialize(Rendered with{InstantFilm=legacyFilm},Json));
                var candidateFiles=await Save(stem+"-candidate",candidate);
                var legacy=ProfessionalRawPipeline.RenderOutput(master,Rendered with{InstantFilm=legacyFilm});var legacyFiles=await Save(stem+"-legacy",legacy);
                process.Refresh();reports.Add(new{input=id,inputEvidence,profile=profile.Id,domain=CandidateRecipe.Domain,admission=CandidateRecipe.Admission,recipe,
                    oldRecipe=legacyFilm,candidateFiles,legacyFiles,renderMs,allocatedBytes=GC.GetTotalAllocatedBytes()-allocated,beforePrivate,privateBytes=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64,
                    ledgerAfter=StudioGlobalResources.AccountedBytes,saveReopen="Candidate JSON + frozen rendered product master; NOT live project UI integration",referenceEvidence=recipe.Provenance,humanAestheticReview="NOT_RUN"});
                File.WriteAllText(Path.Combine(output,"review.json"),JsonSerializer.Serialize(reports,Json));
            }
        }
        foreach(var input in inputs)
        {
            if(Hash(input.Original)!=input.ExpectedOriginalSha256||Hash(input.FrozenTiff)!=input.ExpectedFrozenSha256)throw new InvalidDataException("Authorized original or frozen derivative identity mismatch: "+input.Id);
            var copy=Path.Combine(output,input.Id+"-isolated-frozen.tif");File.Copy(input.FrozenTiff,copy,false);
            using(var lease=StudioGlobalResources.ReserveWork(1024L*1024*1024))
            {
                var full=ReadFrozen(copy);var fullSize=ProfessionalRawPipeline.OrientedSize(Master(full,"frozen"));
                var preview=ProfessionalRawPipeline.RenderPreview(Master(full,"frozen"),Rendered,512);
                await Review(input.Id,preview,new{input.Id,input.ExpectedOriginalSha256,input.ExpectedFrozenSha256,fullSize,reviewWidth=preview.Width,reviewHeight=preview.Height,
                    precision="Frozen product RGB16 -> display-linear box preview <=512 -> candidate; these are review-resolution NOT full-source renders",nativeDecodeThisRun=false,fujiRepeatabilityClaim=false});
            }
            if(Hash(input.Original)!=input.ExpectedOriginalSha256||Hash(input.FrozenTiff)!=input.ExpectedFrozenSha256||Hash(copy)!=input.ExpectedFrozenSha256)throw new InvalidDataException("Source changed.");
        }
        var data=new float[192*128*3];for(var y=0;y<128;y++)for(var x=0;x<192;x++){var i=(y*192+x)*3;data[i]=x/191f;data[i+1]=y/127f;data[i+2]=(x+y)%32<16?1:0;}
        await Review("synthetic",new(192,128,data,"16","sRGB",1),new{precision="Full synthetic float raster",inputSha256=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes<float>(data.AsSpan())))});
        if(StudioGlobalResources.AccountedBytes!=baseline)throw new InvalidDataException("Resource lease leak.");
    }
}

