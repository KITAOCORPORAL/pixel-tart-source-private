using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

public static partial class PlanningShareRichProjection
{
    [GeneratedRegex(@"\[KITAO-IMAGE:([a-fA-F0-9]{32})\]")]
    private static partial Regex ImageToken();

    public static byte[] Serialize(PlanningShareRichSnapshot snapshot) => JsonSerializer.SerializeToUtf8Bytes(snapshot, PlanningShareRichSnapshot.Json);

    public static PlanningShareRichSnapshot? Capture(byte[] savedPlanningJson, Guid projectId)
    {
        var saved = JsonSerializer.Deserialize<PlanningProjectState>(savedPlanningJson) ?? throw new InvalidDataException("策划文件为空。");
        if (saved.ProjectId != projectId) throw new InvalidDataException("策划与当前项目身份不符。");
        var package = saved.Document?.EnsureIdentity(projectId).Package ?? throw new InvalidDataException("没有已保存的策划正文。");
        if (package.RichTextPackageBase64 is not { } rich) return null;
        var doc = new FlowDocument();
        using var stream = new MemoryStream(Convert.FromBase64String(rich));
        new TextRange(doc.ContentStart, doc.ContentEnd).Load(stream, DataFormats.Rtf);
        var layout = package.RichLayout;
        var semantics = (layout?.Blocks ?? []).ToDictionary(b => b.BlockIndex);
        var images = (layout?.Images ?? []).ToDictionary(i => i.DocumentImageId);
        var losses = new HashSet<string>();
        var placed = new HashSet<Guid>();
        var body = doc.Blocks.SelectMany((block, index) => Blocks(block, semantics.GetValueOrDefault(index))).ToArray();
        if (layout?.ImportLosses.Count > 0) losses.Add("原 Word 导入已记录部分排版差异，分享使用实际保存下来的正文。");
        if (layout?.HeaderFooter is { } hf && (hf.PageNumbers || hf.Header.Length > 0 || hf.Footer.Length > 0 || hf.FirstHeader.Length > 0 || hf.FirstFooter.Length > 0)) losses.Add("正文页眉页脚只保留在桌面原稿中。");
        if (images.Values.Any(i => i.Placement is "光标" or "文字" && !placed.Contains(i.DocumentImageId))) losses.Add("个别自有图片没有可验证的正文位置，显示在文档内容区。");
        return new(projectId, package.DocumentId, Convert.ToHexString(SHA256.HashData(savedPlanningJson)), body, losses.ToArray());

        IEnumerable<PlanningShareRichBlock> Blocks(Block block, PlanningBlockSemantic? semantic = null, string? listType = null, bool withinTable = false)
        {
            var pageBreak = semantic?.PageBreakBefore == true ? true : (bool?)null;
            if (block is Paragraph styled && semantic?.Style is "Heading1" or "Heading2" or "Heading3")
            {
                styled.FontSize = semantic.Style switch { "Heading1" => 26, "Heading2" => 21, _ => 17 };
                styled.FontWeight = FontWeights.Bold;
            }
            switch (block)
            {
                case Paragraph p:
                    var text = new StringBuilder();
                    var runs = new List<PlanningShareRichMark>();
                    foreach (var inline in Inlines(p.Inlines))
                    {
                        if (inline is LineBreak) text.Append('\n');
                        else if (inline is Run run)
                        {
                            var first = text.Length;
                            text.Append(run.Text);
                            if (text.Length == first) continue;
                            var size = run.FontSize;
                            if (size is < 8 or > 126) losses.Add("部分文字字号超出网页编辑器范围，网页采用支持的相近字号。");
                            var face = run.FontFamily.Source;
                            var font = face.Contains("Courier", StringComparison.OrdinalIgnoreCase) || face.Contains("Consolas", StringComparison.OrdinalIgnoreCase) ? "mono" : face.Contains("Times", StringComparison.OrdinalIgnoreCase) || face.Contains("宋", StringComparison.Ordinal) ? "serif" : "sans";
                            var mark = new PlanningShareRichMark(first, text.Length, run.FontWeight.ToOpenTypeWeight() >= 600 ? true : null,
                                run.FontStyle == FontStyles.Italic || run.FontStyle == FontStyles.Oblique ? true : null,
                                run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Underline) ? true : null,
                                Color(run.Foreground), Math.Clamp(size, 8, 126), Color(run.Background), font);
                            if (runs.Count > 0 && runs[^1].End == mark.Start && (runs[^1] with { Start = mark.Start, End = mark.End }) == mark) runs[^1] = runs[^1] with { End = mark.End };
                            else runs.Add(mark);
                        }
                        else losses.Add("部分内嵌对象不能在线编辑，未展开放置本地原件。");
                    }
                    var kind = semantic?.Style switch { "Heading1" => "h1", "Heading2" => "h2", "Heading3" => "h3", _ => listType ?? "p" };
                    if (p.TextAlignment == TextAlignment.Justify) losses.Add("两端对齐正文在线采用左对齐。");
                    var align = p.TextAlignment switch { TextAlignment.Center => "center", TextAlignment.Right => "right", _ => "left" };
                    var value = text.ToString();
                    var pivot = 0;
                    foreach (Match match in ImageToken().Matches(value))
                    {
                        if (!Guid.TryParseExact(match.Groups[1].Value, "N", out var id) || !images.TryGetValue(id, out var image))
                            throw new InvalidDataException("富文本中存在没有归属记录的图片标记，未发布。");
                        if (match.Index > pivot) yield return Slice(pivot, match.Index);
                        placed.Add(id);
                        yield return new("caption", NativeImage: new(id, image.Caption.Length == 0 ? image.Title : image.Caption, image.Width, image.Height), PageBreakBefore: pageBreak);
                        pageBreak = null;
                        pivot = match.Index + match.Length;
                    }
                    if (value.Length > pivot || value.Length == 0) yield return Slice(pivot, value.Length);
                    if (semantic?.CanvasRevisionId is Guid revision && package.CanvasRevisions.Any(r => r.RevisionId == revision && !string.IsNullOrEmpty(r.PreviewPngBase64)))
                                            {
                        var snapshot = package.CanvasRevisions.First(r => r.RevisionId == revision);
                        var h = Math.Clamp(600d * snapshot.EditableSnapshot.PixelHeight / Math.Max(1, snapshot.EditableSnapshot.PixelWidth), 1, 12000);
                        yield return new("caption", NativeImage: new(revision, "已保存的画布快照", 600, h));
                    }
                    PlanningShareRichBlock Slice(int start, int end)
                    {
                        var result = new PlanningShareRichBlock(kind, value[start..end], align,
                            runs.Where(r => r.End > start && r.Start < end).Select(r => r with { Start = Math.Max(start, r.Start) - start, End = Math.Min(end, r.End) - start }).ToArray(), PageBreakBefore: pageBreak);
                        pageBreak = null;
                        return result;
                    }
                    break;
                case Table table when !withinTable:
                    var rows = table.RowGroups.SelectMany(g => g.Rows).Select(row => new PlanningShareRichRow(row.Cells.Select(cell =>
                        new PlanningShareRichCell(cell.ColumnSpan, cell.RowSpan, cell.Blocks.SelectMany(b => Blocks(b, withinTable: true)).ToArray())).ToArray())).ToArray();
                    yield return new("p", NativeTable: new(rows, semantic?.RepeatHeader == true), PageBreakBefore: pageBreak);
                    break;
                case Table table:
                    losses.Add("嵌套表格在所属单元格内保留文字；未重排成第二层表格。");
                    foreach (var row in table.RowGroups.SelectMany(g => g.Rows))
                        foreach (var cell in row.Cells)
                            foreach (var child in cell.Blocks.SelectMany(b => Blocks(b, withinTable: true))) yield return child;
                    break;
                case List list:
                    if (list.StartIndex != 1) losses.Add("编号列表在网页中从 1 开始。");
                    var type = list.MarkerStyle is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman ? "ol" : "ul";
                    foreach (var child in list.ListItems.SelectMany(i => i.Blocks).SelectMany(b => Blocks(b, listType: type, withinTable: withinTable))) yield return child;
                    break;
                case Section section:
                    foreach (var child in section.Blocks.SelectMany(b => Blocks(b, withinTable: withinTable))) yield return child;
                    break;
                default:
                    losses.Add("部分独立本地对象在网页中不展开。");
                    break;
            }
        }
    }

    public static (string Rtf, int RemovedObjects) SanitizeRtf(string rich)
    {
        var document = new FlowDocument();
        using var input = new MemoryStream(Convert.FromBase64String(rich));
        new TextRange(document.ContentStart, document.ContentEnd).Load(input, DataFormats.Rtf);
        var removedObjects = 0;
        StripBlocks(document.Blocks);
        using var output = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(output, DataFormats.Rtf);
        return (Convert.ToBase64String(output.ToArray()), removedObjects);

        void StripBlocks(BlockCollection blocks)
        {
            foreach (var block in blocks.ToArray())
            {
                switch (block)
                {
                    case BlockUIContainer: blocks.InsertBefore(block, new Paragraph()); blocks.Remove(block); removedObjects++; break;
                    case Paragraph p: StripInlines(p.Inlines); break;
                    case Section section: StripBlocks(section.Blocks); break;
                    case List list: foreach (var item in list.ListItems) StripBlocks(item.Blocks); break;
                    case Table table: foreach (var cell in table.RowGroups.SelectMany(g => g.Rows).SelectMany(r => r.Cells)) StripBlocks(cell.Blocks); break;
                }
            }
        }
        void StripInlines(InlineCollection inlines)
        {
            foreach (var inline in inlines.ToArray())
            {
                if (inline is InlineUIContainer) { inlines.Remove(inline); removedObjects++; continue; }
                if (inline is Hyperlink link) { link.NavigateUri = null; link.TargetName = ""; }
                if (inline is Span span) StripInlines(span.Inlines);
            }
        }
    }

    private static IEnumerable<Inline> Inlines(InlineCollection children)
    {
        foreach(var inline in children)
            if (inline is Span span) { foreach (var child in Inlines(span.Inlines)) yield return child; }
            else yield return inline;
    }

    private static string? Color(Brush? brush) => brush is SolidColorBrush b && b.Color.A == 255 ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : null;
}
