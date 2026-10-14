using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;
public sealed partial class PlanningRichEditor
{
 internal TextPointer CaptureImageInsertion()=>_editor.CaretPosition;
 internal void InsertOwnedImages(IReadOnlyList<PlanningDocumentImage> images,TextPointer position)
 {
  if(position.DocumentStart!=_editor.Document.ContentStart.DocumentStart&&position.CompareTo(_editor.Document.ContentStart)<0)throw new InvalidOperationException("编辑器已变化，请重新插入。");
  _history.Execute(()=>{var cursor=position;foreach(var meta in images){var inline=new InlineUIContainer(PlanningDocumentImages.Create(meta),cursor);var caption=new Run(meta.Title+" — "+meta.Caption,inline.ElementEnd);cursor=caption.ElementEnd;}});_persist?.Invoke();
 }
}
