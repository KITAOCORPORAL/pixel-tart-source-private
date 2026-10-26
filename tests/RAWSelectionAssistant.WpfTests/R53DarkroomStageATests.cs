using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class R53DarkroomStageATests
{
    static string Image(string root){var p=Path.Combine(root,"synthetic.png");StudioQuickExport.Encode(BitmapSource.Create(32,24,96,96,PixelFormats.Rgb24,null,Enumerable.Repeat((byte)90,32*24*3).ToArray(),32*3),p,p,StudioExportFormat.Png);return p;}
    static ColorStudioToolParameter Exposure=>ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure");
    static async Task Render(ReferenceColorWorkspaceViewModel vm){await vm.Editor.ApplyCommand.ExecuteAsync(null);await vm.AnalysisWork;}
    static byte[] Pixels(BitmapSource image){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb24,null,0);var b=new byte[rgb.PixelWidth*rgb.PixelHeight*3];rgb.CopyPixels(b,rgb.PixelWidth*3,0);return b;}
    [TestMethod]public Task SamePhotoSimpleProfessionalSaveReopenAndExportShareDefinition()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r53-shared-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{var input=Image(root);var dialogs=new Dialogs(root);using var vm=new ReferenceColorWorkspaceViewModel(dialogs);await vm.LoadTargetAsync(input);vm.Editor.SetToolParameter(Exposure,.5);await Render(vm);var selected=vm.Editor.SelectedAdjustmentNode?.Id;var definition=JsonSerializer.Serialize(vm.Editor.AdjustmentStack);var pixels=Pixels(vm.Editor.MatchedImage!);
        vm.R53Professional=true;vm.Editor.SetToolParameter(Exposure,.75);await Render(vm);vm.R53Professional=false;Assert.AreEqual(.75,vm.Editor.ToolValue(Exposure));vm.Editor.UndoAdjustmentCommand.Execute(null);await Render(vm);Assert.AreEqual(definition,JsonSerializer.Serialize(vm.Editor.AdjustmentStack));CollectionAssert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));
        var session=Path.Combine(root,"shared.ptstudio.json");await vm.SaveSessionAsync(session);using var reopened=new ReferenceColorWorkspaceViewModel(dialogs);await reopened.LoadSessionAsync(session);await Render(reopened);Assert.AreEqual(definition,JsonSerializer.Serialize(reopened.Editor.AdjustmentStack));CollectionAssert.AreEqual(pixels,Pixels(reopened.Editor.MatchedImage!));reopened.ActiveTarget!.IsSelected=true;await reopened.ExportSelectedCommand.ExecuteAsync(null);var exported=Directory.GetFiles(root,"*_仿色.png").Single();var receipt=JsonSerializer.Deserialize<StudioExportReceipt>(await File.ReadAllTextAsync(exported+".kitao-export.json"))!;Assert.AreEqual(reopened.ActiveTarget.ActiveEditVersionId,receipt.EditVersionId);Assert.AreEqual(reopened.Editor.AdjustmentStack.ProcessingVersion,receipt.ProcessingVersion);
        var evidence=Environment.GetEnvironmentVariable("KITAO_R53_EVIDENCE");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);File.Copy(session,Path.Combine(evidence,"shared.ptstudio.json"));File.Copy(exported,Path.Combine(evidence,"shared-export.png"));File.Copy(input,Path.Combine(evidence,"synthetic.png"));}
        }finally{Directory.Delete(root,true);}
    });
    [TestMethod]public Task OriginalAndMainVariantDeleteConfirmUndoAndIsolationPersist()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r53-variants-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{var dialogs=new Dialogs(root);using var vm=new ReferenceColorWorkspaceViewModel(dialogs);await vm.LoadTargetAsync(Image(root));var original=vm.R53Variants.Single(v=>v.IsOriginal);var first=vm.ActiveTarget!.ActiveEditVersionId;vm.Editor.SetToolParameter(Exposure,.3);await Render(vm);await vm.CreateR53VariantAsync(false);var copy=vm.ActiveTarget.ActiveEditVersionId;Assert.AreNotEqual(first,copy);vm.Editor.SetToolParameter(Exposure,.8);await Render(vm);await vm.SwitchEditVersionAsync(first!.Value);Assert.AreEqual(.3,vm.Editor.ToolValue(Exposure));await vm.SwitchEditVersionAsync(copy!.Value);Assert.AreEqual(.8,vm.Editor.ToolValue(Exposure));vm.SetR53MainVariant();vm.R53VariantRating=4;vm.R53VariantColorLabel="绿";Assert.AreEqual(copy,vm.R53MainVariantId);
        dialogs.Accept=false;await vm.DeleteR53VariantAsync();Assert.IsFalse(vm.ActiveTarget.EditVersions.Single(v=>v.Id==copy).Archived);dialogs.Accept=true;await vm.DeleteR53VariantAsync();Assert.IsTrue(vm.ActiveTarget.EditVersions.Single(v=>v.Id==copy).Archived);await vm.CreateR53VariantAsync(true);var later=vm.ActiveTarget.ActiveEditVersionId;await vm.UndoR53VariantDeleteAsync();Assert.IsTrue(vm.ActiveTarget.EditVersions.Any(v=>v.Id==later),"Undo delete must not erase a later variant");Assert.IsFalse(vm.ActiveTarget.EditVersions.Single(v=>v.Id==copy).Archived);await vm.SwitchEditVersionAsync(original.Id);await vm.DeleteR53VariantAsync();Assert.IsFalse(vm.ActiveTarget.EditVersions.Single(v=>v.Id==original.Id).Archived);vm.Editor.SetToolParameter(Exposure,2);Assert.AreEqual(0d,vm.Editor.ToolValue(Exposure));await vm.CreateR53VariantAsync(true);Assert.AreEqual(0d,vm.Editor.ToolValue(Exposure));
        var path=Path.Combine(root,"variants.ptstudio.json");await vm.SaveSessionAsync(path);using var next=new ReferenceColorWorkspaceViewModel(dialogs);await next.LoadSessionAsync(path);Assert.AreEqual(copy,next.R53MainVariantId);Assert.HasCount(1,next.R53Variants.Where(v=>v.IsOriginal));await next.PrepareR53MainExportAsync();Assert.AreEqual(copy,next.ActiveTarget!.ActiveEditVersionId);Assert.AreEqual(.8,next.Editor.ToolValue(Exposure));Assert.AreEqual(4,next.R53VariantRating);Assert.AreEqual("绿",next.R53VariantColorLabel);}
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]public Task LockedNodesBlockParametersNameOrderDeleteButKeepVisibilityUndo()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r53-lock-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root));vm.Editor.AddAdjustmentNodeCommand.Execute("BasicTone");vm.Editor.SetToolParameter(Exposure,.4);var node=vm.Editor.ToolNode(ColorStudioNodeType.BasicTone)!;vm.Editor.SelectedAdjustmentNode=node;vm.Editor.SelectedNodeLocked=true;var before=JsonSerializer.Serialize(vm.Editor.AdjustmentStack);vm.Editor.SetToolParameter(Exposure,1.4);vm.Editor.RenameSelectedAdjustmentNode("must-not-change");vm.Editor.DeleteAdjustmentNodeCommand.Execute(null);Assert.AreEqual(before,JsonSerializer.Serialize(vm.Editor.AdjustmentStack));vm.Editor.SelectedNodeEnabled=false;Assert.IsFalse(vm.Editor.SelectedNodeEnabled);vm.Editor.UndoAdjustmentCommand.Execute(null);Assert.IsTrue(vm.Editor.SelectedNodeEnabled);Assert.IsTrue(vm.Editor.SelectedNodeLocked);vm.Editor.SelectedNodeLocked=false;vm.Editor.SetToolParameter(Exposure,.6);Assert.AreEqual(.6,vm.Editor.ToolValue(Exposure));
        var system=vm.Editor.AdjustmentStack with{Nodes=vm.Editor.AdjustmentStack.Nodes.Select(n=>n.Id==node.Id?n with{SystemLocked=true}:n).ToArray()};vm.Editor.ApplyTargetSnapshot(null,null,system,false);vm.Editor.SelectedAdjustmentNode=vm.Editor.AdjustmentNodes.Single(n=>n.Id==node.Id);vm.Editor.SelectedNodeLocked=false;Assert.IsTrue(vm.Editor.SelectedNodeLocked);vm.Editor.SetToolParameter(Exposure,2);Assert.AreEqual(.6,vm.Editor.ToolValue(Exposure));var session=Path.Combine(root,"locks.ptstudio.json");await vm.SaveSessionAsync(session);using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await reopened.LoadSessionAsync(session);Assert.IsTrue(reopened.Editor.AdjustmentNodes.Single(n=>n.Id==node.Id).SystemLocked);}
        finally{Directory.Delete(root,true);}await Task.CompletedTask;
    });
    [TestMethod]public Task DipBreakpointsAndProfessionalNodeFilmstripOrderAreRealLayout()=>RunSta(async()=>
    {
        EnsureTestApplication();using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(Path.GetTempPath()));var view=new PhotoGradePageView{DataContext=vm};
        foreach(var width in new[]{1100d,1366d,1600d}){view.Measure(new Size(width,900));view.Arrange(new Rect(0,0,width,900));view.UpdateLayout();view.ApplyR53Layout();Assert.AreEqual(width>=1200?Visibility.Visible:Visibility.Collapsed,((Border)view.FindName("R53Resources")).Visibility);}
        vm.R53Professional=true;view.ApplyR53Layout();Assert.AreEqual(Visibility.Visible,((Border)view.FindName("R53SerialNodes")).Visibility);Assert.IsTrue(Grid.GetRow((Border)view.FindName("FilmstripArea"))>Grid.GetRow((Border)view.FindName("R53SerialNodes")));view.DataContext=null;await Task.CompletedTask;
    });
    [TestMethod]public Task BrushAndCompareShortcutHaveSingleNonConflictingMeaning()=>RunSta(async()=>
    {
        EnsureTestApplication();using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(Path.GetTempPath()));var view=new PhotoGradePageView{DataContext=vm};vm.Editor.ViewMode="仿色结果";Assert.IsTrue(view.ExecuteR53PointerShortcut(System.Windows.Input.Key.B));Assert.AreEqual("仿色结果",vm.Editor.ViewMode);Assert.IsTrue(((TextBlock)view.FindName("ToolLabel")).Text.StartsWith("画笔"));Assert.IsTrue(view.ExecuteR53PointerShortcut(System.Windows.Input.Key.Oem5));Assert.AreEqual("原片",vm.Editor.ViewMode);Assert.IsTrue(view.ExecuteR53PointerShortcut(System.Windows.Input.Key.Oem5));Assert.AreEqual("仿色结果",vm.Editor.ViewMode);view.DataContext=null;await Task.CompletedTask;
    });
    [TestMethod]public Task WideInspectorReserves420DipAndReleasesItsOwnerSubscription()=>RunSta(async()=>
    {
        EnsureTestApplication();using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(Path.GetTempPath()));var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1920,920));view.Arrange(new Rect(0,0,1920,920));view.UpdateLayout();
        var method=typeof(PhotoGradePageView).GetMethod("R53Inspect3D",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;method.Invoke(view,[view,new RoutedEventArgs()]);view.UpdateLayout();var dock=(Border)view.FindName("R53InspectDock");Assert.AreEqual(Visibility.Visible,dock.Visibility);Assert.IsTrue(dock.ActualWidth>=420);Assert.IsTrue(((FrameworkElement)view.FindName("ViewerArea")).ActualWidth>=560);
        method.Invoke(view,[view,new RoutedEventArgs()]);Assert.AreEqual(Visibility.Collapsed,dock.Visibility);view.DataContext=null;await Task.CompletedTask;
    });
    [TestMethod]public Task MainVariantOutputAndDebouncedExistingSessionSaveUseSharedPipeline()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r53-main-auto-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root));vm.Editor.SetToolParameter(Exposure,.25);await Render(vm);var main=vm.ActiveTarget!.ActiveEditVersionId;vm.SetR53MainVariant();await vm.CreateR53VariantAsync(false);vm.Editor.SetToolParameter(Exposure,.9);await Render(vm);var current=vm.ActiveTarget.ActiveEditVersionId;Assert.AreNotEqual(main,current);vm.ActiveTarget.IsSelected=true;await vm.ExportSelectedCommand.ExecuteAsync(null);var output=Directory.GetFiles(root,"*_仿色.png").Single();var receipt=JsonSerializer.Deserialize<StudioExportReceipt>(await File.ReadAllTextAsync(output+".kitao-export.json"))!;Assert.AreEqual(main,receipt.EditVersionId);Assert.AreEqual(current,vm.ActiveTarget.ActiveEditVersionId,"Export must not silently change selected edit target");
        var session=Path.Combine(root,"autosave.ptstudio.json");vm.ActiveTarget.VariantsExpanded=true;await vm.SaveSessionAsync(session);vm.Editor.SetToolParameter(Exposure,1.1);await Task.Delay(1600);var saved=await ColorStudioSessionStore.LoadAsync(session);Assert.AreEqual(1.1,saved.Targets.Single().Stack!.Nodes.First(n=>n.Type==ColorStudioNodeType.BasicTone).NumericParameters["exposure"]);Assert.IsTrue(saved.Targets.Single().VariantsExpanded);}
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]public Task PersistentSyntheticDarkroomSampleAndOffscreenLayoutEvidence()=>RunSta(async()=>
    {
        var output=Environment.GetEnvironmentVariable("KITAO_R53_EVIDENCE");if(string.IsNullOrWhiteSpace(output)){Assert.Inconclusive("Explicit synthetic sample output required");return;}EnsureTestApplication();Directory.CreateDirectory(output);var image=Path.Combine(output,"r53-synthetic.png");
        var pixels=new byte[128*96*3];for(int y=0;y<96;y++)for(int x=0;x<128;x++){var o=(y*128+x)*3;pixels[o]=(byte)(x*2);pixels[o+1]=(byte)(y*2);pixels[o+2]=100;}
        StudioQuickExport.Encode(BitmapSource.Create(128,96,96,96,PixelFormats.Rgb24,null,pixels,128*3),image,image,StudioExportFormat.Png);
        using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(output));await vm.LoadTargetAsync(image);vm.Editor.SetToolParameter(Exposure,.35);await Render(vm);vm.R53VariantRating=3;vm.R53VariantColorLabel="绿";await vm.CreateR53VariantAsync(false);vm.Editor.AddAdjustmentNodeCommand.Execute("Curve");await Render(vm);vm.SetR53MainVariant();
        var view=new PhotoGradePageView{DataContext=vm};
        foreach(var pro in new[]{false,true})foreach(var width in new[]{1100,1600,1920})
        {
            vm.R53Professional=pro;view.Measure(new Size(width,920));view.Arrange(new Rect(0,0,width,920));view.UpdateLayout();view.ApplyR53Layout();view.UpdateLayout();var bitmap=new RenderTargetBitmap(width,920,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,$"offscreen-{(pro?"pro":"simple")}-{width}.png"));encoder.Save(stream);
        }
        var session=Path.Combine(output,"r53-stage-a.ptstudio.json");await vm.SaveSessionAsync(session);vm.ActiveTarget!.IsSelected=true;await vm.ExportSelectedCommand.ExecuteAsync(null);Assert.IsTrue(File.Exists(Path.Combine(output,"r53-synthetic_仿色.png")));view.DataContext=null;
        await File.WriteAllTextAsync(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(new{photo=vm.ActiveTarget.Id,variants=vm.R53Variants.Select(v=>new{v.Id,v.Name,v.IsOriginal,v.IsMain}),session,scope="Offscreen WPF layout and real shared session/export; not native/DPI screenshot"},new JsonSerializerOptions{WriteIndented=true}));
    });
    [TestMethod]
    public Task LegacySimpleControlsCannotBypassReferenceLocksOrOriginalReadOnly() => RunSta(async () =>
    {
        EnsureTestApplication();
        using var editor = new TetherReferenceModeViewModel();
        var now = DateTimeOffset.UtcNow;
        var pixels = new VisualPixelBuffer(1, 1, new byte[] { 60, 80, 100 });
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "synthetic", pixels));
        var source = new ReferenceLookSource(Guid.NewGuid(), analysis.AssetId, "合成参考", "", "test", 1, analysis);
        var look = new ReferenceLook(Guid.NewGuid(), "锁定参考", null, [source], new(MatchStrength: 37), now, now);
        var reference = ColorStudioLegacyMigration.Migrate(look).Stack;
        var locked = reference with { Nodes = reference.Nodes.Select(n => n with { UserLocked = true }).ToArray() };
        editor.ApplyTargetSnapshot(look, null, locked, false, Guid.NewGuid());
        var before = JsonSerializer.Serialize(editor.AdjustmentStack);
        var lookBefore = JsonSerializer.Serialize(editor.SelectedLook);
        editor.MatchStrength = 80;
        editor.KeepOriginalTone = !editor.KeepOriginalTone;
        editor.SelectedLook = look with { Name = "不能替换", Parameters = new(MatchStrength: 80) };
        editor.RestoreSchemeCommand.Execute(null);
        Assert.AreEqual(before, JsonSerializer.Serialize(editor.AdjustmentStack));
        Assert.AreEqual(lookBefore, JsonSerializer.Serialize(editor.SelectedLook), "Locked stack must also protect legacy look fields used by save/export");
        editor.ApplyTargetSnapshot(null, null, null, false, Guid.NewGuid());
        editor.Enabled = false;
        editor.ReadOnlyTarget = true;
        editor.SetToolParameter(Exposure, .8);
        editor.MatchStrength = 80;
        editor.KeepOriginalTone = true;
        editor.FilmEnabled = true;
        Assert.HasCount(0, editor.AdjustmentStack.Nodes);
        Assert.IsFalse(editor.Enabled, "Rejected edit must not change render-enable state");
        Assert.AreEqual(100, editor.MatchStrength);
        Assert.IsFalse(editor.FilmEnabled);
        await Task.CompletedTask;
    });
    [TestMethod]
    public Task CloseFlushPersistsImmediateEditsAndRejectsConcurrentFileChanges() => RunSta(async () =>
    {
        EnsureTestApplication();
        var root = Path.Combine(Path.GetTempPath(), "r53-close-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var vm = new ReferenceColorWorkspaceViewModel(new Dialogs(root));
            await vm.LoadTargetAsync(Image(root));
            var session = Path.Combine(root, "close.ptstudio.json");
            await vm.SaveSessionAsync(session);
            vm.Editor.SetToolParameter(Exposure, .7);
            Assert.IsTrue(await vm.FlushR53BeforeCloseAsync(), "Normal close flushes without waiting for 900ms timer");
            using var reopened = new ReferenceColorWorkspaceViewModel(new Dialogs(root));
            await reopened.LoadSessionAsync(session);
            Assert.AreEqual(.7, reopened.Editor.ToolValue(Exposure));
            vm.Editor.SetToolParameter(Exposure, .9);
            var external = await File.ReadAllTextAsync(session) + "\n";
            await File.WriteAllTextAsync(session, external);
            Assert.IsFalse(await vm.FlushR53BeforeCloseAsync(), "External edit must keep window open");
            Assert.AreEqual(external, await File.ReadAllTextAsync(session));
            Assert.AreEqual(.9, vm.Editor.ToolValue(Exposure), "Failed save retains the unsaved edit in memory");
            StringAssert.Contains(vm.R53SaveStatus, "保存失败");
            var dialogs = new Dialogs(root) { Accept = false };
            using var unsaved = new ReferenceColorWorkspaceViewModel(dialogs);
            await unsaved.LoadTargetAsync(Path.Combine(root, "synthetic.png"));
            unsaved.Editor.SetToolParameter(Exposure, .4);
            Assert.IsFalse(await unsaved.FlushR53BeforeCloseAsync(), "Cancel save and discard must leave application open");
            Assert.AreEqual(.4, unsaved.Editor.ToolValue(Exposure));
            dialogs.Accept = true;
            Assert.IsTrue(await unsaved.FlushR53BeforeCloseAsync(), "Explicit discard permits close without deleting original");
            Assert.IsTrue(File.Exists(Path.Combine(root, "synthetic.png")));
        }
        finally { Directory.Delete(root, true); }
    });
    [TestMethod]
    public Task ReopenedOriginalRemainsReadOnlyAndF2ChildBecomesEditable() => RunSta(async () =>
    {
        EnsureTestApplication();
        var root = Path.Combine(Path.GetTempPath(), "r53-original-reopen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var dialogs = new Dialogs(root);
            using var vm = new ReferenceColorWorkspaceViewModel(dialogs);
            await vm.LoadTargetAsync(Image(root));
            var original = vm.R53Variants.Single(v => v.IsOriginal).Id;
            await vm.SwitchEditVersionAsync(original);
            var session = Path.Combine(root, "original.ptstudio.json");
            await vm.SaveSessionAsync(session);
            using var reopened = new ReferenceColorWorkspaceViewModel(dialogs);
            await reopened.LoadSessionAsync(session);
            Assert.AreEqual(original, reopened.ActiveTarget!.ActiveEditVersionId);
            Assert.IsTrue(reopened.Editor.ReadOnlyTarget);
            reopened.Editor.SetToolParameter(Exposure, 1);
            Assert.AreEqual(0d, reopened.Editor.ToolValue(Exposure));
            await reopened.CreateR53VariantAsync(true);
            Assert.IsFalse(reopened.Editor.ReadOnlyTarget);
            reopened.Editor.SetToolParameter(Exposure, .5);
            Assert.AreEqual(.5, reopened.Editor.ToolValue(Exposure));
        }
        finally { Directory.Delete(root, true); }
    });
    sealed class Dialogs(string root):IDialogService
    {public bool Accept=true;public IReadOnlyList<string> ChooseFiles(string t,string f,bool m)=>[];public string? ChooseFolder(string t,string? p=null)=>root;public string? ChooseSaveFile(string t,string f,string e,string? n=null)=>null;public bool Confirm(string m,string t)=>Accept;public void ShowInfo(string m){}public void ShowError(string m){}public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string>x)=>x;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry>x)=>null;public bool ShowMediaDetails(MediaSelectionItem i,bool b)=>false;public void RevealFile(string p){}}
}

