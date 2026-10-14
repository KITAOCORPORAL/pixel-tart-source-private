using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RAWSelectionAssistant.Views;
public sealed partial class PlanningRichEditor
{
 internal void InsertReferenceImage(byte[] bytes,bool documentTail=false)
 {
  using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
  var cell=documentTail?null:Services.PlanningTableEditing.CellAt(_editor.CaretPosition);
  var width=cell is null?480d:160d;var image=new Image{Source=bitmap,Width=width,Height=width*bitmap.PixelHeight/bitmap.PixelWidth,Stretch=Stretch.Uniform};
  if(cell is null){_editor.Document.Blocks.Add(new BlockUIContainer(image));return;}
  // Insert at the text caret inside this exact cell, not at document tail.
  new InlineUIContainer(image,_editor.CaretPosition);
 }
 internal void InsertReferenceImageUndoable(byte[] bytes){_history.Execute(()=>InsertReferenceImage(bytes));_persist?.Invoke();}
}
