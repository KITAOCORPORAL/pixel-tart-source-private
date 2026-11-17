using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
class Program
{
 [STAThread] static int Main(string[] args)
 {
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);int code=0;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  app.Startup+=async(_,_)=>{try{await Run(root);}catch(Exception e){File.WriteAllText(Path.Combine(root,"FAILURE.txt"),e.ToString());code=3;}finally{app.Shutdown(code);}};app.Run();return code;
 }
 static void Check(bool x,string m){if(!x)throw new InvalidOperationException(m);}
 static async Task Run(string root)
 {
  var store=new InMemorySelectionWorkspaceStore();var vm=new OnlineSelectionViewModel(store:store){ClientName="preserved-client",TargetCountText="1"};var id=Guid.NewGuid();var rows=new List<object>();
  foreach(var name in new[]{"","   ","\t\r\n"})
  {
   vm.ProjectName=name;var result=await vm.CreateForWorkflowAsync([Path.Combine(root,"must-not-read.png")],id);var state=await store.LoadAsync();
   Check(!result.Succeeded&&result.ProjectId is null&&result.Message=="请填写项目名"&&state.Projects.Count==0&&vm.Projects.Count==0,"Blank name must reject without project");Check(vm.ClientName=="preserved-client"&&vm.TargetCountText=="1","Other fields preserved");rows.Add(new{name,rejected=true,projects=state.Projects.Count});
  }
  vm.ProjectName="named synthetic";var invalid=await vm.CreateForWorkflowAsync([],Guid.Empty);Check(!invalid.Succeeded&&(await store.LoadAsync()).Projects.Count==0,"Invalid workflow rejected");
  var created=await vm.CreateForWorkflowAsync([],id);Check(created.Succeeded&&created.ProjectId is not null&&vm.ProjectPage.Project?.WorkflowProjectId==id&&(await store.LoadAsync()).Projects.Count==1,"Named create explicit success");
  var source=Path.Combine(root,"synthetic.png");var image=BitmapSource.Create(8,8,96,96,PixelFormats.Bgra32,null,new byte[256],32);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(source))encoder.Save(stream);
  using var studio=new ReferenceColorWorkspaceViewModel(new Dialogs());await studio.LoadTargetAsync(source);studio.BindWorkflowProject(id);int handoffs=0;studio.OpenOnlineSelection=_=>{handoffs++;return Task.FromResult(true);};
  var parent=Path.Combine(Path.GetTempPath(),"PixelTartPublishing");var before=Directory.Exists(parent)?Directory.GetDirectories(parent).ToHashSet():[];
  studio.ValidateOnlineSelection=_=>Task.FromResult<string?>("请填写项目名");await studio.PrepareOnlineSelectionCommand.ExecuteAsync(null);
  var after=Directory.Exists(parent)?Directory.GetDirectories(parent).ToHashSet():[];Check(before.SetEquals(after)&&handoffs==0&&studio.StatusText=="请填写项目名","Reject before intermediate lease/PNG/render handoff");
  studio.ValidateOnlineSelection=_=>Task.FromResult<string?>("工作流或版本无效");await studio.PrepareOnlineSelectionCommand.ExecuteAsync(null);after=Directory.Exists(parent)?Directory.GetDirectories(parent).ToHashSet():[];Check(before.SetEquals(after)&&handoffs==0,"Invalid workflow preflight has zero intermediates");
  studio.ValidateOnlineSelection=_=>Task.FromResult<string?>(null);await studio.PrepareOnlineSelectionCommand.ExecuteAsync(null);Check(handoffs==1,"Valid workflow reaches result-aware handoff");
  File.WriteAllText(Path.Combine(root,"RESULT.json"),JsonSerializer.Serialize(new{status="PASS",blankCases=rows,namedCreated=created.Succeeded,namedId=created.ProjectId,workflowId=id,preRenderRejectedWithoutPngOrLease=true,otherFieldsPreserved=true,invalidWorkflowRejected=true,validRenderedHandoff=handoffs,nativeRawDecodes=0},new JsonSerializerOptions{WriteIndented=true}));
 }
 sealed class Dialogs:IDialogService
 {public string? ChooseFolder(string t,string? d=null)=>null;public IReadOnlyList<string> ChooseFiles(string t,string f,bool m=true)=>[];public string? ChooseSaveFile(string t,string f,string e,string? n=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m)=>throw new IOException(m);public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> c)=>null;public bool ShowMediaDetails(MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
