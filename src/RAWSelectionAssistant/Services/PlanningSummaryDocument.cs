using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;

public sealed record PlanningSummaryExportResult(string Path,int Sections,IReadOnlyList<string> Warnings);
public static class PlanningSummaryDocument
{
    public static FlowDocument Create(PlanningReadableSummary summary,PlanningDocument source,ICollection<string> warnings)
    {
        var document=new FlowDocument{FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=12,PageWidth=794,PageHeight=1123,PagePadding=new Thickness(48),ColumnWidth=double.PositiveInfinity};
        var page=(source.Package?.RichLayout?.Page??new PlanningPageLayout()).Validate();document.PageWidth=page.Width;document.PageHeight=page.Height;document.PagePadding=new Thickness(page.Left,page.Top,page.Right,page.Bottom);
        document.Tag=source.Package?.RichLayout?.HeaderFooter??new PlanningHeaderFooter();
        document.Blocks.Add(new Paragraph(new Run("摄影策划 · 15区汇总")){FontSize=22,FontWeight=FontWeights.Bold});
        Text($"项目 {summary.ProjectId:D} · 文档 {summary.DocumentId:D}");
        Text("只读阅读快照：按现有15区顺序。引用仅列出状态，不是可搬迁项目包；外链和附件未自动嵌入。汇总不是摄影DOCX往返合同，导回只按普通正文处理，不回写15区资源。");
        for(int index=0;index<summary.Sections.Count;index++)
        {
            var section=summary.Sections[index];document.Blocks.Add(new Paragraph(new Run($"{index+1:00} {section.Name}")){FontSize=18,FontWeight=FontWeights.Bold,Tag=new PlanningBlockSemantic(document.Blocks.Count,"Heading1"),KeepWithNext=true});
            if(section.Entries.Count==0){Text("本区未填写内容。");continue;}
            foreach(var entry in section.Entries)
            {
                document.Blocks.Add(new Paragraph(new Run(entry.Title)){FontWeight=FontWeights.Bold,KeepWithNext=true});
                foreach(var field in entry.Fields)
                {
                    if(index==1&&field.Key=="内容"&&source.Package?.RichTextPackageBase64 is{} rich)
                    {AppendRich(rich,source.Package.RichLayout);continue;}
                    if(index==14&&field.Key=="内容"&&source.Package?.RichTextPackageBase64 is not null)Text("本区共用的持久富文本已在02文字完整列出；以下为原补充文档内容，不重复创建另一份正文。");
                    Text(field.Key+"："+field.Value);
                }
            }
        }
        if(warnings.Count>0){Text("导出警告（保留原资料，不替换源文件）");foreach(var warning in warnings)Text(warning);}
        return document;
        void Text(string value)=>document.Blocks.Add(new Paragraph(new Run(value)){Margin=new Thickness(0,2,0,6)});
        void AppendRich(string rich,PlanningRichLayout? layout)
        {
            // Work on a new RTF document. Never mutate source layout, image metadata or persisted RTF.
            var body=PlanningDocumentLayout.Load(rich,layout is null?null:layout with{Images=[]});
            foreach(var meta in layout?.Images??[])
            {
                var token="[KITAO-IMAGE:"+meta.DocumentImageId.ToString("N")+"]";var matches=PlanningTextSearch.Find(body,token,true);
                foreach(var match in matches.Reverse())
                {
                    var pointers=PlanningTextSearch.Range(match);var range=new TextRange(pointers.Start,pointers.End);
                    try{var image=PlanningDocumentImages.Create(meta);range.Text="";new InlineUIContainer(image,pointers.Start);}
                    catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException)
                    {var message=$"[文档图片不可用：{meta.Title} / {meta.DocumentImageId:D}；{e.GetType().Name}；保留原引用]";range.Text=message;warnings.Add(message);}
                }
                if(matches.Count==0)warnings.Add($"文档图片 {meta.DocumentImageId:D}（{meta.Title}）没有匹配正文位置；未擅自追加图片。");
            }
            foreach(var block in body.Blocks.ToArray()){body.Blocks.Remove(block);document.Blocks.Add(block);}
        }
    }
    public static PlanningSummaryExportResult Export(PlanningReadableSummary summary,PlanningDocument source,string path)
    {
        var warnings=new List<string>();var document=Create(summary,source,warnings);
        PlanningDocxAdapter.Export(document,path);return new(Path.GetFullPath(path),summary.Sections.Count,warnings);
    }
}
