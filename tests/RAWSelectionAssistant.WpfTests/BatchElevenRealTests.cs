using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchElevenRealTests
{
    private static ColorAdjustmentStack Edited()=>new([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Exposure",NumericParameters:new Dictionary<string,double>{{"exposure",1.25}}),new(Guid.NewGuid(),ColorStudioNodeType.WhiteBalance,"Warm",NumericParameters:new Dictionary<string,double>{{"temperature",15}})],ProcessingVersion:2);
    [TestMethod]
    [DoNotParallelize]
    public async Task BatchElevenAuthorizedRawMemoryAndColor()
    {
        var corpus=Environment.GetEnvironmentVariable("KITAO_BATCH11_CORPUS");var evidence=Environment.GetEnvironmentVariable("KITAO_BATCH11_EVIDENCE");
        if(string.IsNullOrEmpty(corpus)||string.IsNullOrEmpty(evidence)){Assert.Inconclusive("Explicit authorized company fixtures required.");return;}
        Directory.CreateDirectory(evidence);var all=new List<object>();var process=Process.GetCurrentProcess();
        var fixtures=new[]{("TNAN8886.CR3","B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401"),("DSC09916.ARW","6F596BB234EE5DE206C66594BC84205349C363FD6A89AE188FF76DAB7C5E53B9"),("DSCF0347.RAF","52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865")};
        foreach(var (name,hash) in fixtures)
        {
            using var temp=new Temp();var source=Path.Combine(corpus,name);var rows=new List<object>();var watch=Stopwatch.StartNew();double last=0;
            void Stage(string stage){process.Refresh();var ms=watch.Elapsed.TotalMilliseconds;rows.Add(new{stage,milliseconds=ms-last,elapsedMs=ms,ledger=StudioGlobalResources.AccountedBytes,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64,managed=GC.GetTotalMemory(false),heap=GC.GetGCMemoryInfo().HeapSizeBytes,gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2)});last=ms;}
            try
            {
                using(var input=File.OpenRead(source))Assert.AreEqual(hash,Convert.ToHexString(await SHA256.HashDataAsync(input)));Stage("source-hash");
                var pipeline=new RawMatchTiff16ProductPipeline(new LibRawDecoder());var decoded=await pipeline.DecodeFrozenMasterAsync(source);Stage("legacy-decode-including-source-hash");
                var recipe=new InstantInputRecipe(2,"CurrentEdits",Guid.NewGuid(),Stack:Edited(),Film:new(true,GrainAmount:5,Seed:21)){LegacyRaw=new(1,2,hash)};
                var definition=LegacyRawInstantInput.Create(recipe);var master=LegacyRawInstantInput.Render(decoded,definition,pipeline,stage:Stage);Stage("current-edit-master-ready");rows.Add(new{stage="memory-plan",plan=BoundedLegacyRawRenderer.Plan(decoded.Width,decoded.Height,recipe.Look,recipe.Stack,recipe.Film)});
                var original=RawImageOrientation.NormalizePixels(decoded.Image);Assert.IsFalse(original.Rgb32.Span.SequenceEqual(master.Image.Rgb32.Span));
                definition=definition with{InstantFilm=new(true,AlgorithmVersion:2){PaperGeometry=new()}};
                var colorSink=0d;for(var y=0;y<master.Image.Height;y++)for(var x=0;x<master.Image.Width;x++){var i=(y*master.Image.Width+x)*3;var rgb=master.Image.Rgb32.Span;colorSink+=InstantFilmPipeline.Apply(new(rgb[i],rgb[i+1],rgb[i+2]),(x+.5)/master.Image.Width,(y+.5)/master.Image.Height,definition.InstantFilm!).X;}Stage("instant-effects-only-full-source-scan");Assert.IsTrue(double.IsFinite(colorSink));
                var hq=ProfessionalRawPipeline.RenderPreview(master,definition,1600);Stage("instant-effects-plus-HQ-cold");
                var block=ProfessionalRawPipeline.RenderRegion(master,definition,50,50,256,256);Stage("native-block");
                var restored=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(definition))!;CollectionAssert.AreEqual(block.Rgb32.ToArray(),ProfessionalRawPipeline.RenderRegion(master,restored,50,50,256,256).Rgb32.ToArray());Stage("definition-reopen-block-validation");
                var formatResults=new List<object>();
                foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
                {
                    var path=temp.File(format+".out");await ProfessionalRawExport.WriteAsync(master,definition,path,format);Stage(format+"-render-and-encode-with-built-in-validation");
                    var verification=ProfessionalRawExport.Inspect(path,master,definition,format);Stage(format+"-independent-call-readback");using var stream=File.OpenRead(path);var outputHash=Convert.ToHexString(await SHA256.HashDataAsync(stream));Stage(format+"-hash");formatResults.Add(new{format=format.ToString(),verification,outputHash});
                }
                using(var unchanged=File.OpenRead(source))Assert.AreEqual(hash,Convert.ToHexString(await SHA256.HashDataAsync(unchanged)));Stage("source-unchanged-hash");
                all.Add(new{name,status="PASS",decoded.Width,decoded.Height,definition,rows,formatResults});
            }
            catch(Exception error){all.Add(new{name,status="REJECTED_OR_FAILED",error=error.ToString(),rows});}
            finally{await File.WriteAllTextAsync(Path.Combine(evidence,"real-cold-paths.json"),JsonSerializer.Serialize(all,new JsonSerializerOptions{WriteIndented=true}));}
            GC.Collect();GC.WaitForPendingFinalizers();
        }
        // Two actual workspace VMs use the authorized Canon input, independent caches/versions; no UI window is launched.
        var pressure=new List<object>();var workspaces=new List<ReferenceColorWorkspaceViewModel>();
        void Sample(string stage){process.Refresh();pressure.Add(new{stage,ledger=StudioGlobalResources.AccountedBytes,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64,managed=GC.GetTotalMemory(false),heap=GC.GetGCMemoryInfo().HeapSizeBytes,gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2)});}
        try
        {
            using var temp=new Temp();
            for(var i=0;i<2;i++)
            {
                var w=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root));workspaces.Add(w);await w.LoadTargetAsync(Path.Combine(corpus,"TNAN8886.CR3"));Assert.IsNotNull(w.ActiveTarget?.RawMaster,w.StatusText);
                w.Editor.ApplyTargetSnapshot(null,null,Edited(),render:false);w.InstantCameraAnimation=false;w.InstantPaperFrame=false;w.InstantLayout="LegacyFree";
                await w.GenerateInstantFilmAsync(false);Assert.AreEqual(6,w.ActiveTarget!.RawDefinition?.ProcessingVersion,w.StatusText);Sample("workspace-"+i+"-master-hq");
            }
            await Task.WhenAll(workspaces.Select(w=>w.UpdateProfessionalViewportAsync(new Rect(0,0,1024,768),1,1)));Sample("queued-tiles");
            var saved=temp.File("real.ptstudio.json");await workspaces[0].SaveSessionAsync(saved);Sample("save-session");
            var sessionPixels=ProfessionalRawPipeline.RenderRegion(workspaces[0].ActiveTarget!.LinearRawMaster!,workspaces[0].ActiveTarget.RawDefinition!,0,0,64,64).Rgb32.ToArray();
            workspaces[1].Dispose();using(var reopen=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root))){await reopen.LoadSessionAsync(saved);Assert.AreEqual(6,reopen.ActiveTarget!.RawDefinition!.ProcessingVersion);CollectionAssert.AreEqual(sessionPixels,ProfessionalRawPipeline.RenderRegion(reopen.ActiveTarget.LinearRawMaster!,reopen.ActiveTarget.RawDefinition,0,0,64,64).Rgb32.ToArray());}Sample("real-reopen");
            IReadOnlyList<string>? prepared=null;workspaces[0].OpenPublishing=paths=>prepared=paths;await workspaces[0].PreparePublishingCommand.ExecuteAsync(null);Assert.IsNotNull(prepared,workspaces[0].ExportFailureSummary);Sample("publishing-full-intermediate");
            var options=new PublishingOptions(new(true,PublishingSizeMode.LongestEdge,LongestEdge:900),false,[],PublishingOutputFormat.Png);var published=temp.File("published.png");var renderer=new RAWSelectionAssistant.Services.Publishing.WpfPublishingRenderer();await renderer.RenderAsync(prepared[0],published,options);await renderer.VerifyPixelsAsync(prepared[0],published,options,default);Sample("publishing-resize-readback");
            var active=workspaces[0].ActiveTarget.LinearRawMaster!;using(var reservation=StudioGlobalResources.ReserveWork(1900L*1024*1024)){Sample("ledger-pressure-not-physical-allocation");Assert.IsTrue(float.IsFinite(active.Image.Rgb32.Span[0]));Assert.Throws<InvalidOperationException>(()=>StudioGlobalResources.ReserveWork(StudioGlobalResources.TotalLimit));}
            await workspaces[0].LoadTargetAsync(Path.Combine(corpus,"DSC09916.ARW"));Sample("switch-to-rejected-real-source-preserve-state");
            await workspaces[0].LoadTargetAsync(Path.Combine(corpus,"TNAN8886.CR3"));Sample("switch-back");
            workspaces[0].QuickExportFormat=StudioExportFormat.Tiff;var export=workspaces[0].ExportSelectedCommand.ExecuteAsync(null);workspaces[0].Dispose();await export;Sample("close-export-cancel");
        }
        catch(Exception e){pressure.Add(new{status="FAILED",error=e.ToString()});throw;}
        finally{foreach(var w in workspaces)w.Dispose();Sample("disposed");GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Sample("after-measurement-GC");await File.WriteAllTextAsync(Path.Combine(evidence,"real-workspace-pressure.json"),JsonSerializer.Serialize(pressure,new JsonSerializerOptions{WriteIndented=true}));}
    }
    private sealed class Temp:IDisposable{public string Root{get;}=Path.Combine(Path.GetTempPath(),"Kitao-batch11-"+Guid.NewGuid());public Temp()=>Directory.CreateDirectory(Root);public string File(string name)=>Path.Combine(Root,name);public void Dispose(){try{Directory.Delete(Root,true);}catch{}}}
    private sealed class Dialogs(string root):IDialogService
    {
        public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string message){}public void ShowError(string message){}public bool Confirm(string message,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
    }
}
