using System.Windows;
using System.Windows.Controls;
namespace RAWSelectionAssistant.Views;
public partial class PlanningCenterView
{
    private async Task ExportPlanningSummaryFromViewAsync()
    {
        if(_vm is null)return;
        if(!await FlushDocumentAsync()){_vm.Dialogs.ShowError("当前策划保存未完成，未导出汇总："+_vm.SaveStatus);return;}
        var path=_vm.Dialogs.ChooseSaveFile("导出15区汇总（阅读快照，非项目便携包）","DOCX|*.docx",".docx",_vm.DocumentTitle+"-15区汇总.docx");
        if(path is null)return;
        try{if(await _vm.ExportPlanningSummaryDocxAsync(path) is{} result)_vm.Dialogs.ShowInfo(_vm.SummaryExportStatus+"\n汇总用于阅读，不回写15区资源；原摄影DOCX导出/导回保持独立。");else _vm.Dialogs.ShowError("当前策划保存未完成，未导出汇总："+_vm.SaveStatus);}
        catch(Exception error)when(error is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or System.Xml.XmlException)
        {_vm.Dialogs.ShowError("15区汇总未完成，原资料与已有输出保留："+error.Message);}
    }
    private async Task ExportPlanningDocxFromViewAsync()
    {
        if(_vm is null||!await FlushDocumentAsync())return;
        var path=_vm.Dialogs.ChooseSaveFile("导出摄影提案DOCX","DOCX|*.docx",".docx",_vm.DocumentTitle+".docx");
        if(path is not null)await _vm.ExportPlanningDocxAsync(path);
    }
    private async Task ImportPlanningDocxFromViewAsync()
    {
        if(_vm is null||!await FlushDocumentAsync())return;
        var paths=_vm.Dialogs.ChooseFiles("导入DOCX并预览差异","DOCX|*.docx",false);if(paths.Count!=1)return;
        var review=_vm.PreviewPlanningDocxImport(paths[0]);ModalContent.Children.Clear();Heading(ModalContent,"DOCX导回 · 差异与损失");
        var panel=new StackPanel();panel.Children.Add(Text("确认后导入普通正文；勾选的镜头字段才回写。先保存恢复点，支持一步撤销。",12));
        foreach(var loss in review.Losses)panel.Children.Add(Text("转换/损失："+loss,12));
        var checks=new Dictionary<Guid,CheckBox>();foreach(var diff in review.Differences)
        {
            var check=new CheckBox{IsChecked=true,Content=$"{diff.BeforeName} → {diff.AfterName}\n场景：{diff.BeforeScene} → {diff.AfterScene}\n动作：{diff.BeforeAction} → {diff.AfterAction}",Margin=new Thickness(4)};
            checks[diff.ShotId]=check;panel.Children.Add(check);
        }
        ModalContent.Children.Add(new ScrollViewer{Content=panel,MaxHeight=430,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        ModalContent.Children.Add(AsyncAction("确认导入",async()=>{if(await _vm.ApplyPlanningDocxImportAsync(review,checks.Where(p=>p.Value.IsChecked==true).Select(p=>p.Key).ToArray(),true)){ModalOverlay.Visibility=Visibility.Collapsed;Render();}},"PrimaryButton"));
        ModalContent.Children.Add(Action("取消",()=>ModalOverlay.Visibility=Visibility.Collapsed));ModalOverlay.Visibility=Visibility.Visible;FocusOverlay(ModalContent);
    }
}
