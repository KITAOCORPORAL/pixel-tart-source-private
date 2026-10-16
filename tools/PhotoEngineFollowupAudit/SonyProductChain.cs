using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;

internal static class SonyProductChain
{
    public static async Task Run(string copy,string output)
    {
        if(Directory.Exists(output)||SonyProbe.Hash(copy)!=SonyProbe.AuthorizedSha)throw new InvalidDataException("New output and exact authorized copy required");
        Directory.CreateDirectory(output);var rows=new List<object>();string status="FAILED";var timer=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var definition=new ProfessionalRawDefinition(WhiteBalance:"CameraGains",RedGain:1.05,GreenGain:1,BlueGain:.95,ExposureEv:.5);
            var decoder=new ProfessionalRawDecoder();var master=await decoder.DecodeAsync(copy,definition);
            rows.Add(new{operation="PV4-real-nondefault-decode",status="PASSED",master.Image.Width,master.Image.Height,master.Image.Orientation,master.Contract,ms=timer.Elapsed.TotalMilliseconds});
            var preview=ProfessionalRawPipeline.RenderPreview(master,definition,768);var neutral=ProfessionalRawPipeline.RenderPreview(master,definition with{ExposureEv=0},768);
            if(preview.Rgb32.Span.SequenceEqual(neutral.Rgb32.Span))throw new InvalidDataException("Non-default exposure had no effect");
            var size=ProfessionalRawPipeline.OrientedSize(master);var oneToOne=ProfessionalRawPipeline.RenderRegion(master,definition,0,0,64,64);var region=ProfessionalRawPipeline.RenderRegion(master,definition,Math.Min(500,size.Width-64),Math.Min(300,size.Height-64),64,64);
            rows.Add(new{operation="fit-1to1-region",status="PASSED",preview=new{preview.Width,preview.Height},oneToOne=new{oneToOne.Width,oneToOne.Height},region=new{region.Width,region.Height},oriented=size,nonDefaultChangedPixels=true});
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();try{ProfessionalRawPipeline.RenderRegion(master,definition,0,0,64,64,cancelled.Token);throw new InvalidDataException("pre-cancel not observed");}catch(OperationCanceledException){rows.Add(new{operation="pre-cancel-region",status="PASSED"});}
            }
            var id=Guid.NewGuid();var versionId=Guid.NewGuid();var version=new StudioEditVersion(versionId,"Sony nondefault",DateTimeOffset.UtcNow,null,null,SonyProbe.AuthorizedSha,null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto){RawDefinition=definition,RawInput=master.Contract};
            var target=new ColorStudioSessionTarget(id,copy,null,true,null,null,null,SourceHash:SonyProbe.AuthorizedSha,EditVersions:[version],ActiveEditVersionId:versionId,RawDefinition:definition,RawInput:master.Contract);
            var sessionPath=Path.Combine(output,"session.private.ptstudio.json");await ColorStudioSessionStore.SaveAsync(sessionPath,new([target],id));var reopened=await ColorStudioSessionStore.LoadAsync(sessionPath);var restored=reopened.Targets.Single();
            if(restored.RawDefinition!=definition||restored.SourceHash!=SonyProbe.AuthorizedSha||restored.ActiveEditVersionId!=versionId)throw new InvalidDataException("Session contract mismatch");
            var secondMaster=await decoder.DecodeAsync(copy,restored.RawDefinition!);var reopenedPreview=ProfessionalRawPipeline.RenderPreview(secondMaster,restored.RawDefinition!,768);
            if(!master.Image.Rgb32.Span.SequenceEqual(secondMaster.Image.Rgb32.Span)||!preview.Rgb32.Span.SequenceEqual(reopenedPreview.Rgb32.Span))throw new InvalidDataException("Save/reopen/redecode changed master or preview");
            rows.Add(new{operation="session-save-reopen-real-redecode",status="PASSED",masterPixelsExact=true,previewPixelsExact=true,decoder=master.Contract.DecoderVersion});
            foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
            {
                var path=Path.Combine(output,"adjusted.private"+(format==StudioExportFormat.Tiff?".tif":format==StudioExportFormat.Png?".png":".jpg"));
                var result=await ProfessionalRawExport.WriteAsync(master,definition,path,format);var inspected=ProfessionalRawExport.Inspect(path,master,definition,format);
                rows.Add(new{operation="export-readback",status="PASSED",format=format.ToString(),inspection=inspected,fileSha256=SonyProbe.Hash(path),ms=timer.Elapsed.TotalMilliseconds});
            }
            if(SonyProbe.Hash(copy)!=SonyProbe.AuthorizedSha)throw new InvalidDataException("Source changed");status="PASSED_PRODUCT_BACKEND_CHAIN_NOT_DESKTOP";
        }
        catch(Exception error){rows.Add(new{operation="failure",status="FAILED",error=error.ToString()});}
        finally
        {
            File.WriteAllText(Path.Combine(output,"SONY_DSC08015_CHAIN.json"),JsonSerializer.Serialize(new{status,identity="Sony_DSC08015",sourceSha256=SonyProbe.AuthorizedSha,rows,desktop="NOT_RUN",foregroundSwitchCancel="NOT_RUN",physicalDpi="NOT_RUN",privateDerivedImagesKeptLocal=true,allSonySupportClaim=false},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
