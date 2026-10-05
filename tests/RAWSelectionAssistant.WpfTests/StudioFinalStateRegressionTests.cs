using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class StudioFinalStateRegressionTests
{
    [TestMethod]
    public Task InactiveStandaloneFilmExportsAndThumbnailsMatchActivatedEffectiveStack() => RunSta(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"Studio-final-film-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            // Match the product's 320px thumbnail decode width so this test compares
            // Film state, not codec resampling of a two-pixel source up to 320px.
            var colors=new byte[320*2*3];
            for(var p=0;p<640;p++){var offset=p*3;colors[offset]=(byte)(p%320<160?90:140);colors[offset+1]=(byte)(p%320<160?120:100);colors[offset+2]=(byte)(p%320<160?150:70);}
            var input=BitmapSource.Create(320,2,96,96,PixelFormats.Rgb24,null,colors,960);input.Freeze();
            var path=Path.Combine(root,"source.png");StudioQuickExport.Encode(input,path,path);
            var film=new PixelTartFilmSettings(true,"PT-W01",ProfileAmount:70,SpatialVersion:2);
            using var editor=new TetherReferenceModeViewModel();
            var exported=await editor.ProcessForExportAsync(path,null,film,CancellationToken.None,null);
            using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs());
            await workspace.LoadTargetAsync(path);var inactive=workspace.ActiveTarget!;
            var other=Path.Combine(root,"other.png");StudioQuickExport.Encode(input,other,other);
            await workspace.LoadTargetAsync(other);
            inactive.FilmSettingsSnapshot=film;await workspace.FlushThumbnailsAsync();
            Assert.AreNotEqual(inactive.Id,workspace.ActiveTarget!.Id);
            var thumbnail=inactive.Thumbnail!;
            editor.ApplyTargetSnapshot(null,film,null,false);
            var activated=ColorStudioBitmapRenderer.Render(input,editor.AdjustmentStack,null);
            Assert.AreEqual(activated.PixelWidth,thumbnail.PixelWidth);Assert.AreEqual(activated.PixelHeight,thumbnail.PixelHeight);
            CollectionAssert.AreEqual(Bytes(activated),Bytes(exported),"Actual export pixels must retain standalone Film.");
            CollectionAssert.AreEqual(Bytes(activated),Bytes(thumbnail),"Inactive thumbnail pixels must retain standalone Film.");
            Assert.IsFalse(Bytes(input).SequenceEqual(Bytes(exported)));
        }
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]
    public Task ApplyingSchemeCanUndoBackToEffectiveOldStackAndFilm() => RunSta(()=>
    {
        using var editor=new TetherReferenceModeViewModel();
        var film=new PixelTartFilmSettings(true,"PT-C01",GrainAmount:23);
        var analysis=VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),"test",new VisualPixelBuffer(2,1,new byte[]{40,80,120,180,150,100})));
        var reference=new ReferenceLookSource(Guid.NewGuid(),analysis.AssetId,"测试参考","","test",1,analysis);
        var look=new ReferenceLook(Guid.NewGuid(),"原方案",null,[reference],new(MatchStrength:37,ToneStrength:61),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,Film:film);
        editor.ApplyTargetSnapshot(look,film,null,false,Guid.NewGuid());var before=editor.AdjustmentStack;
        var oldReference=before.Nodes.Single(n=>n.Type==ColorStudioNodeType.ReferenceMatch);
        var replacement=new ColorAdjustmentStack([
            oldReference with { Id=Guid.NewGuid(),NumericParameters=new Dictionary<string,double>(oldReference.NumericParameters){["match_strength"]=81} },
            new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Exposure",NumericParameters:new Dictionary<string,double>{{"exposure",.7}})],ProcessingVersion:2);
        editor.SelectedColorScheme=new(Guid.NewGuid(),"替换",replacement,DateTimeOffset.UtcNow);editor.ApplyColorSchemeCommand.Execute(null);
        Assert.IsFalse(editor.FilmEnabled);Assert.AreEqual(81,editor.SelectedLook!.Parameters.MatchStrength);editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(before),System.Text.Json.JsonSerializer.Serialize(editor.AdjustmentStack));
        Assert.AreEqual(film,editor.FilmSettings);Assert.AreEqual(look.Parameters,editor.SelectedLook!.Parameters);Assert.AreEqual(look.ReferenceLookId,editor.SelectedLook.ReferenceLookId);
        editor.RedoAdjustmentCommand.Execute(null);Assert.AreEqual(replacement.Nodes[0].Id,editor.AdjustmentStack.Nodes[0].Id);Assert.AreEqual(81,editor.SelectedLook.Parameters.MatchStrength);Assert.IsFalse(editor.FilmEnabled);
        return Task.CompletedTask;
    });
    private static byte[] Bytes(BitmapSource source){var input=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var bytes=new byte[input.PixelWidth*input.PixelHeight*4];input.CopyPixels(bytes,input.PixelWidth*4,0);return bytes;}
    private sealed class Dialogs:IDialogService
    {
        public string? ChooseFolder(string title,string? initialDirectory=null)=>null;
        public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];
        public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds)=>null;
        public void ShowInfo(string message){}public void ShowError(string message)=>Assert.Fail(message);
        public bool Confirm(string message,string title)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;
        public bool ShowMediaDetails(MediaSelectionItem item,bool showAdvancedDetails)=>false;public void RevealFile(string path){}
    }
}
