using System.IO;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
internal static class BlindReviewPack
{
 public static async Task Run(string authorizedCopies,string output)
 {
  if(Directory.Exists(output))throw new IOException("New review pack required");Directory.CreateDirectory(output);var rows=new List<object>();int index=0;
  foreach(var name in new[]{"Sony_DSC08015.ARW","Canon_TNAN8886.CR3","Fuji_DSCF0347.RAF"})
  {
   index++;var path=Path.Combine(authorizedCopies,name);var sha=SonyProbe.Hash(path);
   if(sha!=SonyProbe.AuthorizedSha&&sha!="B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401"&&sha!="52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865")throw new InvalidDataException("Unauthorized photo");
   var pipeline=new RawMatchTiff16ProductPipeline(new LibRawDecoder());var master=await pipeline.DecodeFrozenMasterAsync(path);
   foreach(var variant in new[]{0,1})
   {
    var ev=variant==0?0:.5;var label=index%2==0?(variant==0?"B":"A"):(variant==0?"A":"B");var id=$"Scene{index:00}-{label}";
    var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"Existing production exposure",true,new Dictionary<string,double>{{"exposure",ev}})]);
    var rendered=pipeline.Render(master.Image,null,stack,maximumEdge:1200);var bitmap=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(rendered.Pixels),master.Orientation);var image=Path.Combine(output,id+".private.png");StudioQuickExport.Encode(bitmap,path,image,StudioExportFormat.Png);
    var pixels=rendered.ProcessingPixels!.Rgb32.Span;long zeros=0,saturated=0;foreach(var value in pixels){if(value<=0)zeros++;if(value>=1)saturated++;}
    rows.Add(new{id,sourceSha256=sha,existingExposureEv=ev,privateFile=Path.GetFileName(image),fileSha256=SonyProbe.Hash(image),bitmap.PixelWidth,bitmap.PixelHeight,zeroChannels=zeros,saturatedChannels=saturated,channelCount=pixels.Length,semanticSceneLabels="UNREVIEWED: backlit/skin/saturated/gray/highISO cannot be invented from filenames",photographerChoice="NOT_RUN",algorithmComparison="Only existing legacy product exposure parameters; NOT ACES/OpenDRT/darktable equivalence"});
   }
  }
  File.WriteAllText(Path.Combine(output,"PRIVATE_KEY_AND_NUMERIC.json"),JsonSerializer.Serialize(new{status="PRIVATE_ANONYMOUS_REVIEW_PACK_CREATED_NOT_REVIEWED",rows,noUploads=true,productionCurveReplaced=false},new JsonSerializerOptions{WriteIndented=true}));
  File.WriteAllText(Path.Combine(output,"REVIEW.private.md"),"# 匿名照片参数对照\n仅现有产品两种曝光设置，不是新曲线优劣证据。请按Scene编号选A/B/无偏好，记录高光、肤色、饱和色、灰阶、暗部噪声；不适用类别写N/A。未评分前不设定优胜者。\n");
 }
}
