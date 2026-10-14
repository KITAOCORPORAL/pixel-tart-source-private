using System.IO;
using System.Windows;
using System.Windows.Documents;
namespace RAWSelectionAssistant.Services;
public sealed record PlanningCellPosition(TableCell Cell,int Row,int Column);
public static class PlanningTableEditing
{
 public static IReadOnlyList<PlanningCellPosition> Map(Table table)
 {
  if(table.RowGroups.Count!=1)throw new InvalidOperationException("仅支持单行组表格；请先拆分行组。");
  var result=new System.Collections.Generic.List<PlanningCellPosition>();var occupied=new HashSet<(int,int)>();var rows=table.RowGroups[0].Rows;
  for(var r=0;r<rows.Count;r++){var c=0;foreach(var cell in rows[r].Cells){while(occupied.Contains((r,c)))c++;if(r+cell.RowSpan>rows.Count)throw new InvalidOperationException("合并范围越过表格底边。");
   for(var y=r;y<r+cell.RowSpan;y++)for(var x=c;x<c+cell.ColumnSpan;x++)if(!occupied.Add((y,x)))throw new InvalidOperationException("表格合并范围重叠。");result.Add(new(cell,r,c));c+=cell.ColumnSpan;}}
  var width=result.Select(x=>x.Column+x.Cell.ColumnSpan).DefaultIfEmpty(0).Max();for(var r=0;r<rows.Count;r++)for(var c=0;c<width;c++)if(!occupied.Contains((r,c)))throw new InvalidOperationException("不规则表格存在空洞，不能合并或导出。");
  return result;
 }
 public static TableCell Merge(Table table,IReadOnlyCollection<TableCell> selection)
 {
  var map=Map(table);var chosen=map.Where(p=>selection.Contains(p.Cell)).ToArray();if(chosen.Length!=selection.Count||chosen.Length<2)throw new InvalidOperationException("请选择同一表格中至少两个完整单元格。");
  var top=chosen.Min(p=>p.Row);var left=chosen.Min(p=>p.Column);var bottom=chosen.Max(p=>p.Row+p.Cell.RowSpan);var right=chosen.Max(p=>p.Column+p.Cell.ColumnSpan);
  if(chosen.Sum(p=>p.Cell.RowSpan*p.Cell.ColumnSpan)!=(bottom-top)*(right-left))throw new InvalidOperationException("只允许完整矩形选区；不规则选区未修改。");
  var ordered=chosen.OrderBy(p=>p.Row).ThenBy(p=>p.Column).ToArray();var anchor=ordered[0].Cell;
  foreach(var p in ordered.Skip(1)){foreach(var block in p.Cell.Blocks.ToArray()){p.Cell.Blocks.Remove(block);anchor.Blocks.Add(block);}((TableRow)p.Cell.Parent).Cells.Remove(p.Cell);}
  anchor.RowSpan=bottom-top;anchor.ColumnSpan=right-left;return anchor;
 }
 public static void Split(Table table,TableCell cell)
 {
  var map=Map(table);var origin=map.Single(p=>ReferenceEquals(p.Cell,cell));var height=cell.RowSpan;var width=cell.ColumnSpan;if(height==1&&width==1)return;
  cell.RowSpan=cell.ColumnSpan=1;
  for(var r=origin.Row;r<origin.Row+height;r++)for(var c=origin.Column;c<origin.Column+width;c++)
  {if(r==origin.Row&&c==origin.Column)continue;var row=table.RowGroups[0].Rows[r];var next=map.Where(p=>p.Row==r&&p.Column>c).OrderBy(p=>p.Column).FirstOrDefault();
   var added=new TableCell(new Paragraph()){BorderBrush=cell.BorderBrush,BorderThickness=cell.BorderThickness,Padding=cell.Padding};
   if(next is null)row.Cells.Add(added);else row.Cells.Insert(row.Cells.IndexOf(next.Cell),added);}
 }
 public static Table? TableOf(TableCell? cell)=>cell?.Parent is TableRow r&&r.Parent is TableRowGroup g?g.Parent as Table:null;
 public static TableCell? CellAt(TextPointer position){DependencyObject? p=position.Parent;while(p is FrameworkContentElement e){if(p is TableCell c)return c;p=e.Parent;}return null;}
}
