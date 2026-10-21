using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class PlanningSummaryImageProjectionTests
{
 [TestMethod]public void OwnedCopiesKeepOrderAndIdentityAcrossReferenceMoodboardAndLighting()
 {
  var project=Guid.NewGuid();var doc=Guid.NewGuid();var library=Guid.NewGuid();var asset=Guid.NewGuid();var source=new AssetLibraryStableReference(library,asset,new string('a',64));
  PlanningDocumentImage Image(int order,string placement)=>new(Guid.NewGuid(),doc,source,"ignored",new string('b',64),placement,"图片"+order,"中文图注"+order,360,240,order){StorageRoot=Path.GetFullPath("synthetic-owned")};
  var a=Image(2,"参考图");var b=Image(0,"情绪板");a=a with{CopyPath=PlanningImageStorage.RelativePath(doc,a.DocumentImageId)};b=b with{CopyPath=PlanningImageStorage.RelativePath(doc,b.DocumentImageId)};
  var reference=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Lighting,library,asset,Title:"重复稳定引用");
  var document=new PlanningDocument{Package=new(){ProjectId=project,DocumentId=doc,RichLayout=new(new(),[],[]){Images=[a,b]}},References=[new(reference,"灯光",IsMoodboard:true)]};
  var result=PlanningReadableSummaryBuilder.Build(new(project,"test",document,new(),[]));
  foreach(var section in new[]{3,4,5}){var images=result.Sections[section].Entries.SelectMany(x=>x.OwnedImages).ToArray();CollectionAssert.AreEqual(new[]{b.DocumentImageId,a.DocumentImageId},images.Select(x=>x.DocumentImageId).ToArray());}
  Assert.AreEqual(2,document.Package.RichLayout.Images.Count);Assert.AreEqual(a.DocumentImageId,document.Package.RichLayout.Images[0].DocumentImageId,"Projection cannot reorder stored image metadata");
 }
 [TestMethod]public void UnownedExternalAndUnmatchedLibraryReferencesNeverBecomeOwnedImages()
 {
  var project=Guid.NewGuid();var doc=new PlanningDocument{References=[new(new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:Path.GetFullPath("exists-or-not.png"),Title:"外链")),new(new(Guid.NewGuid(),ShotReferenceKind.General,Guid.NewGuid(),Guid.NewGuid(),Title:"库引用"))]}.EnsureIdentity(project);
  var result=PlanningReadableSummaryBuilder.Build(new(project,"test",doc,new(),[]));Assert.IsTrue(result.Sections.SelectMany(s=>s.Entries).All(e=>e.OwnedImages.Count==0));
 }
}
