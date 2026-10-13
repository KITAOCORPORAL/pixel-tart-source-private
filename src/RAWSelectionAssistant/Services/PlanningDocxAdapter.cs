using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Xml.Linq;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using DocumentFormat.OpenXml.Packaging;
using W=DocumentFormat.OpenXml.Wordprocessing;
namespace RAWSelectionAssistant.Services;
public static class PlanningDocxAdapter
{
    public sealed record ImportResult(FlowDocument Document,IReadOnlyList<string> Losses);
    public static void Export(FlowDocument document,string path)
    {
        var unsupported=document.Blocks.Any(b=>b is not Paragraph && b is not Table && b is not BlockUIContainer && b is not List);
        if(unsupported)throw new NotSupportedException("DOCX 导出遇到未支持的分节或对象；请使用打印 PDF，未写入输出文件。");
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var package=WordprocessingDocument.Create(temporary,DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var main=package.AddMainDocumentPart();var body=new W.Body();main.Document=new W.Document(body);
                W.Paragraph Paragraph(Paragraph source)
                {
                    var p=new W.Paragraph(new W.ParagraphProperties(new W.Justification{Val=source.TextAlignment switch{TextAlignment.Center=>W.JustificationValues.Center,TextAlignment.Right=>W.JustificationValues.Right,_=>W.JustificationValues.Left}}));
                    if(source.BreakPageBefore)p.ParagraphProperties!.PrependChild(new W.PageBreakBefore());
                    if(source.Tag is RAWSelectionAssistant.Core.Services.Projects.PlanningBlockSemantic semantic && semantic.Style.StartsWith("Heading"))p.ParagraphProperties!.PrependChild(new W.ParagraphStyleId{Val=semantic.Style});
                    IEnumerable<Inline> Flatten(InlineCollection items){foreach(var inline in items){if(inline is Span span){foreach(var nested in Flatten(span.Inlines))yield return nested;}else yield return inline;}}
                    foreach(var inline in Flatten(source.Inlines)){if(inline is LineBreak){p.Append(new W.Run(new W.Break()));continue;}if(inline is not Run run)throw new NotSupportedException("DOCX 导出遇到未支持的内联对象。");var props=new W.RunProperties();if(run.FontWeight==FontWeights.Bold)props.Append(new W.Bold());if(run.FontStyle==FontStyles.Italic)props.Append(new W.Italic());if(run.Foreground is SolidColorBrush brush)props.Append(new W.Color{Val=brush.Color.ToString()[3..]});props.PrependChild(new W.RunFonts{Ascii=run.FontFamily.Source,HighAnsi=run.FontFamily.Source,EastAsia=run.FontFamily.Source});props.Append(new W.FontSize{Val=(run.FontSize*1.5).ToString("0",System.Globalization.CultureInfo.InvariantCulture)});if(run.TextDecorations.Contains(TextDecorations.Underline[0]))props.Append(new W.Underline{Val=W.UnderlineValues.Single});p.Append(new W.Run(props,new W.Text(run.Text){Space=DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve}));}return p;
                }
                uint imageId=0;
                W.Paragraph ImageParagraph(BlockUIContainer container)
                {
                    if(container.Child is not Image image || image.Source is not BitmapSource bitmap)throw new NotSupportedException("DOCX 暂不支持此嵌入对象。");
                    var part=main.AddImagePart(ImagePartType.Png);using(var bytes=new MemoryStream()){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(bytes);bytes.Position=0;part.FeedData(bytes);}
                    var relationship=main.GetIdOfPart(part);var width=(long)((double.IsFinite(image.Width)?image.Width:480)*9525);var height=(long)(width*(double)bitmap.PixelHeight/bitmap.PixelWidth);var drawing=new W.Drawing();
                    drawing.InnerXml=$"<wp:inline xmlns:wp='http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing' xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main' xmlns:pic='http://schemas.openxmlformats.org/drawingml/2006/picture' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><wp:extent cx='{width}' cy='{height}'/><wp:docPr id='{++imageId}' name='Picture {imageId}'/><a:graphic><a:graphicData uri='http://schemas.openxmlformats.org/drawingml/2006/picture'><pic:pic><pic:nvPicPr><pic:cNvPr id='0' name='Image'/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed='{relationship}'/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x='0' y='0'/><a:ext cx='{width}' cy='{height}'/></a:xfrm><a:prstGeom prst='rect'><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline>";
                    return new W.Paragraph(new W.Run(drawing));
                }
                foreach(var block in document.Blocks)
                    if(block is Paragraph p)body.Append(Paragraph(p));
                    else if(block is BlockUIContainer image)body.Append(ImageParagraph(image));
                    else if(block is List list)
                    {
                        var numbering=main.NumberingDefinitionsPart??main.AddNewPart<NumberingDefinitionsPart>();numbering.Numbering??=new W.Numbering();
                        var numberId=numbering.Numbering.Elements<W.NumberingInstance>().Count()+1;
                        var definition=new W.AbstractNum(){AbstractNumberId=numberId};
                        void WriteList(List source,int level)
                        {
                            if(level>8)throw new NotSupportedException("DOCX列表最多9级。");
                            if(!definition.Elements<W.Level>().Any(l=>l.LevelIndex?.Value==level))definition.Append(new W.Level(new W.StartNumberingValue{Val=source.StartIndex},new W.NumberingFormat{Val=source.MarkerStyle==TextMarkerStyle.Decimal?W.NumberFormatValues.Decimal:W.NumberFormatValues.Bullet},new W.LevelText{Val=source.MarkerStyle==TextMarkerStyle.Decimal?"%"+(level+1)+".":"•"}){LevelIndex=level});
                            foreach(var item in source.ListItems)
                            {
                                bool first=true;
                                foreach(var child in item.Blocks)
                                {
                                    if(child is List nested){WriteList(nested,level+1);continue;}
                                    if(child is not Paragraph paragraph || !first)throw new NotSupportedException("本次DOCX范围为每列表项一个段落及其嵌套列表；其他内容拒绝导出，未写入目标。");
                                    var exported=Paragraph(paragraph);exported.ParagraphProperties!.PrependChild(new W.NumberingProperties(new W.NumberingLevelReference{Val=level},new W.NumberingId{Val=numberId}));body.Append(exported);first=false;
                                }
                            }
                        }
                        WriteList(list,0);numbering.Numbering.Append(definition);numbering.Numbering.Append(new W.NumberingInstance(new W.AbstractNumId{Val=numberId}){NumberID=numberId});numbering.Numbering.Save();
                    }
                    else if(block is Table table){var t=new W.Table(new W.TableProperties(),new W.TableGrid(Enumerable.Range(0,Math.Max(1,table.RowGroups.SelectMany(g=>g.Rows).Select(r=>r.Cells.Count).DefaultIfEmpty(1).Max())).Select(i=>new W.GridColumn{Width=(i<table.Columns.Count&&table.Columns[i].Width.IsAbsolute?(int)(table.Columns[i].Width.Value*15):2000).ToString()})));foreach(var group in table.RowGroups)foreach(var row in group.Rows){var r=new W.TableRow();foreach(var cell in row.Cells){if(cell.ColumnSpan!=1||cell.RowSpan!=1)throw new NotSupportedException("合并单元格尚未支持，未写入输出文件。");var c=new W.TableCell();var border=cell.BorderThickness.Left>0?W.BorderValues.Single:W.BorderValues.Nil;c.Append(new W.TableCellProperties(new W.TableCellBorders(new W.TopBorder{Val=border,Size=8},new W.LeftBorder{Val=border,Size=8},new W.BottomBorder{Val=border,Size=8},new W.RightBorder{Val=border,Size=8})));foreach(var child in cell.Blocks){if(child is not Paragraph cp)throw new NotSupportedException("嵌套表格尚未支持。");c.Append(Paragraph(cp));}r.Append(c);}t.Append(r);}body.Append(t);}
                body.Append(new W.SectionProperties(new W.PageSize{Width=(uint)(double.IsFinite(document.PageWidth)?document.PageWidth*15:11906),Height=(uint)(double.IsFinite(document.PageHeight)?document.PageHeight*15:16838)},new W.PageMargin{Left=(uint)(document.PagePadding.Left*15),Right=(uint)(document.PagePadding.Right*15),Top=(int)(document.PagePadding.Top*15),Bottom=(int)(document.PagePadding.Bottom*15),Header=0,Footer=0,Gutter=0}));main.Document.Save();
            }
            File.Move(temporary,path,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static ImportResult Import(string path)
    {
        using var package=WordprocessingDocument.Open(path,false);var body=package.MainDocumentPart?.Document?.Body??throw new InvalidDataException("DOCX 正文为空。");
        var losses=new List<string>();
        if(body.Descendants<W.TableCell>().SelectMany(c=>c.Elements<W.Paragraph>()).Any(p=>p.ParagraphProperties?.NumberingProperties is not null))losses.Add("表格单元格内列表降为段落，编号结构未保留。");
        if(body.Descendants<W.Hyperlink>().Any())losses.Add("超链接内容不在本次支持范围；请在原文档保留原件。");
        if(body.Descendants<W.TableCell>().Any(c=>c.Descendants<W.Drawing>().Any()||c.Descendants<W.Table>().Any()))losses.Add("表格内图片或嵌套表格未导入。");
        if(body.Elements<W.Paragraph>().Any(p=>p.Descendants<W.Drawing>().Any()&&p.Elements<W.Run>().Any(r=>r.Elements<W.Text>().Any())))losses.Add("同段图文被拆分为图片块和文字段落。");
        var supported=new HashSet<string>{"body","pStyle","pageBreakBefore","pgMar","tcBorders","top","left","bottom","right","br","p","pPr","jc","r","rPr","rFonts","b","i","sz","t","tbl","tr","tc","sectPr","pgSz","tblPr","tblGrid","gridCol","tcPr","tblW","u","color","numPr","ilvl","numId","drawing","inline","extent","docPr","graphic","graphicData","pic","nvPicPr","cNvPr","cNvPicPr","blipFill","blip","stretch","fillRect","spPr","xfrm","off","ext","prstGeom","avLst"};
        foreach(var name in body.Descendants().Select(x=>x.LocalName).Distinct().Where(x=>!supported.Contains(x)))losses.Add("未支持内容/属性："+name);
        if(package.MainDocumentPart!.HeaderParts.Any()||package.MainDocumentPart.FooterParts.Any())losses.Add("页眉/页脚未导入。");
        if(body.Descendants<W.SectionProperties>().Count()>1)losses.Add("多分节布局转换为单节 A4。");
        if(package.MainDocumentPart.StyleDefinitionsPart is not null)losses.Add("样式表继承尚未完整支持，仅导入直接字符格式。");
        if(body.Descendants<W.TableProperties>().Any(x=>x.HasChildren)||body.Descendants<W.TableCellProperties>().Any(x=>x.ChildElements.Any(e=>e is not W.TableCellBorders)))losses.Add("表格样式、宽度和单元格属性不保证保真；仅保留基础行列内容。");
        if(body.Descendants<W.PageSize>().Any(x=>x.Width?.Value!=11906U||x.Height?.Value!=16838U))losses.Add("页面设置将转换为 A4。");
        var document=new FlowDocument();var section=body.Elements<W.SectionProperties>().LastOrDefault();if(section?.GetFirstChild<W.PageSize>() is {} size){document.PageWidth=(size.Width?.Value??11906)/15d;document.PageHeight=(size.Height?.Value??16838)/15d;}if(section?.GetFirstChild<W.PageMargin>() is {} margin)document.PagePadding=new Thickness((margin.Left?.Value??720)/15d,(margin.Top?.Value??720)/15d,(margin.Right?.Value??720)/15d,(margin.Bottom?.Value??720)/15d);
        Paragraph Paragraph(W.Paragraph source){var p=new Paragraph();var style=source.ParagraphProperties?.ParagraphStyleId?.Val?.Value;if(style is "Heading1" or "Heading2" or "Heading3")p.Tag=new RAWSelectionAssistant.Core.Services.Projects.PlanningBlockSemantic(0,style);p.BreakPageBefore=source.ParagraphProperties?.PageBreakBefore is not null;foreach(var r in source.Elements<W.Run>()){var run=new Run(string.Concat(r.ChildElements.Select(t=>t is W.Text text?text.Text:t is W.Break?"\n":"")));if(r.RunProperties?.RunFonts?.Ascii?.Value is {} font)run.FontFamily=new FontFamily(font);if(r.RunProperties?.Bold is not null)run.FontWeight=FontWeights.Bold;if(r.RunProperties?.Italic is not null)run.FontStyle=FontStyles.Italic;if(r.RunProperties?.Underline is not null)run.TextDecorations=TextDecorations.Underline;if(r.RunProperties?.Color?.Val?.Value is {} color && color!="auto")run.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+color));if(double.TryParse(r.RunProperties?.FontSize?.Val?.Value,out var halfPoints))run.FontSize=halfPoints/1.5;p.Inlines.Add(run);}var align=source.ParagraphProperties?.Justification?.Val?.Value;p.TextAlignment=align==W.JustificationValues.Center?TextAlignment.Center:align==W.JustificationValues.Right?TextAlignment.Right:TextAlignment.Left;return p;}
        var listStack=new System.Collections.Generic.List<List>();int? currentNumber=null;
        void AddNumbered(W.Paragraph source,Paragraph paragraph)
        {
            var props=source.ParagraphProperties!.NumberingProperties!;var id=props.NumberingId?.Val?.Value??0;var level=props.NumberingLevelReference?.Val?.Value??0;
            if(level is <0 or >8){losses.Add("列表层级超过支持范围，保留为普通段落。");document.Blocks.Add(paragraph);return;}
            if(currentNumber!=id){listStack.Clear();currentNumber=id;}
            var numbering=package.MainDocumentPart!.NumberingDefinitionsPart?.Numbering;
            var instance=numbering?.Elements<W.NumberingInstance>().FirstOrDefault(n=>n.NumberID?.Value==id);
            var definition=numbering?.Elements<W.AbstractNum>().FirstOrDefault(n=>n.AbstractNumberId?.Value==instance?.AbstractNumId?.Val?.Value);
            while(listStack.Count>level+1)listStack.RemoveAt(listStack.Count-1);
            while(listStack.Count<=level)
            {
                var spec=definition?.Elements<W.Level>().FirstOrDefault(l=>l.LevelIndex?.Value==listStack.Count);
                var format=spec?.NumberingFormat?.Val?.Value;
                if(format!=W.NumberFormatValues.Decimal&&format!=W.NumberFormatValues.Bullet)losses.Add("未支持的编号样式转为项目符号。");
                var list=new List{MarkerStyle=format==W.NumberFormatValues.Decimal?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,StartIndex=Math.Max(1,spec?.StartNumberingValue?.Val?.Value??1)};
                if(listStack.Count==0)document.Blocks.Add(list);
                else {var parent=listStack[^1];if(parent.ListItems.Count==0){parent.ListItems.Add(new ListItem(new Paragraph()));losses.Add("缺失父列表项，已补空父项。");}parent.ListItems.LastListItem.Blocks.Add(list);}
                listStack.Add(list);
            }
            listStack[level].ListItems.Add(new ListItem(paragraph));
        }
        foreach(var child in body.ChildElements)
            if(child is W.Paragraph p)
            {
                var drawings=p.Descendants<W.Drawing>().ToArray();
                foreach(var drawing in drawings)
                {
                    var xml=XElement.Parse(drawing.OuterXml);var blip=xml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="blip");var id=blip?.Attributes().FirstOrDefault(x=>x.Name.LocalName=="embed")?.Value;
                    if(id is null){losses.Add("外部链接图片未导入。");continue;}
                    var imagePart=package.MainDocumentPart.GetPartById(id);using var raw=imagePart.GetStream();using var bytes=new MemoryStream();raw.CopyTo(bytes);bytes.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=bytes;bitmap.EndInit();if(bitmap.CanFreeze)bitmap.Freeze();var extent=xml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="extent");var imageWidth=double.TryParse(extent?.Attribute("cx")?.Value,out var cx)?cx/9525:480;document.Blocks.Add(new BlockUIContainer(new Image{Source=bitmap,Width=imageWidth,Stretch=Stretch.Uniform}));
                }
                if(drawings.Length==0 || p.Elements<W.Run>().Any(r=>r.Elements<W.Text>().Any()))
                {
                    var paragraph=Paragraph(p);
                    if(p.ParagraphProperties?.NumberingProperties is not null)AddNumbered(p,paragraph);
                    else {listStack.Clear();currentNumber=null;document.Blocks.Add(paragraph);}
                }
            }
            else if(child is W.Table t){listStack.Clear();currentNumber=null;var table=new Table();foreach(var col in t.GetFirstChild<W.TableGrid>()?.Elements<W.GridColumn>()??[]){if(double.TryParse(col.Width?.Value,out var width))table.Columns.Add(new TableColumn{Width=new GridLength(width/15)});}var group=new TableRowGroup();table.RowGroups.Add(group);foreach(var row in t.Elements<W.TableRow>()){var r=new TableRow();group.Rows.Add(r);foreach(var cell in row.Elements<W.TableCell>()){var c=new TableCell();if(cell.TableCellProperties?.GetFirstChild<W.TableCellBorders>()?.TopBorder?.Val?.Value==W.BorderValues.Single){c.BorderBrush=Brushes.Gray;c.BorderThickness=new Thickness(1);}r.Cells.Add(c);foreach(var cellParagraph in cell.Elements<W.Paragraph>())c.Blocks.Add(Paragraph(cellParagraph));}}document.Blocks.Add(table);}
        return new(document,losses);
    }
}
