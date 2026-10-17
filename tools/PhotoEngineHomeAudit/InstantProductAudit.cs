using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
internal static class InstantProductAudit
{
 private static string Hash(string p){using var s=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(s));}
 public static async Task Run(string manifest,string output,string? existingDirectOutput=null)
 {
  if(Directory.Exists(output))throw new IOException("New product audit output");Directory.CreateDirectory(output);var results=new List<object>();
  foreach(var item in JsonSerializer.Deserialize<InstantReviewInput[]>(File.ReadAllText(manifest))!)
  {
   if(Hash(item.Original)!=item.ExpectedOriginalSha256||Hash(item.FrozenTiff)!=item.ExpectedFrozenSha256)throw new InvalidDataException("Authorized source mismatch");
   TiffStreamValidation source;using(var f=File.OpenRead(item.FrozenTiff))source=TiffStreamValidator.Validate(f);
   if(source.BitsPerSample!=16||!source.IccProfile.SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Frozen RGB16 ICC");
   using var inputReservation=StudioGlobalResources.ReserveWork(source.Width*(long)source.Height*12);
   var data=new float[checked(source.Width*source.Height*3)];using(var rowReservation=StudioGlobalResources.ReserveWork(source.Width*6L))using(var f=File.OpenRead(item.FrozenTiff)){f.Position=f.Length-source.PixelBytes;var row=new byte[source.Width*6];for(int y=0;y<source.Height;y++){f.ReadExactly(row);for(int i=0;i<source.Width*3;i++)data[(y*source.Width*3)+i]=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(i*2))/65535f;}}
   var basis=new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");
   var image=new HighBitDepthImageBuffer(source.Width,source.Height,data,"16","sRGB",(ushort)source.Orientation);
   var contract=new ProfessionalRawContract("AuthorizedFrozenProductSrgb16","Frozen",item.Id,item.ExpectedOriginalSha256,basis.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB");var master=new ProfessionalRawMaster(image,contract);
   foreach(var profile in CandidateProfiles.Available.Where(p=>p.Id!="modern-600-v3"))
   {
    var sw=Stopwatch.StartNew();var r=CandidateRecipe.Create(profile.Id,20261017) with{Response=profile.Response with{ExposureEv=.2},Emulsion=new(Microstructure:.002,DevelopmentUnevenness:.003),Surface=new(Frame:true)};
    var legacy=InstantFilmProfiles.Available.Single(p=>p.Id==profile.LegacyId);var film=new InstantFilmDefinition(true,legacy.Id,legacy.ChemicalGeneration,AlgorithmVersion:3){V3=r};var definition=basis with{InstantFilm=film};
    var folder=Path.Combine(output,item.Id,profile.Id);Directory.CreateDirectory(folder);var id=Guid.NewGuid();var version=Guid.NewGuid();var edit=new StudioEditVersion(version,"V3 actual product",DateTimeOffset.UtcNow,null,null,item.ExpectedOriginalSha256,null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto){RawDefinition=definition,RawInput=contract};
    var session=Path.Combine(folder,"session.private.ptstudio.json");await ColorStudioSessionStore.SaveAsync(session,new([new(id,item.FrozenTiff,null,true,null,null,null,SourceHash:item.ExpectedOriginalSha256,EditVersions:[edit],ActiveEditVersionId:version,RawDefinition:definition,RawInput:contract)],id));var reopened=await ColorStudioSessionStore.LoadAsync(session);var restored=reopened.Targets.Single().RawDefinition!;
    using var preview=ProfessionalRawPipeline.RenderPreview(master,restored,512);using var region=ProfessionalRawPipeline.RenderRegion(master,restored,16,16,64,64);
    var formats=new List<object>();foreach(var format in existingDirectOutput is null?new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg}:Array.Empty<StudioExportFormat>())
    {
     var file=Path.Combine(folder,"direct"+StudioQuickExport.Extension("x.arw",format));var receipt=await ProfessionalRawExport.WriteAsync(master,restored,file,format);formats.Add(new{format=format.ToString(),receipt,sha256=Hash(file),bytes=new FileInfo(file).Length});
    }
    double previewMaximum=0;
    var tiff=existingDirectOutput is null?Path.Combine(folder,"direct.tif"):Path.Combine(existingDirectOutput,item.Id,profile.Id,"direct.tif");TiffStreamValidation tv;using(var file=File.OpenRead(tiff))tv=TiffStreamValidator.Validate(file);
    using(var compareBudget=StudioGlobalResources.ReserveWork((long)preview.Width*preview.Height*3*8+tv.Width*6L))
    using(var file=File.OpenRead(tiff))
    {
     var sum=new double[preview.Width*preview.Height*3];double rx=tv.Width/(double)preview.Width,ry=tv.Height/(double)preview.Height;file.Position=file.Length-tv.PixelBytes;var row=new byte[tv.Width*6];
     for(int y=0;y<tv.Height;y++){file.ReadExactly(row);for(int py=(int)(y/ry);py<=Math.Min(preview.Height-1,(int)((y+1)/ry));py++){var wy=Math.Max(0,Math.Min(y+1,(py+1)*ry)-Math.Max(y,py*ry));if(wy==0)continue;for(int x=0;x<tv.Width;x++)for(int px=(int)(x/rx);px<=Math.Min(preview.Width-1,(int)((x+1)/rx));px++){var wx=Math.Max(0,Math.Min(x+1,(px+1)*rx)-Math.Max(x,px*rx));for(int c=0;c<3;c++)sum[(py*preview.Width+px)*3+c]+=CandidateTone.Decode(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan((x*3+c)*2))/65535d)*wx*wy;}}}
     for(int i=0;i<sum.Length;i++)previewMaximum=Math.Max(previewMaximum,Math.Abs(CandidateTone.Encode(sum[i]/(rx*ry))-preview.Rgb32.Span[i]));
    }
    if(previewMaximum>2d/65535)throw new InvalidDataException("Real product preview vs full-size TIFF linear-area oracle exceeds fixed quantization tolerance");
    var process=Process.GetCurrentProcess();process.Refresh();results.Add(new{input=item.Id,profile=profile.Id,originalSha=item.ExpectedOriginalSha256,frozenSha=item.ExpectedFrozenSha256,recipe=r,previewVsFullMaximum=previewMaximum,previewVsFullTolerance=2d/65535,previewWidth=preview.Width,previewHeight=preview.Height,regionPixelSha=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(region.Rgb32.Span))),sessionRecipeExact=JsonSerializer.Serialize(restored)==JsonSerializer.Serialize(definition),formats,milliseconds=sw.Elapsed.TotalMilliseconds,privateBytes=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64,ledger=StudioGlobalResources.AccountedBytes,scope="Formal Core module + actual ProfessionalRawExport direct stripes; no intermediate TIFF feeding PNG/JPEG; foreground UI separate"});
    File.WriteAllText(Path.Combine(output,"PRODUCT_RESULTS.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
   }
   if(Hash(item.Original)!=item.ExpectedOriginalSha256)throw new InvalidDataException("Original changed");
  }
 }
}
