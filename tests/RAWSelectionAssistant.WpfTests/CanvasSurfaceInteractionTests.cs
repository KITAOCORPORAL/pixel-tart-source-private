using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class CanvasSurfaceInteractionTests
{
    [TestMethod]
    public Task RealSurfaceRoutesDeleteAndRestoresSavedObjects()=>AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-CanvasSurface",Guid.NewGuid().ToString("N"));
        var editor=CanvasFixtures.Create(3);var store=new CanvasDocumentStore(root);
        var view=new FreeCanvasView(editor,new WpfAssetThumbnailProvider(),store);
        var window=new Window { Content=view,Width=1200,Height=800,ShowInTaskbar=false };
        try
        {
            window.Show();await view.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            view.Surface.Focus();Assert.IsTrue(view.Surface.IsKeyboardFocused);
            var input=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(view.Surface)!,0,Key.Delete){RoutedEvent=Keyboard.KeyDownEvent};
            view.Surface.RaiseEvent(input);Assert.IsTrue(input.Handled);Assert.HasCount(0,editor.Document.Objects);
            editor.Undo();Assert.HasCount(3,editor.Document.Objects);
            editor.SelectAll();editor.Group();editor.Rotate(27);editor.Move(-70,150);
            Assert.IsTrue(await view.FlushAsync());
            var loaded=await store.LoadAsync(editor.Document.CanvasId);Assert.HasCount(3,loaded!.Objects);
            Assert.AreEqual(editor.Document.Objects[0],loaded.Objects[0]);
            view.Surface.Fit();Assert.IsTrue(double.IsFinite(view.Surface.Zoom));
            editor.Select(editor.Document.Objects[0].ObjectId);editor.Ungroup();editor.Select(editor.Document.Objects[0].ObjectId);
            view.Surface.BeginCrop();Assert.IsTrue(view.Surface.CropMode);view.Surface.PendingCrop=new(.1,.2,.6,.5);view.Surface.FinishCrop(false);
            Assert.AreEqual(new CanvasCrop(),editor.Selected[0].CropRect);
            editor.SetLocked(true);view.Surface.BeginCrop();Assert.IsFalse(view.Surface.CropMode);
        }
        finally{await view.FlushAsync();window.Close();if(Directory.Exists(root))Directory.Delete(root,true);}
    });

    [TestMethod]
    public Task SaveFailureKeepsDocumentAvailableForRetry()=>AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-CanvasSaveFailure",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var blocker=Path.Combine(root,"blocked");await File.WriteAllTextAsync(blocker,"not a directory");
        var editor=CanvasFixtures.Create();
        var view=new FreeCanvasView(editor,new WpfAssetThumbnailProvider(),new CanvasDocumentStore(blocker));
        try{Assert.IsFalse(await view.FlushAsync());Assert.HasCount(1,editor.Document.Objects);}
        finally{Directory.Delete(root,true);}
    });
}
