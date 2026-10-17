using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
internal static class InstantFullAudit
{
 private static string Hash(string p){using var s=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(s));}
 public static async Task Run(string manifest,string output)
 {
  if(Directory.Exists(output))throw new IOException("Fresh full-size evidence required");Directory.CreateDirectory(output);
  var reports=new List<object>();var baseline=StudioGlobalResources.AccountedBytes;var process=Process.GetCurrentProcess();
  foreach(var item in JsonSerializer.Deserialize<InstantReviewInput[]>(File.ReadAllText(manifest))!)
  {
   if(Hash(item.Original)!=item.ExpectedOriginalSha256||Hash(item.FrozenTiff)!=item.ExpectedFrozenSha256)throw new InvalidDataException("Source/frozen identity mismatch");
   var folder=Path.Combine(output,item.Id);Directory.CreateDirectory(folder);var copied=Path.Combine(folder,"frozen.private.tif");File.Copy(item.FrozenTiff,copied);
   TiffStreamValidation validation;using(var sourceFile=File.OpenRead(copied))validation=TiffStreamValidator.Validate(sourceFile);
   if(validation.BitsPerSample!=16||!validation.IccProfile.SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Frozen RGB16/sRGB required");
   var orientation=validation.Orientation;var w=orientation>=5?validation.Height:validation.Width;var h=orientation>=5?validation.Width:validation.Height;var bytes=(long)w*h*3*4;
   var reservation=StudioGlobalResources.ReserveWork(bytes);CandidateImageLease? source=null;
   try
   {
    var data=new float[checked(w*h*3)];using(var rowLease=StudioGlobalResources.ReserveWork(validation.Width*6L))
    using(var file=File.OpenRead(copied))
    {
     file.Position=file.Length-validation.PixelBytes;var row=new byte[validation.Width*6];
     for(int y=0;y<validation.Height;y++){file.ReadExactly(row);for(int x=0;x<validation.Width;x++)
     {var(dx,dy)=orientation switch{8=>(y,validation.Width-1-x),6=>(validation.Height-1-y,x),3=>(validation.Width-1-x,validation.Height-1-y),_=>(x,y)};
      for(int c=0;c<3;c++)data[(dy*w+dx)*3+c]=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan((x*3+c)*2))/65535f;}}
    }
    // Transfer an already acquired input reservation, no second charge or temporary full-image copy.
    source=CandidateImageLease.Transfer(new HighBitDepthImageBuffer(w,h,data,"16","sRGB",1),reservation);reservation=null!;
    foreach(var profile in CandidateProfiles.Available.Where(p=>p.Id!="modern-600-v3"))
    {
     var timer=Stopwatch.StartNew();var recipe=CandidateRecipe.Create(profile.Id).Freeze();var recipeJson=JsonSerializer.Serialize(recipe);var restored=JsonSerializer.Deserialize<CandidateRecipe>(recipeJson)!.Freeze();
     using var session=new CandidateRenderSession(source,restored);var path=Path.Combine(folder,profile.Id+".private.tif");var sampleHashes=new Dictionary<string,string>();
     using(var region=session.RenderRegion(session.Layout.Width/2,session.Layout.Height/2,32,32))using(var borrow=region.Borrow())sampleHashes["region"]=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(borrow.Image.Rgb32.Span)));
     var renderRegionMs=timer.Elapsed.TotalMilliseconds;
     using var rowBudget=StudioGlobalResources.ReserveWork(session.Layout.Width*6L);CandidateImageLease? strip=null;CandidateImageLease.BorrowedImage? stripBorrow=null;int first=-1;
     try
     {
      await AtomicTiffWriter.WriteRowsAsync(path,session.Layout.Width,session.Layout.Height,(y,row)=>
      {
       if(strip is null||y>=first+strip.Height){stripBorrow?.Dispose();strip?.Dispose();first=y;strip=session.RenderRegion(0,y,session.Layout.Width,Math.Min(32,session.Layout.Height-y));stripBorrow=strip.Borrow();}
       var values=stripBorrow!.Image.Rgb32.Span.Slice((y-first)*session.Layout.Width*3,session.Layout.Width*3);
       for(int i=0;i<values.Length;i++)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.Span[(i*2)..],(ushort)Math.Clamp(Math.Round(values[i]*65535),0,65535));
      },new(IccProfile:StudioQuickExport.SrgbProfileBytes(),Orientation:1,Software:"Kitao V3 research strips"),overwrite:false);
     }
     finally{stripBorrow?.Dispose();strip?.Dispose();}
     TiffStreamValidation check;using(var file=File.OpenRead(path))check=TiffStreamValidator.Validate(file);if(check.BitsPerSample!=16||check.Orientation!=1||!check.IccProfile.SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Tiff output contract");
     process.Refresh();reports.Add(new{input=item.Id,profile=profile.Id,scope="FULL_SIZE_RESEARCH_STRIP_TIFF_NOT_PRODUCT_UI",sourceWidth=w,sourceHeight=h,outputWidth=check.Width,outputHeight=check.Height,bytesSource=bytes,recipe=restored,regionHash=sampleHashes["region"],renderRegionMs,totalMs=timer.Elapsed.TotalMilliseconds,pixelSha256=check.PixelSha256,iccExact=true,orientation=1,bits=16,retainedLedger=StudioGlobalResources.AccountedBytes,processPrivate=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64,pngJpeg="NOT_RUN_THIS_PASS",preview="Synthetic area oracle tested; full-photo preview not generated in this pass",reopen="JSON frozen recipe; not native product project"});
     File.WriteAllText(Path.Combine(output,"FULL_SIZE_RESULTS.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
    }
   }
   finally{source?.Dispose();reservation?.Dispose();}
   if(Hash(item.Original)!=item.ExpectedOriginalSha256||Hash(copied)!=item.ExpectedFrozenSha256)throw new InvalidDataException("Source changed");
  }
  if(StudioGlobalResources.AccountedBytes!=baseline)throw new InvalidDataException("Outstanding owned resources");
 }
}
