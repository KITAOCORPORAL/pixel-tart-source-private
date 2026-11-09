using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
public sealed partial class CanvasFavoriteAppAcceptanceTests
{
 private static async Task HttpField(MainWindow window,string id,string value){var handle=new WindowInteropHelper(window).Handle;await Task.Run(()=>{var e=AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));Assert.IsNotNull(e,"Native field "+id);((ValuePattern)e.GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);});}
 private static async Task HttpFileDialog(MainWindow window,string title,string file,bool folder=false)
 {
  var handle=new WindowInteropHelper(window).Handle;await Task.Run(async()=>{AutomationElement? dialog=null;var until=DateTime.UtcNow.AddSeconds(30);while(dialog is null&&DateTime.UtcNow<until){dialog=AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,title))??AutomationElement.RootElement.FindFirst(TreeScope.Children,new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty,Environment.ProcessId),new PropertyCondition(AutomationElement.NameProperty,title)));if(dialog is null)await Task.Delay(50);}if(dialog is null){var windows=AutomationElement.RootElement.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ProcessIdProperty,Environment.ProcessId));var descriptions=windows.Cast<AutomationElement>().Select(x=>new{name=x.Current.Name,id=x.Current.AutomationId,type=x.Current.ControlType.ProgrammaticName}).ToArray();await File.WriteAllTextAsync(Path.Combine(Root,"NATIVE_DIALOG_FAILURE.json"),JsonSerializer.Serialize(descriptions));}Assert.IsNotNull(dialog,"Native file dialog "+title);
if(Environment.GetEnvironmentVariable("KITAO_HTTP_NATIVE_PICKER_MANUAL")=="1")
{
 await File.WriteAllTextAsync(Path.Combine(Root,"PICKER_READY.json"),JsonSerializer.Serialize(new{processId=Environment.ProcessId,handle=handle.ToInt64(),title,file,folder}));
 var waitUntil=DateTime.UtcNow.AddSeconds(120);
 while(DateTime.UtcNow<waitUntil)
 {
  try{if(AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,title)) is null)return;}
  catch(ElementNotAvailableException){return;}
  await Task.Delay(100);
 }
 throw new TimeoutException("Native picker was not completed by the controlled UI driver");
}var edit=dialog.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit),new PropertyCondition(AutomationElement.AutomationIdProperty,"1001")));if(edit is null)
{
 var controls=dialog.FindAll(TreeScope.Descendants,System.Windows.Automation.Condition.TrueCondition).Cast<AutomationElement>().Where(x=>x.Current.ControlType==ControlType.Edit||x.Current.ControlType==ControlType.ComboBox).ToArray();
 await File.WriteAllTextAsync(Path.Combine(Root,folder?"NATIVE_FOLDER_FIELDS.json":"NATIVE_FILE_FIELDS.json"),JsonSerializer.Serialize(controls.Select(x=>new{type=x.Current.ControlType.ProgrammaticName,id=x.Current.AutomationId,name=x.Current.Name,valuePattern=x.TryGetCurrentPattern(ValuePattern.Pattern,out _)})));
 edit=controls.FirstOrDefault(x=>x.Current.ControlType==ControlType.Edit&&(x.Current.Name.Contains("文件名")||folder&&x.Current.Name.Contains("文件夹"))&&x.TryGetCurrentPattern(ValuePattern.Pattern,out _));
 edit??=controls.FirstOrDefault(x=>(x.Current.Name.Contains("文件名")||folder&&x.Current.Name.Contains("文件夹"))&&x.TryGetCurrentPattern(ValuePattern.Pattern,out _));
}
Assert.IsNotNull(edit,"Native file/folder entry");await File.WriteAllTextAsync(Path.Combine(Root,folder?"FOLDER_ACTION_BEFORE.json":"FILE_ACTION_BEFORE.json"),JsonSerializer.Serialize(new{id=edit.Current.AutomationId,enabled=edit.Current.IsEnabled,offscreen=edit.Current.IsOffscreen,readOnly=((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).Current.IsReadOnly}));edit.SetFocus();((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).SetValue(file);await Task.Delay(150);var choose=dialog.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),new PropertyCondition(AutomationElement.AutomationIdProperty,"1")));Assert.IsNotNull(choose,"Native confirm file/folder");await File.WriteAllTextAsync(Path.Combine(Root,folder?"FOLDER_CONFIRM_STATE.json":"FILE_CONFIRM_STATE.json"),JsonSerializer.Serialize(new{name=choose.Current.Name,id=choose.Current.AutomationId,enabled=choose.Current.IsEnabled}));while(!choose.Current.IsEnabled&&DateTime.UtcNow<until)await Task.Delay(50);choose.SetFocus();await Task.Delay(100);var dialogHandle=new IntPtr(dialog.Current.NativeWindowHandle);Assert.AreNotEqual(IntPtr.Zero,dialogHandle,"Actual native picker HWND");var buttonHandle=HttpNativeDialogs.GetDlgItem(dialogHandle,1);await File.WriteAllTextAsync(Path.Combine(Root,folder?"FOLDER_NATIVE_CONFIRM.json":"FILE_NATIVE_CONFIRM.json"),JsonSerializer.Serialize(new{dialogHandle=dialogHandle.ToInt64(),buttonHandle=buttonHandle.ToInt64(),commandId=1,action=folder?"Native BM_CLICK on observed folder-confirm button after UIA path entry":"Native WM_COMMAND IDOK after UIA filename entry; no ViewModel import injection"}));Assert.IsTrue(folder?HttpNativeDialogs.PostMessage(buttonHandle,0x00F5,IntPtr.Zero,IntPtr.Zero):HttpNativeDialogs.PostMessage(dialogHandle,0x0111,new IntPtr(1),buttonHandle),"Native picker button confirmation");});
 }
 private static async Task HttpOpen(MainWindow window,MainViewModel main)
 {await NativeInvoke(window,"PrimaryNavigationOnlineSelection");await Wait(()=>main.IsOnlineSelectionPage,"Online selection visible");await main.OnlineSelectionPage.RefreshAsync();}
 private static async Task HttpOpenExisting(MainWindow window,MainViewModel main)
 {await HttpOpen(window,main);await Wait(()=>main.OnlineSelectionPage.Projects.Count==1,"One saved desktop project");await NativeInvoke(window,"OnlineOpenProject");await Wait(()=>main.OnlineSelectionPage.ProjectPage.Project is not null,"Native open selected project");}
 [TestMethod]public void HttpPhase1OfflineDraftAndProxyPersist()=>Run(async(window,main)=>
 {
  Directory.CreateDirectory(Root);var source=Path.Combine(Root,"synthetic-network.png");var bytes=new byte[96*72*4];for(int i=0;i<96*72;i++){bytes[i*4]=50;bytes[i*4+1]=150;bytes[i*4+2]=220;bytes[i*4+3]=255;}var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(96,72,96,96,PixelFormats.Bgra32,null,bytes,96*4)));using(var f=File.Create(source))png.Save(f);
  await HttpOpen(window,main);Assert.IsInstanceOfType<HttpOnlineSelectionProvider>(main.OnlineSelectionPage.Provider);await NativeInvoke(window,"OnlineCreateProject");await Wait(()=>main.OnlineSelectionPage.IsCreateModalOpen,"Visible create modal opened");await HttpField(window,"OnlineProjectName","HTTP 合成图闭环");await HttpField(window,"OnlineClientName","本机测试客户");await HttpField(window,"OnlineTargetCount","1");
  await Wait(()=>main.OnlineSelectionPage.ProjectName=="HTTP 合成图闭环"&&main.OnlineSelectionPage.ClientName=="本机测试客户"&&main.OnlineSelectionPage.TargetCountText=="1","Native fields update create bindings");Capture(window,"OFFSCREEN-http-before-import.png");await File.WriteAllTextAsync(Path.Combine(Root,"CREATE_ENTRY_STATE.json"),JsonSerializer.Serialize(new{main.OnlineSelectionPage.ProjectName,main.OnlineSelectionPage.ClientName,main.OnlineSelectionPage.TargetCountText,main.OnlineSelectionPage.StatusText,canCreate=main.OnlineSelectionPage.CreateAndImportCommand.CanExecute(null)}));
  var invoke=NativeInvoke(window,"OnlineCreateImport");await HttpFileDialog(window,"选择在线选片照片",source);await invoke;var page=main.OnlineSelectionPage.ProjectPage;await Wait(()=>!main.OnlineSelectionPage.IsBusy&&page.Assets.Count==1&&page.Assets[0].ProxyJpegPath is not null,"Local proxy completed while service offline");var id=page.Project!.Id;Assert.IsTrue(string.IsNullOrWhiteSpace(page.Assets[0].CloudAssetId));Assert.IsTrue(page.Assets[0].StatusText.Contains("未上传"));
  await NativeInvoke(window,"OnlineUploadProxies");await Wait(()=>!page.IsBusy&&page.StatusText.Contains("未连接"),"Actual offline request reports retry");Assert.AreEqual(id,page.Project.Id);Assert.AreEqual(0,page.CloudConfirmedCount);
  await File.WriteAllTextAsync(Path.Combine(Root,"HTTP_OFFLINE.json"),JsonSerializer.Serialize(new{processId=Environment.ProcessId,projectId=id,assetId=page.Assets[0].Id,source,sourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source))),proxy=page.Assets[0].ProxyJpegPath,localProxyReady=true,cloudConfirmed=0,status=page.StatusText}));Capture(window,"OFFSCREEN-http-offline.png");
 });
 [TestMethod]public void HttpPhase2RestartUploadPublishAndCopyLink()=>Run(async(window,main)=>
 {
  var old=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root,"HTTP_OFFLINE.json"))).RootElement;await HttpOpenExisting(window,main);var page=main.OnlineSelectionPage.ProjectPage;Assert.AreEqual(old.GetProperty("projectId").GetGuid(),page.Project!.Id);Assert.AreEqual(old.GetProperty("assetId").GetGuid(),page.Assets[0].Id);Assert.AreNotEqual(old.GetProperty("processId").GetInt32(),Environment.ProcessId);
  await NativeInvoke(window,"OnlineUploadProxies");await Wait(()=>!page.IsBusy&&page.CloudConfirmedCount==1,"Service confirmed proxy upload");Assert.IsTrue(page.Assets[0].StatusText.Contains("确认上传"));var asset=page.Assets[0].ToModel();await NativeInvoke(window,"OnlineUploadProxies");await Wait(()=>!page.IsBusy,"Idempotent repeat upload entry");Assert.AreEqual(asset.Id,page.Assets[0].Id);Assert.AreEqual(asset.CloudAssetId,page.Assets[0].CloudAssetId);
  await NativeInvoke(window,"OnlinePublish");await Wait(()=>!page.IsBusy&&page.PublishedLink is not null,"Service signed protected link");await NativeInvoke(window,"OnlineCopyLink");await Wait(()=>page.StatusText.Contains("已复制"),"Native copied link");var link=Clipboard.GetText();Assert.AreEqual(page.PublishedLink,link);await File.WriteAllTextAsync(Path.Combine(Root,"SHARE_LINK.secret.txt"),link);
  await File.WriteAllTextAsync(Path.Combine(Root,"HTTP_PUBLISHED.json"),JsonSerializer.Serialize(new{processId=Environment.ProcessId,projectId=page.Project.Id,publicId=page.Project.PublicId,assetId=asset.Id,cloudId=asset.CloudAssetId,proxySha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(asset.ProxyJpegPath!))),protectedLinkSha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(link))),rules=page.Rule,uploaded=page.CloudConfirmedCount}));Capture(window,"OFFSCREEN-http-published.png");
 });
 [TestMethod]public void HttpPhase3RefreshConfirmedResultAndSyncWorkflow()=>Run(async(window,main)=>
 {
  await HttpOpenExisting(window,main);var page=main.OnlineSelectionPage.ProjectPage;await NativeInvoke(window,"OnlineRefreshResults");await Wait(()=>!page.IsBusy&&page.FinalResult is {IsLocked:true},"Actual GET confirmed snapshot");Assert.AreEqual(1,page.SelectedCount);Assert.AreEqual(1,page.FinalResult!.SelectionVersion);Assert.IsGreaterThan(0L,page.FinalResult.Revision);Assert.AreEqual(page.Project!.Id,page.FinalResult.SelectionProjectId);
  await NativeButton(window,"交付结果");var archive=Path.Combine(Root,"archive");Directory.CreateDirectory(archive);var invoke=NativeInvoke(window,"OnlineSyncWorkflow");await HttpFileDialog(window,"选择选片结果归档目录",archive,true);await invoke;try{await Wait(()=>!page.IsBusy&&main.IsWorkflowPage&&page.Project.WorkflowProjectId is not null,"Native sync returns linked workflow");}catch{await File.WriteAllTextAsync(Path.Combine(Root,"SYNC_FAILURE_STATE.json"),JsonSerializer.Serialize(new{page.IsBusy,page.StatusText,main.CurrentPage,workflowId=page.Project.WorkflowProjectId,hasHandoff=page.WorkflowHandoff is not null}));throw;}Assert.AreEqual("HTTP 合成图闭环",main.ProjectName);Assert.HasCount(1,main.Selections);var workflow=main.ProjectHistory.Single(x=>x.Id==page.Project.WorkflowProjectId);Assert.HasCount(1,workflow.SelectionInputs);
  await File.WriteAllTextAsync(Path.Combine(Root,"HTTP_SYNC.json"),JsonSerializer.Serialize(new{processId=Environment.ProcessId,projectId=page.Project.Id,workflowId=page.Project.WorkflowProjectId,selectionVersion=page.FinalResult.SelectionVersion,revision=page.FinalResult.Revision,items=page.FinalResult.Items,archive,workflowInputs=workflow.SelectionInputs,sourceDirectories=workflow.SourceDirectories}));Capture(window,"OFFSCREEN-http-workflow.png");
 });
 [TestMethod]public void HttpPhase4ReopenPersistsConfirmedResultAndRevoke()=>Run(async(window,main)=>
 {
  var prior=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root,"HTTP_SYNC.json"))).RootElement;await HttpOpenExisting(window,main);var page=main.OnlineSelectionPage.ProjectPage;Assert.AreNotEqual(prior.GetProperty("processId").GetInt32(),Environment.ProcessId);Assert.AreEqual(prior.GetProperty("projectId").GetGuid(),page.Project!.Id);Assert.AreEqual(prior.GetProperty("workflowId").GetGuid(),page.Project.WorkflowProjectId);Assert.IsTrue(page.FinalResult!.IsLocked);Assert.AreEqual(prior.GetProperty("revision").GetInt64(),page.FinalResult.Revision);Assert.AreEqual(1,page.SelectedCount);Assert.HasCount(1,page.FinalResult.Items);
  await File.WriteAllTextAsync(Path.Combine(Root,"HTTP_REOPEN.json"),JsonSerializer.Serialize(new{processId=Environment.ProcessId,projectId=page.Project.Id,workflowId=page.Project.WorkflowProjectId,resultVersion=page.FinalResult.SelectionVersion,revision=page.FinalResult.Revision,selected=page.SelectedCount,persistedWithoutHttpRefresh=true}));
  await NativeInvoke(window,"OnlineUnpublish");await Wait(()=>!page.IsBusy&&page.PublishedLink is null&&page.StatusText.Contains("撤销"),"Native revoke signed link");Capture(window,"OFFSCREEN-http-reopened.png");
 });
}

internal static class HttpNativeDialogs{[DllImport("user32.dll",SetLastError=true)]internal static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr w,IntPtr l);[DllImport("user32.dll")]internal static extern IntPtr GetDlgItem(IntPtr hwnd,int id);}
