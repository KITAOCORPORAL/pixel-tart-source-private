using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PlanningCenterView
{
    private readonly List<Action> _structureDraftCaptures=[];
    private async void SimpleMode_Click(object sender,RoutedEventArgs e){if(_vm is not null&&await FlushDocumentAsync())await _vm.SwitchPlanningModeAsync(false);}
    private async void AdvancedMode_Click(object sender,RoutedEventArgs e){if(_vm is not null&&await FlushDocumentAsync())await _vm.SwitchPlanningModeAsync(true);}
    private void RenderAdvancedPlanning()
    {
        if(_vm is null)return;
        var heading=Text("摄影提案 · 文档工作区",16,true);heading.Margin=new Thickness(0,0,0,6);DocumentContent.Children.Add(heading);
        var ribbon=new WrapPanel();
        ribbon.Children.Add(AsyncAction("保存",async()=>{await FlushDocumentAsync();}));
        ribbon.Children.Add(AsyncAction("撤销策划操作",async()=>{if(await _vm.UndoPlanningAsync())Render();}));
        ribbon.Children.Add(AsyncAction("刷新结构引用",async()=>{if(await _vm.RefreshStructureSourcesAsync())Render();}));
        ribbon.Children.Add(AsyncAction("导出摄影DOCX",ExportPlanningDocxFromViewAsync));
        ribbon.Children.Add(AsyncAction("导回摄影DOCX",ImportPlanningDocxFromViewAsync));
        DocumentContent.Children.Add(ribbon);
        ribbon.ToolTip="正文与结构引用共用同一策划；移除块不删除源镜头。";
        var owner=_vm;var project=owner.ProjectId;var document=owner.PlanningDocumentId;var renderRevision=_documentRenderRevision;var generation=owner.EditorGeneration;
        var editor=new PlanningRichEditor(owner.RichTextPackage,owner.DocumentBody,(rtf,text,layout)=>{if(generation==owner.EditorGeneration&&renderRevision==_documentRenderRevision&&owner.ProjectId==project&&owner.PlanningDocumentId==document)owner.SaveRichText(rtf,text,layout);},owner.DocumentCanvasRevisions,owner.RichLayout);
        editor.ApplyPlanningPaperAppearance();editor.UsePlanningDocxActions(ExportPlanningDocxFromViewAsync,ImportPlanningDocxFromViewAsync,error=>owner.Dialogs.ShowError("DOCX 操作未完成，原资料保留："+error.Message));editor.SetPageZoom(owner.UiLayout.DocumentZoom);
        editor.PageZoomChanged+=async value=>{if(generation==owner.EditorGeneration&&renderRevision==_documentRenderRevision&&owner.ProjectId==project&&owner.PlanningDocumentId==document)await owner.SaveUiLayoutAsync(owner.UiLayout with{DocumentZoom=value});};
        DocumentContent.Children.Add(editor);
        foreach(var block in owner.StructureBlocks.ToArray())
        {
            Heading(DocumentContent,block.Kind=="Shots"?"分镜表 · 结构化引用":"摄影资源 · 结构化引用");
            DocumentContent.Children.Add(AsyncAction("移除此结构块",async()=>{if(await FlushDocumentAsync())await owner.RemoveStructureBlockAsync(block.Id);}));
            if(block.Kind=="Shots")
            {
                foreach(var id in block.SourceIds)
                {
                    var shot=owner.Shots.FirstOrDefault(s=>s.ShotId==id);if(shot is null){DocumentContent.Children.Add(Text("归档或缺失镜头："+id,12));continue;}
                    var row=new WrapPanel();var name=new TextBox{Text=shot.Name,Width=240,TextWrapping=TextWrapping.Wrap,MinHeight=64};var action=new TextBox{Text=shot.Action,Width=280,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,MinHeight=96,MaxHeight=160,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
                    System.Windows.Automation.AutomationProperties.SetName(name,"结构镜头名称");System.Windows.Automation.AutomationProperties.SetName(action,"结构镜头动作");
                    row.Children.Add(name);row.Children.Add(action);
                    var savedName=shot.Name;var savedAction=shot.Action;
                    _structureDraftCaptures.Add(()=>
                    {
                        if(generation!=owner.EditorGeneration||renderRevision!=_documentRenderRevision||owner.ProjectId!=project)return;
                        if(name.Text==savedName&&action.Text==savedAction)return;
                        var latest=owner.ShotDraft(id);
                        owner.StageStructureDraft(latest with{Name=name.Text==savedName?latest.Name:name.Text,Action=action.Text==savedAction?latest.Action:action.Text});
                        savedName=name.Text;savedAction=action.Text;
                    });
                    row.Children.Add(AsyncAction("应用镜头修改",async()=>{if(generation!=owner.EditorGeneration||renderRevision!=_documentRenderRevision)return; if(await FlushDocumentAsync())Render();}));DocumentContent.Children.Add(row);
                }
            }
            else foreach(var id in block.SourceIds)
            {
                var item=owner.ResourceCatalog.Items.FirstOrDefault(i=>i.Id==id);if(item is null)continue;
                DocumentContent.Children.Add(Action(item.Name+(item.Archived?"（归档）":""),()=>EditPlanningResource(item)));
            }
        }
    }
}
