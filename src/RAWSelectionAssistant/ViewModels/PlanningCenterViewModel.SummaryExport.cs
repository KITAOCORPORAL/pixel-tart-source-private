using System.IO;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class PlanningCenterViewModel
{
    public string SummaryExportStatus {get;private set;}="";
    public async Task<PlanningSummaryExportResult?> ExportPlanningSummaryDocxAsync(string path)
    {
        if(ProjectId is not Guid project||!await FlushAsync())return null;
        string Baseline()=>System.Text.Json.JsonSerializer.Serialize(new{Document=DocxBaseline(),Summary=_state?.Summary,Booking=_booking,VisualLinks=_state?.VisualLinks});
        var baseline=Baseline();
        // Read through the existing read-only ledger delegate. A disconnected/failed source is declared,
        // never replaced with an invented zero or old values for a different project.
        IReadOnlyList<RAWSelectionAssistant.Core.Models.FinanceTransaction> expenses=[];var expenseState="收支只读来源未连接；未读取账本";
        if(!IsPackageImported && ReadPlanningExpenses is{} readExpenses)
        {
            try{expenses=(await readExpenses(project)).Where(x=>x.ProjectId==project&&x.Kind==RAWSelectionAssistant.Core.Models.FinanceTransactionKind.Expense).DistinctBy(x=>x.Id).ToArray();expenseState="本次按项目只读读取成功；未修改账本";}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or InvalidOperationException){expenseState="本次账本读取失败；未使用旧缓存："+error.GetType().Name;}
        }
        if(ProjectId!=project)throw new InvalidOperationException("策划已切换，未导出旧项目。");
        if(Baseline()!=baseline)throw new InvalidOperationException("读取期间策划内容已变化，请保存后重新汇总。");
        var full=Path.GetFullPath(path);
        if(!string.Equals(Path.GetExtension(full),".docx",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("15区汇总当前仅导出DOCX阅读快照。");
        if(File.Exists(full))throw new InvalidOperationException("汇总导出须选择新文件；已有DOCX保持不变，请另存新名称。");
        var external=_document.Attachments.Select(a=>a.Path).Concat(_document.References.Select(r=>r.Source.ExternalReference)).Concat(Shots.SelectMany(s=>s.References).Select(r=>r.ExternalReference)).Concat(_document.Resources.Items.OfType<PlanningLight>().Select(l=>l.ReferencePath)).Concat(expenses.SelectMany(e=>e.AttachmentPaths));
        if(external.Where(p=>!string.IsNullOrWhiteSpace(p)&&Path.IsPathFullyQualified(p!)).Any(p=>string.Equals(Path.GetFullPath(p!),full,StringComparison.OrdinalIgnoreCase))||
           string.Equals(full,Path.Combine(_planning.StorageRoot,$"{project:N}.planning.json"),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("汇总输出不能覆盖策划原文件或外部引用。");
        // Bind only the root on the detached export projection. Each image is verified during rendering;
        // missing images become explicit placeholders instead of mutating/recovering the stored plan.
        var source=_document.Package?.RichLayout is{} layout
            ?_document with{Package=_document.Package with{RichLayout=layout with{Images=layout.Images.Select(i=>i with{StorageRoot=_planning.StorageRoot}).ToArray()}}}
            :_document;
        source=PlanningProjectCopyStorage.Bind(source,_planning.StorageRoot);
        var input=new PlanningSummaryInput(project,ProjectName,source,_state?.Summary??new(),Shots.Select(s=>ShotDraft(s.ShotId)).ToArray(),_booking,expenses,expenseState,_state?.VisualLinks);
        var result=PlanningSummaryDocument.Export(PlanningReadableSummaryBuilder.Build(input),source,full);
        SummaryExportStatus=$"15区汇总DOCX已导出；{result.Warnings.Count}项文档图片警告。项目自有参考按安全PNG规则呈现；普通附件列名称/状态；未导入外链不打包。";
        OnPropertyChanged(nameof(SummaryExportStatus));return result;
    }
}
