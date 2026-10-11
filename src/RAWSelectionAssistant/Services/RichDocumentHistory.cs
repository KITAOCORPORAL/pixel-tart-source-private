using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.IO;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
public sealed record RichDocumentSnapshot(string Rtf,PlanningRichLayout Layout,int Caret);
public sealed class RichDocumentHistory(RichTextBox editor,Func<PlanningRichLayout> layout,Action<PlanningRichLayout> restoreLayout)
{
    private readonly Stack<RichDocumentSnapshot> _undo=[];private readonly Stack<RichDocumentSnapshot> _redo=[];private RichDocumentSnapshot? _current;
    public bool Restoring {get;private set;}
    public RichDocumentSnapshot Capture(){using var bytes=new MemoryStream();new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Save(bytes,DataFormats.Rtf);return new(Convert.ToBase64String(bytes.ToArray()),layout(),editor.Document.ContentStart.GetOffsetToPosition(editor.CaretPosition));}
    public void Initialize()=>_current=Capture();
    public void Checkpoint(){if(Restoring)return;var next=Capture();if(_current is not null&&(_current.Rtf!=next.Rtf||JsonSerializer.Serialize(_current.Layout)!=JsonSerializer.Serialize(next.Layout))){_undo.Push(_current);Trim();_redo.Clear();} _current=next;}
    private void Trim()
    {
        long bytes=0;var kept=new List<RichDocumentSnapshot>();foreach(var item in _undo){bytes+=item.Rtf.Length*2L;if(kept.Count>=100||bytes>128L*1024*1024)break;kept.Add(item);}_undo.Clear();foreach(var item in kept.AsEnumerable().Reverse())_undo.Push(item);
    }
    public void Execute(Action action){Checkpoint();var before=_current!;Restoring=true;try{action();}catch{Restore(before);throw;}finally{Restoring=false;}Checkpoint();}
    public void Undo(){Checkpoint();if(_undo.Count==0)return;_redo.Push(_current!);Restore(_undo.Pop());}
    public void Redo(){if(_redo.Count==0)return;_undo.Push(_current!);Restore(_redo.Pop());}
    private void Restore(RichDocumentSnapshot snapshot){Restoring=true;try{editor.Document=PlanningDocumentLayout.Load(snapshot.Rtf,snapshot.Layout);restoreLayout(snapshot.Layout);editor.CaretPosition=editor.Document.ContentStart.GetPositionAtOffset(Math.Min(snapshot.Caret,editor.Document.ContentStart.GetOffsetToPosition(editor.Document.ContentEnd)))??editor.Document.ContentStart;_current=Capture();}finally{Restoring=false;}}
}
