using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Services;

/// <summary>A local, dependency-free visual PDF. WPF paginates the document with
/// Windows Chinese fonts; pages are embedded at 144 DPI. Text is not selectable.</summary>
public static class PlanningProposalPdf
{
    public static FlowDocument CreateDocument(PlanningCenterViewModel vm)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 14, Foreground = Brushes.Black,
            Background = Brushes.White, PageWidth = 794, PageHeight = 1123, PagePadding = new Thickness(64),
            ColumnWidth = double.PositiveInfinity, LineHeight = 24
        };
        void Text(string value, double size = 14, bool heading = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            doc.Blocks.Add(new Paragraph(new Run(value))
            {
                FontSize = size, FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(0, heading ? 20 : 4, 0, heading ? 12 : 10), KeepWithNext = heading
            });
        }
        void Picture(PlanningReferenceItem reference)
        {
            if (reference.PreviewPath is not string path || !File.Exists(path)) { Text(reference.Title + " · 源文件暂不可用"); return; }
            try
            {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
                var image = new Image { Source = bitmap, Width = 640, Height = Math.Min(430, 640d * bitmap.PixelHeight / bitmap.PixelWidth), Stretch = Stretch.Uniform };
                doc.Blocks.Add(new BlockUIContainer(image) { Margin = new Thickness(0, 18, 0, 8) });
                Text(reference.Title, 11);
            }
            catch (Exception error) when (error is IOException or NotSupportedException) { Text(reference.Title + " · 图片暂不可用"); }
        }
        Text("摄影策划案 · " + vm.ProposalStatus, 11); Text(vm.DocumentTitle, 32, true); Text(vm.DocumentSubtitle, 20);
        Text(vm.DocumentDate + " · " + vm.DocumentLocation + " · " + vm.DocumentPeople, 12);
        foreach (var line in vm.DocumentBody.Split('\n'))
        {
            if (line.Trim() == "---") { doc.Blocks.Add(new Paragraph { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) }); continue; }
            var heading = line.StartsWith("# ", StringComparison.Ordinal);
            var value = heading || line.StartsWith("> ", StringComparison.Ordinal) ? line[2..] : line.StartsWith("- ", StringComparison.Ordinal) ? "• " + line[2..] : line;
            var paragraph = new Paragraph { FontSize = heading ? 23 : 14, KeepWithNext = heading, Margin = new Thickness(0, heading ? 18 : 4, 0, 10) };
            var parts = value.Split("**"); for (var i = 0; i < parts.Length; i++) paragraph.Inlines.Add(i % 2 == 1 ? new Bold(new Run(parts[i])) : new Run(parts[i]));
            doc.Blocks.Add(paragraph);
        }
        foreach (var reference in vm.HeroReferences) Picture(reference);
        foreach (var section in new[] { ("拍摄目标", vm.ShootGoal), ("视觉关键词", vm.Keywords), ("客户要求", vm.ClientRequirements), ("必拍内容", vm.MustCapture), ("注意事项", vm.PlanningNotes), ("交付用途", vm.OutputPurpose) })
        { Text(section.Item1, 22, true); Text(section.Item2); }
        Text("视觉方向", 22, true);
        Text("项目配色：" + string.Join("  ", vm.PaletteColors.Select(color => color.Hex)));
        Text("项目色彩方案：" + string.Join(" / ", vm.ColorSchemes.Select(look => look.Name)));
        foreach (var section in new[] { "情绪板", "灯光图", "服化道" })
        {
            var references = vm.AllProjectReferences.Where(item => section switch { "情绪板" => item.IsMoodboard, "灯光图" => item.Category == "灯光", _ => item.Category == "造型" }).ToArray();
            if (references.Length == 0) continue;
            Text(section, 26, true); foreach (var reference in references) Picture(reference);
        }
        Text("镜头清单", 26, true);
        foreach (var shot in vm.Shots) { Text($"镜头 {shot.Order + 1:00} · {shot.Name}", 18, true); Text($"{shot.Scene} · 预计 {shot.EstimatedMinutes} 分钟"); Text(shot.Notes ?? ""); }
        if (vm.Attachments.Count > 0) { Text("项目文件", 22, true); foreach (var item in vm.Attachments) Text(item.Name); }
        return doc;
    }

    public static void Export(PlanningCenterViewModel vm, string path)
    {
        var document = CreateDocument(vm);
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(794, 1123); paginator.ComputePageCount();
        var pages = new List<byte[]>();
        for (var i = 0; i < paginator.PageCount; i++)
        {
            using var page = paginator.GetPage(i);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 794, 1123));
                drawing.DrawRectangle(new VisualBrush(page.Visual), null, new Rect(0, 0, 794, 1123));
                var footer = new FormattedText($"{i + 1} / {paginator.PageCount}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 10, Brushes.Gray, 1);
                drawing.DrawText(footer, new Point(730 - footer.Width, 1090));
            }
            var bitmap = new RenderTargetBitmap(1191, 1685, 144, 144, PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder = new JpegBitmapEncoder { QualityLevel = 94 }; encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var buffer = new MemoryStream(); encoder.Save(buffer); pages.Add(buffer.ToArray());
        }
        var temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { WritePdf(temporary, pages); File.Move(temporary, Path.GetFullPath(path), true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void WritePdf(string path, IReadOnlyList<byte[]> pages)
    {
        using var stream = File.Create(path);
        void Write(string value) { var data = Encoding.ASCII.GetBytes(value); stream.Write(data); }
        var offsets = new List<long> { 0 };
        void Object(int number, string value) { offsets.Add(stream.Position); Write($"{number} 0 obj\n{value}\nendobj\n"); }
        Write("%PDF-1.4\n");
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{3 + i * 3} 0 R"))}] >>");
        for (var i = 0; i < pages.Count; i++)
        {
            var n = 3 + i * 3;
            Object(n, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.5 842.25] /Resources << /XObject << /Im0 {n + 1} 0 R >> >> /Contents {n + 2} 0 R >>");
            offsets.Add(stream.Position); Write($"{n + 1} 0 obj\n<< /Type /XObject /Subtype /Image /Width 1191 /Height 1685 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {pages[i].Length} >>\nstream\n");
            stream.Write(pages[i]); Write("\nendstream\nendobj\n");
            const string content = "q 595.5 0 0 842.25 0 0 cm /Im0 Do Q";
            Object(n + 2, $"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }
        var xref = stream.Position; Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }
}
