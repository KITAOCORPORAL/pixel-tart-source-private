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
public static partial class PlanningDocxAdapter
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
                uint imageId=0;
                W.Paragraph Paragraph(Paragraph source)
                {
                    var p=new W.Paragraph(new W.ParagraphProperties(new W.Justification{Val=source.TextAlignment switch{TextAlignment.Center=>W.JustificationValues.Center,TextAlignment.Right=>W.JustificationValues.Right,_=>W.JustificationValues.Left}}));
                    if(source.BreakPageBefore)p.ParagraphProperties!.PrependChild(new W.PageBreakBefore());
                    if(source.Tag is RAWSelectionAssistant.Core.Services.Projects.PlanningBlockSemantic semantic && semantic.Style.StartsWith("Heading"))p.ParagraphProperties!.PrependChild(new W.ParagraphStyleId{Val=semantic.Style});
                    IEnumerable<Inline> Flatten(InlineCollection items){foreach(var inline in items){if(inline is Span span){foreach(var nested in Flatten(span.Inlines))yield return nested;}else yield return inline;}}
                    foreach(var inline in Flatten(source.Inlines)){if(inline is LineBreak){p.Append(new W.Run(new W.Break()));continue;}if(inline is InlineUIContainer {Child:Image image}){p.Append(ImageRun(image));continue;}if(inline is not Run run)throw new NotSupportedException("DOCX 导出遇到未支持的内联对象。");var props=new W.RunProperties();if(run.FontWeight==FontWeights.Bold)props.Append(new W.Bold());if(run.FontStyle==FontStyles.Italic)props.Append(new W.Italic());if(run.Foreground is SolidColorBrush brush)props.Append(new W.Color{Val=brush.Color.ToString()[3..]});props.PrependChild(new W.RunFonts{Ascii=run.FontFamily.Source,HighAnsi=run.FontFamily.Source,EastAsia=run.FontFamily.Source});props.Append(new W.FontSize{Val=(run.FontSize*1.5).ToString("0",System.Globalization.CultureInfo.InvariantCulture)});if(run.TextDecorations.Contains(TextDecorations.Underline[0]))props.Append(new W.Underline{Val=W.UnderlineValues.Single});var outputRun=new W.Run(props);var lines=run.Text.Replace("\r\n","\n").Split('\n');for(var line=0;line<lines.Length;line++){if(line>0)outputRun.Append(new W.Break());outputRun.Append(new W.Text(lines[line]){Space=DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve});}p.Append(outputRun);}return p;
                }
                W.Run ImageRun(Image image)
                {
                    if(image.Source is not BitmapSource bitmap)throw new NotSupportedException("DOCX 暂不支持此嵌入对象。");
                    var part=main.AddImagePart(ImagePartType.Png);using(var bytes=new MemoryStream()){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(bytes);bytes.Position=0;part.FeedData(bytes);}
                    var relationship=main.GetIdOfPart(part);var width=(long)((double.IsFinite(image.Width)?image.Width:480)*9525);var height=(long)((double.IsFinite(image.Height)?image.Height:width/9525d*bitmap.PixelHeight/bitmap.PixelWidth)*9525);var drawing=new W.Drawing();
                    drawing.InnerXml=$"<wp:inline xmlns:wp='http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing' xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main' xmlns:pic='http://schemas.openxmlformats.org/drawingml/2006/picture' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><wp:extent cx='{width}' cy='{height}'/><wp:docPr id='{++imageId}' name='Picture {imageId}'/><a:graphic><a:graphicData uri='http://schemas.openxmlformats.org/drawingml/2006/picture'><pic:pic><pic:nvPicPr><pic:cNvPr id='0' name='Image'/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed='{relationship}'/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x='0' y='0'/><a:ext cx='{width}' cy='{height}'/></a:xfrm><a:prstGeom prst='rect'><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline>";
                    if(image.Tag is RAWSelectionAssistant.Core.Services.Projects.PlanningDocumentImage meta)
                    {
                        var xml=XElement.Parse(drawing.OuterXml);var properties=xml.Descendants().First(x=>x.Name.LocalName=="docPr");
                        properties.SetAttributeValue("descr","KitaoImageV1:"+System.Text.Json.JsonSerializer.Serialize(new{meta.DocumentImageId,meta.DocumentId,meta.Title,meta.Caption,meta.Placement,meta.ShotId}));drawing.InnerXml=string.Concat(xml.Nodes().Select(n=>n.ToString(SaveOptions.DisableFormatting)));
                    }
                    return new W.Run(drawing);
                }
                void ExportList(List list,DocumentFormat.OpenXml.OpenXmlCompositeElement target)
                {
                        var numbering=main.NumberingDefinitionsPart??main.AddNewPart<NumberingDefinitionsPart>();numbering.Numbering??=new W.Numbering();
                        var numberId=numbering.Numbering.Elements<W.NumberingInstance>().Count()+1;
                        var definition=new W.AbstractNum(){AbstractNumberId=numberId};
                        void WriteList(List source,int level)
                        {
                            if(source.MarkerStyle is not(TextMarkerStyle.Decimal or TextMarkerStyle.Disc))throw new NotSupportedException("仅支持项目符号和十进制编号列表；不转换未知编号样式。");
                            if(level>8)throw new NotSupportedException("DOCX列表最多9级。");
                            if(!definition.Elements<W.Level>().Any(l=>l.LevelIndex?.Value==level))definition.Append(new W.Level(new W.StartNumberingValue{Val=source.StartIndex},new W.NumberingFormat{Val=source.MarkerStyle==TextMarkerStyle.Decimal?W.NumberFormatValues.Decimal:W.NumberFormatValues.Bullet},new W.LevelText{Val=source.MarkerStyle==TextMarkerStyle.Decimal?"%"+(level+1)+".":"•"}){LevelIndex=level});
                            foreach(var item in source.ListItems)
                            {
                                bool first=true;
                                foreach(var child in item.Blocks)
                                {
                                    if(child is List nested){WriteList(nested,level+1);continue;}
                                    if(child is not Paragraph paragraph || !first)throw new NotSupportedException("本次DOCX范围为每列表项一个段落及其嵌套列表；其他内容拒绝导出，未写入目标。");
                                    var exported=Paragraph(paragraph);exported.ParagraphProperties!.PrependChild(new W.NumberingProperties(new W.NumberingLevelReference{Val=level},new W.NumberingId{Val=numberId}));target.Append(exported);first=false;
                                }
                            }
                        }
                        WriteList(list,0);var firstInstance=numbering.Numbering.Elements<W.NumberingInstance>().FirstOrDefault();if(firstInstance is null)numbering.Numbering.Append(definition);else numbering.Numbering.InsertBefore(definition,firstInstance);numbering.Numbering.Append(new W.NumberingInstance(new W.AbstractNumId{Val=numberId}){NumberID=numberId});numbering.Numbering.Save();
                }
                void CellBlocks(BlockCollection blocks,W.TableCell target)
                {
                    foreach(var child in blocks)
                        if(child is Paragraph cp)target.Append(Paragraph(cp));
                        else if(child is BlockUIContainer {Child:Image image})target.Append(new W.Paragraph(ImageRun(image)));
                        else if(child is List list)ExportList(list,target);
                        else throw new NotSupportedException("单元格内嵌套表格或未知块尚未支持；原件保留，未写入目标。");
                }
                foreach(var block in document.Blocks)
                    if(block is Paragraph p)body.Append(Paragraph(p));
                    else if(block is BlockUIContainer image)body.Append(new W.Paragraph(ImageRun(image.Child as Image ?? throw new NotSupportedException("DOCX 暂不支持此嵌入对象。"))));
                    else if(block is List list)ExportList(list,body);
                    else if(block is Table table)
                    {
                        var map=PlanningTableEditing.Map(table);var cols=map.Max(p=>p.Column+p.Cell.ColumnSpan);
                        var t=new W.Table(new W.TableProperties(),new W.TableGrid(Enumerable.Range(0,cols).Select(i=>new W.GridColumn{Width=(i<table.Columns.Count&&table.Columns[i].Width.IsAbsolute?(int)(table.Columns[i].Width.Value*15):2000).ToString()})));
                        for(var y=0;y<table.RowGroups[0].Rows.Count;y++)
                        {
                            var row=new W.TableRow();if(y==0&&(table.Tag as RAWSelectionAssistant.Core.Services.Projects.PlanningBlockSemantic)?.RepeatHeader==true)row.Append(new W.TableRowProperties(new W.TableHeader()));
                            foreach(var pos in map.Where(pos=>pos.Row<=y&&pos.Row+pos.Cell.RowSpan>y).OrderBy(pos=>pos.Column))
                            {
                                var props=new W.TableCellProperties();if(pos.Cell.ColumnSpan>1)props.Append(new W.GridSpan{Val=pos.Cell.ColumnSpan});
                                if(pos.Cell.RowSpan>1)props.Append(new W.VerticalMerge{Val=pos.Row==y?W.MergedCellValues.Restart:W.MergedCellValues.Continue});
                                var border=pos.Cell.BorderThickness.Left>0?W.BorderValues.Single:W.BorderValues.Nil;
                                props.Append(new W.TableCellBorders(new W.TopBorder{Val=border,Size=8},new W.LeftBorder{Val=border,Size=8},new W.BottomBorder{Val=border,Size=8},new W.RightBorder{Val=border,Size=8}));
                                var c=new W.TableCell(props);
                                if(pos.Row==y)CellBlocks(pos.Cell.Blocks,c);
                                if(!c.Elements<W.Paragraph>().Any())c.Append(new W.Paragraph());row.Append(c);
                            }
                            t.Append(row);
                        }
                        body.Append(t);
                    }
                body.Append(new W.SectionProperties(new W.PageSize{Width=(uint)(double.IsFinite(document.PageWidth)?document.PageWidth*15:11906),Height=(uint)(double.IsFinite(document.PageHeight)?document.PageHeight*15:16838),Orient=document.PageWidth>document.PageHeight?W.PageOrientationValues.Landscape:W.PageOrientationValues.Portrait},new W.PageMargin{Left=(uint)(document.PagePadding.Left*15),Right=(uint)(document.PagePadding.Right*15),Top=(int)(document.PagePadding.Top*15),Bottom=(int)(document.PagePadding.Bottom*15),Header=0,Footer=0,Gutter=0}));WritePageExtras(main,body.Elements<W.SectionProperties>().Last(),document.Tag as RAWSelectionAssistant.Core.Services.Projects.PlanningHeaderFooter??new());main.Document.Save();
            }
            // Replacing an existing document is not a rename-overwrite. On Windows,
            // MoveFileEx refuses a destination with a delete-sharing reader; ReplaceFile
            // supports that reader and preserves the destination security descriptor.
            if(File.Exists(path))File.Replace(temporary,path,null);
            else File.Move(temporary,path);
        }
        catch(UnauthorizedAccessException error){throw new UnauthorizedAccessException("DOCX 导出未完成：目标只读或文件/目录权限拒绝替换；原目标未被本次输出覆盖。路径："+Path.GetFullPath(path),error);}
        catch(IOException error){throw new IOException("DOCX 导出未完成：文件占用或磁盘写入失败；原目标未被本次输出覆盖。路径："+Path.GetFullPath(path),error);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static ImportResult Import(string path)
    {
        using var package=WordprocessingDocument.Open(path,false);var body=package.MainDocumentPart?.Document?.Body??throw new InvalidDataException("DOCX 正文为空。");
        var losses=new List<string>();
        if(body.Descendants<W.TableCell>().Any(c=>c.ChildElements.Any(x=>x is not W.TableCellProperties && x is not W.Paragraph)||c.Descendants<W.Hyperlink>().Any()||c.Descendants<W.SdtRun>().Any()))
            throw new NotSupportedException("单元格内嵌套表格、内容控件或超链接当前不能安全导入；原件和策划均未修改。");
        // Cross-cell continuation and per-instance level overrides need an explicit numbering
        // model. Reject them before import rather than silently restarting a shared sequence.
        var cellNumbers=body.Descendants<W.TableCell>().SelectMany((cell,index)=>cell.Descendants<W.NumberingId>().Select(n=>new{Cell=index,Id=n.Val?.Value??0})).ToArray();
        if(cellNumbers.GroupBy(n=>n.Id).Any(g=>g.Select(n=>n.Cell).Distinct().Count()>1))throw new NotSupportedException("跨单元格共享续号尚未支持；请在原件将各单元格列表设为独立编号后导入。");
        var definitions=package.MainDocumentPart!.NumberingDefinitionsPart?.Numbering;
        if(cellNumbers.Any(n=>definitions?.Elements<W.NumberingInstance>().FirstOrDefault(i=>i.NumberID?.Value==n.Id)?.Elements<W.LevelOverride>().Any()==true))throw new NotSupportedException("表内列表的实例级编号覆盖尚未支持，未修改策划。");
        if(body.Descendants<W.Hyperlink>().Any())losses.Add("超链接内容不在本次支持范围；请在原文档保留原件。");

        var supported=new HashSet<string>{"headerReference","footerReference","titlePg","pgNumType","gridSpan","vMerge","trPr","tblHeader","body","pStyle","pageBreakBefore","pgMar","tcBorders","top","left","bottom","right","br","p","pPr","jc","r","rPr","rFonts","b","i","sz","t","tbl","tr","tc","sectPr","pgSz","tblPr","tblGrid","gridCol","tcPr","tblW","u","color","numPr","ilvl","numId","drawing","inline","extent","docPr","graphic","graphicData","pic","nvPicPr","cNvPr","cNvPicPr","blipFill","blip","stretch","fillRect","spPr","xfrm","off","ext","prstGeom","avLst"};
        foreach(var name in body.Descendants().Select(x=>x.LocalName).Distinct().Where(x=>!supported.Contains(x)))losses.Add("未支持内容/属性："+name);

        if(body.Descendants<W.SectionProperties>().Count()>1)losses.Add("多分节布局转换为单节 A4。");
        if(package.MainDocumentPart.StyleDefinitionsPart is not null)losses.Add("样式表继承尚未完整支持，仅导入直接字符格式。");
        if(body.Descendants<W.TableProperties>().Any(x=>x.HasChildren)||body.Descendants<W.TableCellProperties>().Any(x=>x.ChildElements.Any(e=>e is not W.TableCellBorders && e is not W.GridSpan && e is not W.VerticalMerge)))losses.Add("表格样式、宽度和单元格属性不保证保真；仅保留基础行列内容。");
        var document=new FlowDocument();var section=body.Elements<W.SectionProperties>().LastOrDefault();
        var size=section?.GetFirstChild<W.PageSize>();var sourceWidth=(double)(size?.Width?.Value??11906);var sourceHeight=(double)(size?.Height?.Value??16838);
        if(sourceWidth<=0||sourceHeight<=0)throw new InvalidDataException("DOCX 页面尺寸无效，未导入。");
        var landscape=size?.Orient?.Value==W.PageOrientationValues.Landscape||sourceWidth>sourceHeight;
        var targetWidth=landscape?16838d:11906d;var targetHeight=landscape?11906d:16838d;
        if(Math.Abs(sourceWidth-targetWidth)>4||Math.Abs(sourceHeight-targetHeight)>4)
            losses.Add($"页面设置将转换为 A4（原尺寸 {sourceWidth/1440:0.##} × {sourceHeight/1440:0.##} 英寸）；保留方向并按比例换算页边距。");
        document.PageWidth=targetWidth/15;document.PageHeight=targetHeight/15;document.ColumnWidth=double.PositiveInfinity;
        var margin=section?.GetFirstChild<W.PageMargin>();
        document.PagePadding=new Thickness((margin?.Left?.Value??720)/15d*targetWidth/sourceWidth,(margin?.Top?.Value??720)/15d*targetHeight/sourceHeight,
            (margin?.Right?.Value??720)/15d*targetWidth/sourceWidth,(margin?.Bottom?.Value??720)/15d*targetHeight/sourceHeight);
        new RAWSelectionAssistant.Core.Services.Projects.PlanningPageLayout(document.PageWidth,document.PageHeight,document.PagePadding.Left,document.PagePadding.Top,document.PagePadding.Right,document.PagePadding.Bottom).Validate();
        Image ImportImage(W.Drawing drawing)
        {
            var xml=XElement.Parse(drawing.OuterXml);var blip=xml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="blip");var id=blip?.Attributes().FirstOrDefault(x=>x.Name.LocalName=="embed")?.Value;
            if(id is null)throw new NotSupportedException("外部链接图片不能安全导入；请先嵌入图片，原件未修改。");
            if(package.MainDocumentPart!.GetPartById(id) is not ImagePart imagePart)throw new InvalidDataException("图片关系没有指向图片资源。");
            using var raw=imagePart.GetStream();using var bytes=new MemoryStream();raw.CopyTo(bytes);bytes.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=bytes;bitmap.EndInit();bitmap.Freeze();
            var extent=xml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="extent");var width=double.TryParse(extent?.Attribute("cx")?.Value,out var cx)?cx/9525:480;
            var height=double.TryParse(extent?.Attribute("cy")?.Value,out var cy)?cy/9525:width*bitmap.PixelHeight/bitmap.PixelWidth;
            if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0)throw new InvalidDataException("图片尺寸无效。");
            return new Image{Source=bitmap,Width=width,Height=height,Stretch=Stretch.Fill,ToolTip=xml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="docPr")?.Attribute("descr")?.Value};
        }
        Paragraph Paragraph(W.Paragraph source){var p=new Paragraph();var style=source.ParagraphProperties?.ParagraphStyleId?.Val?.Value;if(style is "Heading1" or "Heading2" or "Heading3")p.Tag=new RAWSelectionAssistant.Core.Services.Projects.PlanningBlockSemantic(0,style);p.BreakPageBefore=source.ParagraphProperties?.PageBreakBefore is not null;foreach(var r in source.Elements<W.Run>()){if(r.Elements<W.Drawing>().Any()){foreach(var child in r.ChildElements){if(child is W.Drawing drawing)p.Inlines.Add(new InlineUIContainer(ImportImage(drawing)));else if(child is W.Text text)p.Inlines.Add(new Run(text.Text));else if(child is W.Break)p.Inlines.Add(new LineBreak());}continue;}var run=new Run(string.Concat(r.ChildElements.Select(t=>t is W.Text text?text.Text:t is W.Break?"\n":"")));if(r.RunProperties?.RunFonts?.Ascii?.Value is {} font)run.FontFamily=new FontFamily(font);if(r.RunProperties?.Bold is not null)run.FontWeight=FontWeights.Bold;if(r.RunProperties?.Italic is not null)run.FontStyle=FontStyles.Italic;if(r.RunProperties?.Underline is not null)run.TextDecorations=TextDecorations.Underline;if(r.RunProperties?.Color?.Val?.Value is {} color && color!="auto")run.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+color));if(double.TryParse(r.RunProperties?.FontSize?.Val?.Value,out var halfPoints))run.FontSize=halfPoints/1.5;var segments=run.Text.Split('\n');for(var segment=0;segment<segments.Length;segment++){if(segment>0)p.Inlines.Add(new LineBreak());p.Inlines.Add(new Run(segments[segment]){FontFamily=run.FontFamily,FontWeight=run.FontWeight,FontStyle=run.FontStyle,Foreground=run.Foreground,FontSize=run.FontSize,TextDecorations=run.TextDecorations});}}var align=source.ParagraphProperties?.Justification?.Val?.Value;p.TextAlignment=align==W.JustificationValues.Center?TextAlignment.Center:align==W.JustificationValues.Right?TextAlignment.Right:TextAlignment.Left;return p;}
        var listStack=new System.Collections.Generic.List<List>();int? currentNumber=null;
        void AddNumbered(W.Paragraph source,Paragraph paragraph,BlockCollection target)
        {
            var props=source.ParagraphProperties!.NumberingProperties!;var id=props.NumberingId?.Val?.Value??0;var level=props.NumberingLevelReference?.Val?.Value??0;
            if(level is <0 or >8){losses.Add("列表层级超过支持范围，保留为普通段落。");target.Add(paragraph);return;}
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
                if(listStack.Count==0)target.Add(list);
                else {var parent=listStack[^1];if(parent.ListItems.Count==0){parent.ListItems.Add(new ListItem(new Paragraph()));losses.Add("缺失父列表项，已补空父项。");}parent.ListItems.LastListItem.Blocks.Add(list);}
                listStack.Add(list);
            }
            listStack[level].ListItems.Add(new ListItem(paragraph));
        }
        foreach(var child in body.ChildElements)
            if(child is W.Paragraph p)
            {
                var paragraph=Paragraph(p);
                if(p.ParagraphProperties?.NumberingProperties is not null)AddNumbered(p,paragraph,document.Blocks);
                else {listStack.Clear();currentNumber=null;document.Blocks.Add(paragraph);}
            }
            else if(child is W.Table t)
            {
                listStack.Clear();currentNumber=null;var table=new Table();var group=new TableRowGroup();table.RowGroups.Add(group);
                foreach(var col in t.GetFirstChild<W.TableGrid>()?.Elements<W.GridColumn>()??[])if(double.TryParse(col.Width?.Value,out var cw))table.Columns.Add(new TableColumn{Width=new GridLength(cw/15)});
                var active=new Dictionary<int,(TableCell Cell,int Width)>();var first=true;
                foreach(var sourceRow in t.Elements<W.TableRow>())
                {
                    var row=new TableRow();group.Rows.Add(row);var column=0;var next=new Dictionary<int,(TableCell,int)>();
                    if(first&&sourceRow.TableRowProperties?.GetFirstChild<W.TableHeader>() is not null)table.Tag=new RAWSelectionAssistant.Core.Services.Projects.PlanningBlockSemantic(0){RepeatHeader=true};first=false;
                    foreach(var sourceCell in sourceRow.Elements<W.TableCell>())
                    {
                        var span=sourceCell.TableCellProperties?.GetFirstChild<W.GridSpan>()?.Val?.Value??1;if(span<1||span>1000)throw new InvalidDataException("无效合并列宽。");
                        var merge=sourceCell.TableCellProperties?.GetFirstChild<W.VerticalMerge>();
                        if(merge is not null&&merge.Val?.Value!=W.MergedCellValues.Restart)
                        {if(!active.TryGetValue(column,out var above)||above.Width!=span)throw new InvalidDataException("垂直合并缺少匹配起点，未导入。");
                         if(sourceCell.Descendants<W.Text>().Any(x=>!string.IsNullOrWhiteSpace(x.Text))||sourceCell.Descendants<W.Drawing>().Any()||sourceCell.Descendants<W.NumberingProperties>().Any())throw new InvalidDataException("合并延续格含额外内容，不能安全丢弃。");above.Cell.RowSpan++;next[column]=above;}
                        else
                        {var cell=new TableCell{ColumnSpan=span};if(sourceCell.TableCellProperties?.GetFirstChild<W.TableCellBorders>()?.TopBorder?.Val?.Value==W.BorderValues.Single){cell.BorderBrush=Brushes.Gray;cell.BorderThickness=new Thickness(1);}listStack.Clear();currentNumber=null;
                         foreach(var cp in sourceCell.Elements<W.Paragraph>()){var paragraph=Paragraph(cp);if(cp.ParagraphProperties?.NumberingProperties is not null)AddNumbered(cp,paragraph,cell.Blocks);else{listStack.Clear();currentNumber=null;cell.Blocks.Add(paragraph);}}if(cell.Blocks.Count==0)cell.Blocks.Add(new Paragraph());row.Cells.Add(cell);if(merge is not null)next[column]=(cell,span);}
                        column+=span;
                    }
                    active=next;
                }
                PlanningTableEditing.Map(table);document.Blocks.Add(table);
            }
        document.Tag=ReadPageExtras(package.MainDocumentPart!,section,losses);
        return new(document,losses);
    }
}
