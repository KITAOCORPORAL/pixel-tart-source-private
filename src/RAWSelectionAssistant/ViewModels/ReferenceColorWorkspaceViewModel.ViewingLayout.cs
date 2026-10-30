using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private readonly Stack<ViewingTableState> viewingUndo=[],viewingRedo=[];private ViewingTableState? viewingGesture;
 public bool CanUndoViewing=>viewingUndo.Count>0;public bool CanRedoViewing=>viewingRedo.Count>0;
 private static void BoundViewingHistory(Stack<ViewingTableState> history){if(history.Count<=40)return;var keep=history.Take(40).Reverse().ToArray();history.Clear();foreach(var state in keep)history.Push(state);}
 private void SetViewingState(ViewingTableState next,bool remember=true){next=next.Validate();if(JsonSerializer.Serialize(next)==JsonSerializer.Serialize(ViewingTable))return;if(remember&&viewingGesture is null){viewingUndo.Push(ViewingTable);BoundViewingHistory(viewingUndo);viewingRedo.Clear();}Layout=Layout with{ViewingTable=next};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));}
 public void BeginViewingGesture(){viewingGesture??=ViewingTable;}
 public void CommitViewingGesture(){var before=viewingGesture;viewingGesture=null;if(before is not null&&JsonSerializer.Serialize(before)!=JsonSerializer.Serialize(ViewingTable)){viewingUndo.Push(before);BoundViewingHistory(viewingUndo);viewingRedo.Clear();}}
 public void CancelViewingGesture(){var before=viewingGesture;viewingGesture=null;if(before is not null)SetViewingState(before,false);}
 internal bool ViewingGestureActive=>viewingGesture is not null;
 public void UndoViewing(){if(!viewingUndo.TryPop(out var state))return;viewingRedo.Push(ViewingTable);SetViewingState(state,false);}
 public void RedoViewing(){if(!viewingRedo.TryPop(out var state))return;viewingUndo.Push(ViewingTable);SetViewingState(state,false);}
 public void SetViewingViewport(double scale,double x,double y)=>SetViewingState(ViewingTable with{Viewport=new ViewingTableViewport(scale,x,y).Validate()});
 public void SetViewingBrightness(double brightness)=>SetViewingState(ViewingTable with{Brightness=Math.Clamp(brightness,.1,1.6)});
 public void OrderViewing(Guid variant,bool front)=>OrderViewingSelection([variant],front);
 public void OrderViewingSelection(IEnumerable<Guid> variants,bool front)
 {
  var ids=variants.ToHashSet();var ordered=ViewingTable.Items.OrderBy(i=>i.Z).ToArray();var chosen=ordered.Where(i=>ids.Contains(i.VariantId));var other=ordered.Where(i=>!ids.Contains(i.VariantId));var next=(front?other.Concat(chosen):chosen.Concat(other)).Select((i,n)=>i with{Z=n}).ToArray();SetViewingState(ViewingTable with{Items=next});
 }
 public void TileViewing(){BeginViewingGesture();try{var items=ViewingTable.Items.OrderBy(i=>i.Z).Select((i,n)=>i with{X=20+n%5*230,Y=20+n/5*170,Rotation=0,Scale=1}).ToArray();SetViewingState(ViewingTable with{Items=items,Viewport=new()});}finally{CommitViewingGesture();}}
 public bool IsViewingPinned(Guid target,Guid variant)=>ViewingTable.Items.Any(i=>i.TargetId==target&&i.VariantId==variant);
 public void PinViewingVariant(Guid targetId,Guid variantId){var target=Targets.SingleOrDefault(t=>t.Id==targetId);var version=target?.EditVersions.SingleOrDefault(v=>v.Id==variantId&&!v.Archived);if(target?.SourceHash is null||version is null||IsViewingPinned(targetId,variantId))return;if(ViewingTable.Items.Count>=100){ViewingStatus="灯箱已满，未移出已有照片";OnPropertyChanged(nameof(ViewingStatus));return;}var frozen=JsonSerializer.Deserialize<StudioEditVersion>(JsonSerializer.Serialize(version))!;var n=ViewingTable.Items.Count;var item=new ViewingTableItem(targetId,variantId,target.SourceHash,FilmBackendBindingV1.HashRecipe(frozen),frozen,20+n%5*230,20+n/5*170){Z=n};SetViewingState(ViewingTable with{Items=ViewingTable.Items.Append(item).ToArray()});}
}
