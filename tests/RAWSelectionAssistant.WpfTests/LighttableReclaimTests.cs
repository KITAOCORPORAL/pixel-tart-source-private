using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class LighttableReclaimTests
{
    private static string Root=>Environment.GetEnvironmentVariable("KITAO_LIGHTTABLE_RECLAIM_ROOT")??throw new InvalidOperationException("Explicit isolated reclaim root required");
    private static Target Field<Target>(StudioViewingTableView view,string name)=>(Target)typeof(StudioViewingTableView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(view)!;
    private static void Layout(FrameworkElement view,int width=1366){var height=width==1920?1080:768;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();}
    private static async Task<ReferenceColorWorkspaceViewModel> Workspace(string scenario)
    {
        EnsureTestApplication();var directory=Path.Combine(Root,scenario+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);var workspace=new ReferenceColorWorkspaceViewModel(new CompanyViewingMeasurementsTests.Dialogs());
        for(var index=0;index<3;index++)
        {
            var width=index==1?100:240;var height=index==1?240:160;var pixels=new byte[width*height*3];
            for(var offset=0;offset<pixels.Length;offset++)pixels[offset]=(byte)((offset+index*61)%251);
            var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,pixels,width*3);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));var path=Path.Combine(directory,index+".png");using(var file=File.Create(path))encoder.Save(file);
            await workspace.LoadTargetAsync(path);await workspace.CreateR53VariantAsync(true);workspace.ActiveTarget!.IsSelected=true;
        }
        workspace.PinSelectedViewingVariants();await workspace.PrepareViewingTableAsync();return workspace;
    }
    private static Rect Bounds(FrameworkElement element,Visual ancestor)=>element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
    [TestMethod]
    public Task SameProjectSaveRetainsSelectionButReloadWithSameIdsNeverLeaksSelection()=>RunSta(async()=>
    {
        new ViewingPreferences().Save();using var workspace=await Workspace("save");var view=new StudioViewingTableView(workspace);
        try
        {
            Layout(view);await view.RefreshAsync();var frame=workspace.ViewingFrames[0];view.SelectForTest(frame);view.SelectForTest(workspace.ViewingFrames[1],true);Layout(view);
            var selected=view.SelectedVariant;var ids=Field<HashSet<Guid>>(view,"selection").ToArray();var recipe=frame.Item.RecipeHash;var frozen=frame.Item.FrozenVariant;var path=Path.Combine(Root,"project-A.ptstudio.json");await workspace.SaveSessionAsync(path);
            await File.WriteAllTextAsync(Path.Combine(Root,"SAVE_SELECTION.json"),JsonSerializer.Serialize(new{expected=selected,actual=view.SelectedVariant,expectedSelection=ids,actualSelection=Field<HashSet<Guid>>(view,"selection").ToArray(),phase="first saved-project notification uses same backend as autosave"},new JsonSerializerOptions{WriteIndented=true}));
            Assert.AreEqual(selected,view.SelectedVariant,"Saving the current project must not clear the selected fixed version");CollectionAssert.AreEquivalent(ids,Field<HashSet<Guid>>(view,"selection").ToArray());
            workspace.BeginViewingGesture();workspace.SetViewingPlacement(frame.Item.TargetId,frame.Item.VariantId,240,170,.9,37);workspace.CommitViewingGesture();await workspace.SaveSessionCommand.ExecuteAsync(null);Assert.AreEqual(selected,view.SelectedVariant);
            workspace.UndoViewing();Assert.AreEqual(frame.Item.Rotation,workspace.ViewingTable.Items.Single(item=>item.VariantId==frame.Item.VariantId).Rotation);workspace.RedoViewing();Assert.AreEqual(37d,workspace.ViewingTable.Items.Single(item=>item.VariantId==frame.Item.VariantId).Rotation);
            Assert.AreSame(frozen,workspace.ViewingTable.Items.Single(item=>item.VariantId==frame.Item.VariantId).FrozenVariant);Assert.AreEqual(recipe,workspace.ViewingTable.Items.Single(item=>item.VariantId==frame.Item.VariantId).RecipeHash);
            var other=Path.Combine(Root,"project-B-same-ids.ptstudio.json");File.Copy(path,other);await workspace.LoadSessionAsync(other);Assert.IsNull(view.SelectedVariant,"Project replacement must clear selection even when target and version IDs collide");Assert.IsEmpty(Field<HashSet<Guid>>(view,"selection"));Assert.IsFalse(workspace.CanUndoViewing);
            await workspace.PrepareViewingTableAsync();view.SelectForTest(workspace.ViewingFrames[0]);await workspace.LoadSessionAsync(path);Assert.IsNull(view.SelectedVariant);Assert.IsEmpty(Field<HashSet<Guid>>(view,"selection"));
            await File.WriteAllTextAsync(Path.Combine(Root,"PROJECT_ISOLATION.json"),JsonSerializer.Serialize(new{sameProjectSelection=true,sameIdsDifferentProjectCleared=true,reopenCleared=true,frozenRecipeUnchanged=true,undoRedo=true}));
        }
        finally{view.Close();new ViewingPreferences().Save();}
    });
    [TestMethod]
    public Task ContextToolbarUsesTransformedPhotoAndLabelBoundsWithinViewport()=>RunSta(async()=>
    {
        new ViewingPreferences().Save();using var workspace=await Workspace("context");var view=new StudioViewingTableView(workspace);var rows=new List<object>();
        try
        {
            Layout(view);await view.RefreshAsync();workspace.SetViewingViewport(1,0,0);var frame=workspace.ViewingFrames[1];view.SelectForTest(frame);var image=frame.Image;var decodeCount=workspace.ViewingDecodeCount;
            foreach(var width in new[]{800,1366,1920})foreach(var angle in new[]{37d,90d,154d})foreach(var scale in new[]{.6,.8,1.4})
            {
                Layout(view,width);var center=Field<Grid>(view,"center");workspace.SetViewingPlacement(frame.Item.TargetId,frame.Item.VariantId,angle==154?center.ActualWidth-260:Math.Min(320,center.ActualWidth-300),center.ActualHeight-240,scale,angle);Layout(view,width);view.SelectForTest(frame);Layout(view,width);
                var card=view.CardForTest(frame.Item.VariantId)!;var content=(Grid)card.Child;var photo=content.Children.OfType<Image>().Single();var label=content.Children.OfType<TextBlock>().Single();var bounds=Bounds(photo,center);bounds.Union(Bounds(label,center));var toolbar=Field<StackPanel>(view,"contextBar");var actual=Bounds(toolbar,center);var intersects=bounds.IntersectsWith(actual);
                rows.Add(new{width,angle,scale,photoAndLabel=bounds.ToString(),toolbar=actual.ToString(),viewport=new[]{center.ActualWidth,center.ActualHeight},intersects});await File.WriteAllTextAsync(Path.Combine(Root,"CONTEXT_BOUNDS.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
                Assert.IsFalse(intersects,"Context toolbar must not overlap the actual rotated photo or label when external space is available");Assert.IsTrue(actual.Left>=0&&actual.Top>=0&&actual.Right<=center.ActualWidth+.5&&actual.Bottom<=center.ActualHeight+.5);
                Assert.AreSame(image,workspace.ViewingFrames.Single(value=>value.Item.VariantId==frame.Item.VariantId).Image);Assert.AreEqual(decodeCount,workspace.ViewingDecodeCount,"Toolbar placement must not trigger a photo decode");
                if(width>=1366&&angle==154&&scale==1.4){var preview=new RenderTargetBitmap(width,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);preview.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(preview));using var file=File.Create(Path.Combine(Root,$"OFFSCREEN-context-{width}.png"));encoder.Save(file);}
            }
        }
        finally{view.Close();new ViewingPreferences().Save();}
    });
}
