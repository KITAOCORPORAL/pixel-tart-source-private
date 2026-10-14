using System.Globalization;
using System.Text;
using System.Windows.Documents;
namespace RAWSelectionAssistant.Services;

public sealed record PlanningTextMatch(Paragraph Paragraph,int Offset,int Length);
/// <summary>Searches visible body text within paragraphs, across Run formatting boundaries.
/// Paragraph/cell/list boundaries are not silently flattened into matches.</summary>
public static class PlanningTextSearch
{
 public static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks)
 {
  foreach(var b in blocks)
   if(b is Paragraph p)yield return p;
   else if(b is Table t)foreach(var p2 in t.RowGroups.SelectMany(g=>g.Rows).SelectMany(r=>r.Cells).SelectMany(c=>Paragraphs(c.Blocks)))yield return p2;
   else if(b is List list)foreach(var p2 in list.ListItems.SelectMany(i=>Paragraphs(i.Blocks)))yield return p2;
   else if(b is Section section)foreach(var p2 in Paragraphs(section.Blocks))yield return p2;
 }
 private static IEnumerable<Inline> Inlines(InlineCollection items)
 {foreach(var i in items)if(i is Span s){foreach(var n in Inlines(s.Inlines))yield return n;}else yield return i;}
 private static string Text(Paragraph p)=>string.Concat(Inlines(p.Inlines).Select(i=>i is Run r?r.Text:i is LineBreak?"\n":"\uFFFC"));
 private static bool Word(char c)=>char.IsLetterOrDigit(c)||c=='_'||CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
 public static IReadOnlyList<PlanningTextMatch> Find(FlowDocument document,string query,bool matchCase=false,bool wholeWord=false)
 {
  if(string.IsNullOrEmpty(query)||query.Contains('\r')||query.Contains('\n'))return [];
  var found=new System.Collections.Generic.List<PlanningTextMatch>();var comparison=matchCase?StringComparison.Ordinal:StringComparison.OrdinalIgnoreCase;
  foreach(var p in Paragraphs(document.Blocks))
  {var text=Text(p);for(var start=0;start<=text.Length-query.Length;){var index=text.IndexOf(query,start,comparison);if(index<0)break;
    if(!wholeWord||((index==0||!Word(text[index-1]))&&(index+query.Length==text.Length||!Word(text[index+query.Length]))))found.Add(new(p,index,query.Length));start=index+query.Length;}}
  return found;
 }
 public static (TextPointer Start,TextPointer End) Range(PlanningTextMatch match)
 {
  TextPointer? start=null,end=null;var offset=0;
  foreach(var inline in Inlines(match.Paragraph.Inlines))
  {var count=inline is Run r?r.Text.Length:1;
   if(inline is Run run){if(start is null&&match.Offset>=offset&&match.Offset<offset+count)start=run.ContentStart.GetPositionAtOffset(match.Offset-offset);
    if(match.Offset+match.Length>offset&&match.Offset+match.Length<=offset+count)end=run.ContentStart.GetPositionAtOffset(match.Offset+match.Length-offset);}
   offset+=count;}
  return(start??throw new InvalidOperationException("匹配已失效，请重新查找。"),end??throw new InvalidOperationException("匹配包含非文本对象。"));
 }
 public static void Replace(PlanningTextMatch match,string replacement)
 {
  var offset=0;var changes=new System.Collections.Generic.List<(Run Run,int Start,int Count)>();
  foreach(var inline in Inlines(match.Paragraph.Inlines))
  {var length=inline is Run r?r.Text.Length:1;var start=Math.Max(match.Offset,offset);var end=Math.Min(match.Offset+match.Length,offset+length);
   if(end>start){if(inline is not Run run)throw new InvalidOperationException("不能替换图片或换行对象。请缩小查找范围。");changes.Add((run,start-offset,end-start));}offset+=length;}
  for(var i=changes.Count-1;i>=0;i--){var c=changes[i];c.Run.Text=c.Run.Text.Remove(c.Start,c.Count).Insert(c.Start,i==0?replacement:"");}
 }
}
