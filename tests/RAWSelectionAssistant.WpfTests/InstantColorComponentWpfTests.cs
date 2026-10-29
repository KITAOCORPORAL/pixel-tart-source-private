using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public class InstantColorComponentWpfTests
{
    static string Hash(BitmapSource b){var f=new FormatConvertedBitmap(b,PixelFormats.Rgba64,null,0);var bytes=new byte[f.PixelWidth*f.PixelHeight*8];f.CopyPixels(bytes,f.PixelWidth*8,0);return Convert.ToHexString(SHA256.HashData(bytes));}
    [TestMethod]public Task OptionalModelsGalleryPreviewDevelopFreezeReopenAndAllExports()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Environment.GetEnvironmentVariable("KITAO_COLOR_COMPONENT_EVIDENCE")??Path.Combine(Path.GetTempPath(),"instant-color-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var data=new byte[96*64*3];for(int i=0;i<data.Length;i++)data[i]=(byte)(i%251);var b=BitmapSource.Create(96,64,96,96,PixelFormats.Rgb24,null,data,96*3);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));var source=Path.Combine(root,"synthetic.png");using(var f=File.Create(source))e.Save(f);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(source);vm.EnableLightPrintWorkspace();vm.InstantCameraAnimation=false;
        var writer=typeof(ReferenceColorWorkspaceViewModel).Assembly.GetType("RAWSelectionAssistant.Services.InstantV3DirectExport")!.GetMethod("Write",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)!;var records=new List<object>();
        foreach(var kind in new[]{"color","bw"})
        {
            if(vm.IsLightPrintLocked)vm.ReshootLightPrintCommand.Execute(null);vm.SelectedPhotographerPaperId=kind=="bw"?"itype-bw":"itype-color";vm.InstantV3ColorChoice=kind=="bw"?"风格近似 · 黑白V1":"风格近似 · 彩色V1";vm.SelectedLightPrintFrameId="orig.03";await vm.LightPrintPreviewWork;Assert.IsTrue(vm.LightPrintStatus.Contains("就绪"),vm.LightPrintStatus);var preview=Hash(vm.Editor.MatchedImage!);
            await vm.PreviewPhotographerPapersCommand.ExecuteAsync(null);Assert.IsTrue(vm.PhotographerPapers.All(p=>p.Preview is not null),vm.PaperPreviewStatus);
            await vm.DevelopLightPrintCommand.ExecuteAsync(null);Assert.AreEqual(preview,Hash(vm.Editor.MatchedImage!));var d=vm.ActiveTarget!.RawDefinition!;var c=d.InstantFilm!.V3!.ColorComponent;Assert.IsNotNull(c,vm.StatusText);Assert.AreEqual(4,d.InstantFilm.V3.AlgorithmVersion);Assert.IsTrue(vm.InstantV3ColorEvidence.Contains("非相机实测"));
            var before=JsonSerializer.Serialize(d);foreach(var format in new[]{StudioExportFormat.Png,StudioExportFormat.Tiff,StudioExportFormat.Jpeg}){var path=Path.Combine(root,kind+"-"+format+(format==StudioExportFormat.Png?".png":format==StudioExportFormat.Tiff?".tif":".jpg"));await (Task)writer.Invoke(null,[vm.ActiveTarget.LinearRawMaster!,d,path,format,CancellationToken.None,null])!;Assert.IsTrue(File.Exists(path));}
            var session=Path.Combine(root,kind+".ptstudio.json");await vm.SaveSessionAsync(session);using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs());await reopened.LoadSessionAsync(session);Assert.AreEqual(before,JsonSerializer.Serialize(reopened.ActiveTarget!.RawDefinition));Assert.AreEqual(preview,Hash(reopened.Editor.MatchedImage!));Assert.AreEqual(c.ModelSha256,reopened.ActiveTarget.RawDefinition!.InstantFilm!.V3!.ColorComponent!.ModelSha256);records.Add(new{kind,modelId=c.Id,modelHash=c.ModelSha256,previewHash=preview,recipe=before,session});
        }
        await File.WriteAllTextAsync(Path.Combine(root,"CANDIDATE_RESULTS.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,records,scope="synthetic production pipeline; no camera measurement"},new JsonSerializerOptions{WriteIndented=true}));
    });
    [TestMethod]public Task IndependentOptionalModelReopen()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Environment.GetEnvironmentVariable("KITAO_COLOR_COMPONENT_EVIDENCE");if(root is null||!File.Exists(Path.Combine(root,"CANDIDATE_RESULTS.json"))){Assert.Inconclusive("Prior process fixture required");return;}using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"CANDIDATE_RESULTS.json")));Assert.AreNotEqual(Environment.ProcessId,doc.RootElement.GetProperty("pid").GetInt32());using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());foreach(var row in doc.RootElement.GetProperty("records").EnumerateArray()){await vm.LoadSessionAsync(row.GetProperty("session").GetString()!);Assert.AreEqual(row.GetProperty("recipe").GetString(),JsonSerializer.Serialize(vm.ActiveTarget!.RawDefinition));Assert.AreEqual(row.GetProperty("previewHash").GetString(),Hash(vm.Editor.MatchedImage!));}await File.WriteAllTextAsync(Path.Combine(root,"INDEPENDENT_MODEL_READBACK.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,exact=true}));
    });
    sealed class Dialogs:IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string t,string f,bool m)=>[];public string? ChooseFolder(string t,string? initialDirectory=null)=>null;public string? ChooseSaveFile(string t,string f,string e,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool b)=>false;public void RevealFile(string p){}
    }
}
