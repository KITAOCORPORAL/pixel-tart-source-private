using System.IO;
using System.Globalization;
using System.Windows;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class CanvasUsageStateTests
{
    [TestMethod]
    public Task IdentityDerivedUsageTracksMultiplicityUndoAndHiddenObjects() => RunSta(() =>
    {
        EnsureTestApplication();
        var library=Guid.NewGuid();var asset=Guid.NewGuid();
        var item=new CanvasObject { LibraryId=library,AssetId=asset,Name="usage.png",ContentHash="test" };
        var editor=new CanvasEditor(new());
        var view=new FreeCanvasView(editor,new PixelTart.Modules.AssetLibrary.WpfAssetThumbnailProvider(),new CanvasDocumentStore(Path.Combine(Path.GetTempPath(),"kitao-unused-store-"+Guid.NewGuid().ToString("N"))));
        Assert.IsFalse(view.UsedMaterialKeys.Contains((library,asset)));
        editor.Add([item]);Assert.IsTrue(view.UsedMaterialKeys.Contains((library,asset)));
        editor.Add([item with { Hidden=true,Locked=true }]);Assert.HasCount(1,view.UsedMaterialKeys);
        editor.Select(editor.Document.Objects[0].ObjectId);editor.Remove();Assert.IsTrue(view.UsedMaterialKeys.Contains((library,asset)),"Hidden/locked remaining instance still counts");
        editor.Undo();Assert.HasCount(2,editor.Document.Objects);Assert.HasCount(1,view.UsedMaterialKeys);
        editor.Redo();Assert.HasCount(1,editor.Document.Objects);Assert.HasCount(1,view.UsedMaterialKeys);
        editor.Select(editor.Document.Objects[0].ObjectId);editor.SetLocked(false);editor.Remove();Assert.HasCount(0,view.UsedMaterialKeys);
        editor.Undo();Assert.IsTrue(view.UsedMaterialKeys.Contains((library,asset)));
        editor.Redo();Assert.HasCount(0,view.UsedMaterialKeys);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task SameAssetIdInAnotherLibraryAndNonAssetObjectsDoNotCrossMark() => RunSta(() =>
    {
        EnsureTestApplication();var library=Guid.NewGuid();var other=Guid.NewGuid();var asset=Guid.NewGuid();
        var editor=new CanvasEditor(new());editor.Add([new(){LibraryId=library,AssetId=asset},new(){AssetId=asset},new(){LibraryId=other,AssetId=Guid.Empty}]);
        var view=new FreeCanvasView(editor,new PixelTart.Modules.AssetLibrary.WpfAssetThumbnailProvider(),new CanvasDocumentStore(Path.Combine(Path.GetTempPath(),"kitao-unused-store-"+Guid.NewGuid().ToString("N"))));
        Assert.HasCount(1,view.UsedMaterialKeys);
        var converter=new CanvasMaterialUsageVisibilityConverter();
        Visibility Visible(CanvasObject item)=>(Visibility)converter.Convert([item,view.UsedMaterialKeys],typeof(Visibility),null!,CultureInfo.InvariantCulture);
        Assert.AreEqual(Visibility.Visible,Visible(new(){LibraryId=library,AssetId=asset}));
        Assert.AreEqual(Visibility.Collapsed,Visible(new(){LibraryId=other,AssetId=asset}));
        Assert.AreEqual(Visibility.Collapsed,Visible(new(){LibraryId=library}));
        return Task.CompletedTask;
    });
}
