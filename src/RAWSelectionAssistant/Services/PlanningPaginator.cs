using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
/// <summary>Shared preview/print/PDF paginator. It allocates header/footer space rather than painting over body.</summary>
public sealed class PlanningPaginator : DocumentPaginator, IDocumentPaginatorSource
{
 private readonly DocumentPaginator _inner;
 private readonly PlanningHeaderFooter _extra;
 private readonly Size _size;
 public PlanningPaginator(FlowDocument document)
 {
  _extra=document.Tag as PlanningHeaderFooter??new();_extra.Validate();
  _size=new(double.IsFinite(document.PageWidth)?document.PageWidth:794,double.IsFinite(document.PageHeight)?document.PageHeight:1123);
  var padding=document.PagePadding;
  if(!string.IsNullOrEmpty(_extra.Header)||!string.IsNullOrEmpty(_extra.FirstHeader))padding.Top=Math.Max(54,padding.Top);
  if(_extra.PageNumbers||!string.IsNullOrEmpty(_extra.Footer)||!string.IsNullOrEmpty(_extra.FirstFooter))padding.Bottom=Math.Max(54,padding.Bottom);
  document.PagePadding=padding;
  foreach(var table in document.Blocks.OfType<Table>().Where(t=>(t.Tag as PlanningBlockSemantic)?.RepeatHeader==true).ToArray())
   PlanningRepeatHeaders.Prepare(document,table,_size.Height-padding.Top-padding.Bottom);
  _inner=((IDocumentPaginatorSource)document).DocumentPaginator;_inner.PageSize=_size;_inner.ComputePageCount();
 }
 public string HeaderText(int index)=>index==0&&_extra.DifferentFirstPage?_extra.FirstHeader:_extra.Header;
 public string FooterText(int index)
 {
  var text=index==0&&_extra.DifferentFirstPage?_extra.FirstFooter:_extra.Footer;
  return text+(_extra.PageNumbers&&!(index==0&&_extra.HideFirstPageNumber)?$"    第{_extra.StartNumber+index}页/共{PageCount}页":"");
 }
 public override DocumentPage GetPage(int pageNumber)
 {
  var page=_inner.GetPage(pageNumber);if(page==DocumentPage.Missing)return page;
  var visual=new DrawingVisual();using(var d=visual.RenderOpen())
  {d.DrawRectangle(Brushes.White,null,new Rect(_size));d.DrawRectangle(new VisualBrush(page.Visual),null,new Rect(_size));
   void Text(string text,double y){var formatted=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),10,Brushes.Black,1){MaxTextWidth=_size.Width-96,Trimming=TextTrimming.CharacterEllipsis};d.DrawText(formatted,new Point(48,y));}
   Text(HeaderText(pageNumber),20);Text(FooterText(pageNumber),_size.Height-35);}
  return new DocumentPage(visual,_size,new Rect(_size),page.ContentBox);
 }
 public override bool IsPageCountValid=>_inner.IsPageCountValid;
 public override int PageCount=>_inner.PageCount;
 public override Size PageSize{get=>_size;set{if(value!=_size)throw new NotSupportedException("请在页面设置中修改纸张后重新分页。");}}
 public override IDocumentPaginatorSource Source=>this;
 DocumentPaginator IDocumentPaginatorSource.DocumentPaginator=>this;
}
internal static class PlanningRepeatHeaders
{
 private static TableRow Copy(TableRow row)
 {
  var clone=new TableRow();foreach(var cell in row.Cells){var c=new TableCell{ColumnSpan=cell.ColumnSpan,RowSpan=cell.RowSpan,Padding=cell.Padding,BorderBrush=cell.BorderBrush,BorderThickness=cell.BorderThickness};
   foreach(var block in cell.Blocks)c.Blocks.Add((Block)System.Windows.Markup.XamlReader.Parse(System.Windows.Markup.XamlWriter.Save(block)));clone.Cells.Add(c);}return clone;
 }
 private static Table TableLike(Table source){var t=new Table{CellSpacing=source.CellSpacing};foreach(var col in source.Columns)t.Columns.Add(new TableColumn{Width=col.Width});t.RowGroups.Add(new TableRowGroup());return t;}
 public static void Prepare(FlowDocument document,Table table,double availableHeight)
 {
  var map=PlanningTableEditing.Map(table);if(map.Any(p=>p.Cell.RowSpan>1))throw new NotSupportedException("重复表头暂不能与纵向合并同时分页；请取消重复表头或拆分纵向合并。");
  var rows=table.RowGroups[0].Rows.ToArray();if(rows.Length<2)return;
  // Measure each candidate with the same WPF paginator; never overlay a header on an existing page.
  var chunks=new System.Collections.Generic.List<Table>();var current=TableLike(table);current.RowGroups[0].Rows.Add(Copy(rows[0]));
  foreach(var row in rows.Skip(1))
  {
   var next=Copy(row);current.RowGroups[0].Rows.Add(next);
   var probe=new FlowDocument{PageWidth=document.PageWidth,PageHeight=document.PageHeight,PagePadding=document.PagePadding,ColumnWidth=double.PositiveInfinity,FontFamily=document.FontFamily,FontSize=document.FontSize};
   var copy=(Table)System.Windows.Markup.XamlReader.Parse(System.Windows.Markup.XamlWriter.Save(current));probe.Blocks.Add(copy);var paginator=((IDocumentPaginatorSource)probe).DocumentPaginator;paginator.PageSize=new Size(document.PageWidth,document.PageHeight);paginator.ComputePageCount();
   if(paginator.PageCount>1){current.RowGroups[0].Rows.Remove(next);if(current.RowGroups[0].Rows.Count==1)throw new NotSupportedException("单行高于一页，不能安全重复表头；请缩短单元格内容。");chunks.Add(current);current=TableLike(table);current.RowGroups[0].Rows.Add(Copy(rows[0]));current.RowGroups[0].Rows.Add(next);}
  }
  chunks.Add(current);foreach(var chunk in chunks){document.Blocks.InsertBefore(table,new Paragraph(new Run("")){BreakPageBefore=true,Margin=new Thickness(0),FontSize=1,LineHeight=1});document.Blocks.InsertBefore(table,chunk);}document.Blocks.Remove(table);
 }
}
