using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private readonly Stack<(string Database,AssetLibraryUndoToken Token)> _annotationUndo=[];
    private readonly Stack<(string Database,AssetLibraryUndoToken Token)> _annotationRedo=[];
    public AsyncRelayCommand UndoAnnotationCommand {get;private set;}=null!;
    public AsyncRelayCommand RedoAnnotationCommand {get;private set;}=null!;
    private void InitializeAnnotationUndo()
    {
        UndoAnnotationCommand=new AsyncRelayCommand(_=>RestoreAnnotationAsync(false));RedoAnnotationCommand=new AsyncRelayCommand(_=>RestoreAnnotationAsync(true));
    }
    private async Task RestoreAnnotationAsync(bool redo)
    {
        if(IsLoading||IsExporting)return;
        await FlushMetadataAsync();var source=redo?_annotationRedo:_annotationUndo;var destination=redo?_annotationUndo:_annotationRedo;if(source.Count==0)return;var item=source.Peek();
        await using var repository=new SqliteAssetLibraryRepository(item.Database);var success=redo?await repository.RedoAsync(item.Token):await repository.UndoAsync(item.Token);
        if(success){source.Pop();destination.Push(item);}foreach(var target in Targets){target.OwnAnnotationWrites.Clear();await HydrateAssetMetadataAsync(target);}
        StatusText=success?"摄影师标注已恢复":"标注已被其他操作修改，拒绝过期撤销；已刷新权威值";
    }
}
