using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed record PlanningShotDifference(Guid ShotId,string BeforeName,string AfterName,string BeforeScene,string AfterScene,string BeforeAction,string AfterAction);
public sealed record PlanningDocxReview(Guid ProjectId,Guid DocumentId,string Baseline,string Rtf,string Text,PlanningRichLayout Layout,IReadOnlyList<PlanningShotDifference> Differences,IReadOnlyList<string> Losses);
public sealed partial class PlanningCenterViewModel
{
    private string? _lastDocxImportContent;
    private string? _lastDocxImportBaseline;
    private string DocxBaseline()=>JsonSerializer.Serialize(new {Document=_document,Shots=Shots.Select(s=>ShotDraft(s.ShotId)).ToArray()});
    private static string ParagraphText(Block block)=>new TextRange(block.ContentStart,block.ContentEnd).Text.TrimEnd('\r','\n');
    public async Task<bool> ExportPlanningDocxAsync(string path)
    {
        if(!await FlushAsync()||PlanningDocumentId is not Guid doc)return false;
        var content=RichTextPackage is {} rich?PlanningDocumentLayout.Load(rich,RichLayout):new FlowDocument(new Paragraph(new Run(DocumentBody)));
        content.Blocks.Add(new Paragraph(new Run("KitaoShotTableV1:"+doc.ToString("D"))));
        var table=new Table();var group=new TableRowGroup();table.RowGroups.Add(group);
        void Row(params string[] values){var row=new TableRow();foreach(var value in values)row.Cells.Add(new TableCell(new Paragraph(new Run(value))));group.Rows.Add(row);}
        Row("ShotId","名称","场景","人物动作");foreach(var shot in Shots)Row(shot.ShotId.ToString("D"),shot.Name,shot.Scene??"",shot.Action);
        content.Blocks.Add(table);PlanningDocxAdapter.Export(content,path);return true;
    }
    public PlanningDocxReview PreviewPlanningDocxImport(string path)
    {
        if(ProjectId is not Guid project||PlanningDocumentId is not Guid doc)throw new InvalidOperationException("请先打开策划。");
        var imported=PlanningDocxAdapter.Import(path);var losses=imported.Losses.ToList();var differences=new List<PlanningShotDifference>();
        var blocks=imported.Document.Blocks.ToArray();var seen=new HashSet<Guid>();
        for(var i=0;i<blocks.Length-1;i++)
        {
            var marker=ParagraphText(blocks[i]);if(!marker.StartsWith("KitaoShotTableV1:",StringComparison.Ordinal))continue;
            if(marker!="KitaoShotTableV1:"+doc.ToString("D")){losses.Add("摄影分镜表属于另一文档，仅作为普通内容导入，不回写镜头。");continue;}
            if(blocks[i+1] is not Table table){losses.Add("分镜标记后缺少表格，不回写。");continue;}
            var rows=table.RowGroups.SelectMany(g=>g.Rows).ToArray();
            string[] Cells(TableRow row)=>row.Cells.Select(c=>new TextRange(c.ContentStart,c.ContentEnd).Text.Replace("\r\n","\n").TrimEnd('\r','\n')).ToArray();
            if(rows.Length==0||!Cells(rows[0]).SequenceEqual(new[]{"ShotId","名称","场景","人物动作"})){losses.Add("分镜表头已改变，不能安全回写。");continue;}
            foreach(var row in rows.Skip(1))
            {
                var values=Cells(row);
                if(values.Length!=4||!Guid.TryParse(values[0],out var id)||!seen.Add(id)){losses.Add("无效或重复镜头ID行未回写。");continue;}
                var current=Shots.FirstOrDefault(s=>s.ShotId==id);if(current is null){losses.Add("未知/归档镜头ID未创建或回写："+id);continue;}
                if(string.IsNullOrWhiteSpace(values[1])){losses.Add("空名称镜头行不回写："+id);continue;}
                if(current.Name!=values[1]||(current.Scene??"")!=values[2]||current.Action!=values[3])differences.Add(new(id,current.Name,values[1],current.Scene??"",values[2],current.Action,values[3]));
            }
            // This table is a projection, not a second copy in ordinary content.
            imported.Document.Blocks.Remove(blocks[i]);imported.Document.Blocks.Remove(table);
        }
        using var stream=new MemoryStream();var range=new TextRange(imported.Document.ContentStart,imported.Document.ContentEnd);range.Save(stream,DataFormats.Rtf);
        var page=new PlanningPageLayout(double.IsFinite(imported.Document.PageWidth)?imported.Document.PageWidth:794,double.IsFinite(imported.Document.PageHeight)?imported.Document.PageHeight:1123,
            imported.Document.PagePadding.Left,imported.Document.PagePadding.Top,imported.Document.PagePadding.Right,imported.Document.PagePadding.Bottom);
        return new(project,doc,DocxBaseline(),Convert.ToBase64String(stream.ToArray()),range.Text,PlanningDocumentLayout.Capture(imported.Document,page,losses),differences,losses);
    }
    public async Task<bool> ApplyPlanningDocxImportAsync(PlanningDocxReview review,IReadOnlyCollection<Guid> selectedIds,bool confirmed)
    {
        if(!confirmed)return false;
        if(review.ProjectId!=ProjectId||review.DocumentId!=PlanningDocumentId||review.Baseline!=DocxBaseline())throw new InvalidOperationException("策划已改变，请重新预览导入差异。");
        if(!await FlushAsync())return false;
        if(review.Baseline!=DocxBaseline())throw new InvalidOperationException("当前编辑已改变导入基线，请重新预览。");
        // Re-confirming the identical review must not create another recovery point or undo step.
        var selected=review.Differences.Where(d=>selectedIds.Contains(d.ShotId)).ToArray();
        var contentIdentity=JsonSerializer.Serialize(new {review.Rtf,review.Text,review.Layout});
        if (selected.Length==0 && ((_lastDocxImportContent==contentIdentity && _lastDocxImportBaseline==DocxBaseline())
            || (RichTextPackage==review.Rtf && DocumentBody==review.Text && JsonSerializer.Serialize(RichLayout)==JsonSerializer.Serialize(review.Layout)))) return true;
        var recovery=_planning.DraftPath(review.ProjectId)+".docx-recovery-"+Guid.NewGuid().ToString("N")+".json";
        await File.WriteAllTextAsync(recovery,JsonSerializer.Serialize(new PlanningDocumentDraft(review.ProjectId,DetachPlanning(_document),_state!.Summary??new(),Shots.Select(s=>ShotDraft(s.ShotId)).ToArray())));
        CapturePlanningUndo();
        EditorGeneration++;
        SaveRichText(review.Rtf,review.Text,review.Layout);
        foreach(var diff in review.Differences.Where(d=>selectedIds.Contains(d.ShotId)))StageShot(ShotDraft(diff.ShotId) with{Name=diff.AfterName,Scene=diff.AfterScene,Action=diff.AfterAction});
        var saved=await FlushProgrammaticPlanningAsync();
        PresentationChanged(); // Display retained import drafts even if persistence failed.
        if(saved)
        {
            // Capture the new editor's RTF normalization, not the retired editor.
            saved=await FlushAsync();
            if(saved){_lastDocxImportContent=contentIdentity;_lastDocxImportBaseline=DocxBaseline();}
        }
        return saved;
    }
}
