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
        Heading(DocumentContent,"进阶版 · 摄影提案");
        var ribbon=new WrapPanel();
        ribbon.Children.Add(AsyncAction("保存",async()=>{await FlushDocumentAsync();}));
        ribbon.Children.Add(AsyncAction("撤销策划操作",async()=>{if(await _vm.UndoPlanningAsync())Render();}));
        ribbon.Children.Add(AsyncAction("刷新结构引用",async()=>{if(await _vm.RefreshStructureSourcesAsync())Render();}));
        ribbon.Children.Add(AsyncAction("导出摄影DOCX",ExportPlanningDocxFromViewAsync));
        ribbon.Children.Add(AsyncAction("导回摄影DOCX",ImportPlanningDocxFromViewAsync));
        DocumentContent.Children.Add(ribbon);
        DocumentContent.Children.Add(Text("正文排版与摄影结构使用同一策划身份。结构块按ID引用；移除块不删除镜头或资源。",12));
        var owner=_vm;var project=owner.ProjectId;var document=owner.PlanningDocumentId;var renderRevision=_documentRenderRevision;
        var editor=new PlanningRichEditor(owner.RichTextPackage,owner.DocumentBody,(rtf,text,layout)=>{if(renderRevision==_documentRenderRevision&&owner.ProjectId==project&&owner.PlanningDocumentId==document)owner.SaveRichText(rtf,text,layout);},owner.DocumentCanvasRevisions,owner.RichLayout);
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
                    var row=new WrapPanel();var name=new TextBox{Text=shot.Name,Width=180};var action=new TextBox{Text=shot.Action,Width=180};
                    System.Windows.Automation.AutomationProperties.SetName(name,"结构镜头名称");System.Windows.Automation.AutomationProperties.SetName(action,"结构镜头动作");
                    row.Children.Add(name);row.Children.Add(action);
                    var savedName=shot.Name;var savedAction=shot.Action;
                    _structureDraftCaptures.Add(()=>
                    {
                        if(renderRevision!=_documentRenderRevision||owner.ProjectId!=project)return;
                        if(name.Text==savedName&&action.Text==savedAction)return;
                        owner.StageStructureDraft(owner.ShotDraft(id) with{Name=name.Text,Action=action.Text});
                        savedName=name.Text;savedAction=action.Text;
                    });
                    row.Children.Add(AsyncAction("应用镜头修改",async()=>{if(await owner.ApplyStructureShotAsync(owner.ShotDraft(id) with{Name=name.Text,Action=action.Text}))Render();}));DocumentContent.Children.Add(row);
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
