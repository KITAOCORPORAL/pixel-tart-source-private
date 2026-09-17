using System.IO;
using System.Security.Cryptography;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.WpfTests;

[TestClass] public sealed class PaletteCanvasObjectTests
{
    [TestMethod] public async Task PaletteObjectPersistsAsEditableDataAndNeverCreatesAnImageFile()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-PaletteCanvas",Guid.NewGuid().ToString("N"));
        try
        {
            var sourceAsset=Guid.NewGuid();var editor=new CanvasEditor(new());
            editor.AddPalette(40,60,new([new("#102030",210,.5,.12,.4),new("#405060",210,.2,.31,.35),new("#A0B0C0",210,.2,.69,.25)],[sourceAsset]));
            var palette=editor.Selected.Single();Assert.IsTrue(palette.IsPalette);Assert.IsFalse(palette.IsImage);Assert.AreEqual(sourceAsset,palette.Palette!.SourceAssetIds.Single());
            editor.Move(25,10);editor.Scale(1.2);editor.Duplicate();editor.Layer(false);editor.SetLocked(true);
            var store=new CanvasDocumentStore(root);await store.SaveAsync(editor.Document);var loaded=await store.LoadAsync(editor.Document.CanvasId);
            Assert.IsNotNull(loaded);Assert.HasCount(2,loaded!.Objects);Assert.IsTrue(loaded.Objects.All(item=>item.Palette is not null));Assert.IsEmpty(Directory.EnumerateFiles(root,"*.jpg"));Assert.IsEmpty(Directory.EnumerateFiles(root,"*.png"));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [TestMethod] public void CombinedPaletteKeepsAllSourceReferences()
    {
        var first=Guid.NewGuid();var second=Guid.NewGuid();var editor=new CanvasEditor(new());editor.AddPalette(0,0,new([new("#111111",0,0,.07,.34),new("#777777",0,0,.47,.33),new("#EEEEEE",0,0,.93,.33)],[first,second],true));
        var palette=editor.Selected.Single().Palette;Assert.IsNotNull(palette);Assert.IsTrue(palette!.Combined);CollectionAssert.AreEquivalent(new[]{first,second},palette.SourceAssetIds.ToArray());
    }
}

[TestClass] public sealed class CanvasSourceSafetyTests
{
    [TestMethod] public async Task DocumentTargetCannotOverwriteReferencedSource()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-CanvasCollision",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var e=new CanvasEditor(new());var source=Path.Combine(root,e.Document.CanvasId.ToString("N")+".json");await File.WriteAllTextAsync(source,"source sentinel");e.Add([new(){SourcePath=source}]);
            await Assert.ThrowsAsync<InvalidDataException>(()=>new CanvasDocumentStore(root).SaveAsync(e.Document));Assert.AreEqual("source sentinel",await File.ReadAllTextAsync(source));
        }
        finally{Directory.Delete(root,true);}
    }
    [TestMethod] public async Task EveryOperationLeavesSourceHashUnchanged()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-CanvasSafety",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"source.bin");await File.WriteAllBytesAsync(source,Enumerable.Range(0,4096).Select(i=>(byte)i).ToArray());var before=SHA256.HashData(await File.ReadAllBytesAsync(source));
            var e=new CanvasEditor(new());e.Add(Enumerable.Range(0,3).Select(_=>new CanvasObject{SourcePath=source,AssetId=Guid.NewGuid(),SourceWidth=1200,SourceHeight=800}));
            e.Move(13,-60);e.Scale(1.6);e.Crop(new(.1,.1,.8,.7));e.Rotate(37);e.Flip(true);e.Flip(false);e.Group();e.Ungroup();e.Layer(true);e.Layer(false);e.SetLocked(true);e.Move(999,999);e.SetLocked(false);e.Duplicate();e.Arrange("grid");e.Arrange("horizontal");e.Arrange("compact");e.Remove();e.Undo();e.Redo();
            await new CanvasDocumentStore(Path.Combine(root,"documents")).SaveAsync(e.Document);
            CollectionAssert.AreEqual(before,SHA256.HashData(await File.ReadAllBytesAsync(source)));
        }
        finally{Directory.Delete(root,true);}
    }
}
[TestClass] public sealed class CanvasOfflinePreviewTests
{
    [TestMethod] public async Task CanvasPurposeReadsPersistentHighQualityPreviewAfterSourceGoesOffline()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-CanvasOffline",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"pixel.png");await File.WriteAllBytesAsync(source,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            var cache=Path.Combine(root,"previews");var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source)));
            var request=new AssetPreviewRequest(source,AssetPreviewPurpose.Canvas,AssetPreviewQuality.High,1024,AssetId:Guid.NewGuid(),ContentHash:hash);
            var online=await new WpfAssetThumbnailProvider(cache).GetAsync(request);Assert.IsTrue(online.IsAvailable);
            var offline=await new WpfAssetThumbnailProvider(cache).GetAsync(request with{SourcePath=Path.Combine(root,"unmounted","pixel.png"),KnownState=AssetThumbnailState.Offline});
            Assert.IsNotNull(offline.Bitmap);Assert.AreEqual(online.Bitmap!.PixelWidth,offline.Bitmap.PixelWidth);Assert.IsTrue(File.Exists(source));
        }
        finally{Directory.Delete(root,true);}
    }
}
[TestClass] public sealed class CanvasInspirationBoardLinkTests
{
    [TestMethod] public Task BoardRoundTripKeepsStableReferencesAndDeduplicatesExistingTrayEntries()=>AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-CanvasBoard",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"pixel.png");await File.WriteAllBytesAsync(source,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            var product=Path.Combine(root,"product.db");var database=new PixelTartDatabase(product);Assert.IsTrue((await new DatabaseMigrator(database,new DatabaseBackupService(database,Path.Combine(root,"backups"))).MigrateAsync()).Success);
            await using var vm=new AssetLibraryViewModel(Path.Combine(root,"assets.db"),new TaskOperationBridge(),[],productDatabasePath:product,inspirationTrayDatabasePath:Path.Combine(root,"tray.db"));
            await vm.InitializeAsync();await vm.ImportDemoDirectoryAsync(root);
            var refs=vm.AssetCards.Select(card=>vm.CanvasReference(card.Asset)).ToArray();Assert.HasCount(1,refs);
            await vm.SaveCanvasBoardAsync(refs,null,null);var board=vm.InspirationCollections.Single();
            await vm.SaveCanvasBoardAsync(refs,board.CollectionId,null);Assert.AreEqual(1,vm.InspirationCollections.Single().EntryCount);
            var back=await vm.LoadCanvasSourcesAsync("灵感板","",null);Assert.HasCount(1,back);Assert.AreEqual(refs[0].AssetId,back[0].AssetId);Assert.AreEqual(refs[0].LibraryId,back[0].LibraryId);Assert.AreEqual(refs[0].ContentHash,back[0].ContentHash);
        }
        finally{try{Directory.Delete(root,true);}catch{}}
    });
}
[TestClass] public sealed class CanvasProjectLinkTests
{
    [TestMethod] public async Task OptionalProjectAllowsMultipleCanvasesAndPlanningRemainsNullable()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-CanvasProjects",Guid.NewGuid().ToString("N"));
        try
        {
            var store=new CanvasDocumentStore(root);var project=Guid.NewGuid();var first=new CanvasEditor(new());first.SetProject(project);var second=new CanvasEditor(new());second.SetProject(project);
            await store.SaveAsync(first.Document);await store.SaveAsync(second.Document);await store.SaveAsync(new CanvasDocument());Assert.HasCount(2,await store.ListAsync(project));
            first.SetProject(null);await store.SaveAsync(first.Document);Assert.HasCount(1,await store.ListAsync(project));Assert.IsNull(first.Document.PlanningSectionId);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
