using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
class Program
{
 static string output="";static readonly List<object> records=[];
 static void Check(bool value,string message){if(!value)throw new InvalidDataException(message);}
 static void Record(string stage,ReferenceColorWorkspaceViewModel vm){using var p=Process.GetCurrentProcess();p.Refresh();records.Add(new{stage,privateBytes=p.PrivateMemorySize64,workingSet=p.WorkingSet64,managed=GC.GetTotalMemory(false),naturalGen2=GC.CollectionCount(2),exposure=vm.Editor.AdjustmentStack.Nodes.FirstOrDefault(n=>n.Type==ColorStudioNodeType.BasicTone)?.NumericParameters.GetValueOrDefault("exposure"),analysis=vm.AnalysisLabel});File.WriteAllText(Path.Combine(output,"STAGES.json"),JsonSerializer.Serialize(records,new JsonSerializerOptions{WriteIndented=true}));}
 static string Digest(BitmapSource image){using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);int stride=(image.PixelWidth*image.Format.BitsPerPixel+7)/8;var row=new byte[stride];for(int y=0;y<image.PixelHeight;y++){image.CopyPixels(new Int32Rect(0,y,image.PixelWidth,1),row,stride,0);digest.AppendData(row);}return Convert.ToHexString(digest.GetHashAndReset());}
 static async Task Settled(ReferenceColorWorkspaceViewModel vm){var watch=Stopwatch.StartNew();while(!vm.Editor.IsSettled){if(watch.Elapsed.TotalSeconds>90)throw new TimeoutException("Render did not settle: "+vm.Editor.StatusText);await Task.Delay(50);}await vm.AnalysisWork;Check(!vm.Editor.HasError,vm.Editor.StatusText);}
 [STAThread]static int Main(string[] args)
 {
 output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var repo=Environment.GetEnvironmentVariable("KITAO_PROBE_REPO")??throw new InvalidOperationException("Explicit repository required");foreach(var item in XDocument.Load(Path.Combine(repo,"src/RAWSelectionAssistant/App.xaml")).Descendants().Attributes("Source"))application.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+item.Value,UriKind.Relative)));int code=0;
 application.Startup+=async(_,_)=>{try{await Run();}catch(Exception e){code=3;File.WriteAllText(Path.Combine(output,"FAILURE.txt"),e.ToString());}finally{File.WriteAllText(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(new{status=code==0?"PASS":"FAIL",source=typeof(ReferenceColorWorkspaceViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,authority="Controlled production ViewModel host, no ordinary desktop acceptance",forcedGc=false,newRawDecodes=0,records},new JsonSerializerOptions{WriteIndented=true}));application.Shutdown(code);}};application.Run();return code;
 }
 static async Task Run()
 {
 var recovery=Environment.GetEnvironmentVariable("KITAO_PROBE_RECOVERY")??throw new InvalidOperationException("Recovery file required");var original=File.ReadAllBytes(recovery);var session=Path.Combine(output,"probe-input.ptstudio.json");File.WriteAllBytes(session,original);
 var dialogs=new Dialogs(session);using(var vm=new ReferenceColorWorkspaceViewModel(dialogs))
 {
 await vm.OpenSessionCommand.ExecuteAsync(null);await Settled(vm);Check(vm.ActiveTarget is not null&&vm.Editor.SourceImage?.PixelWidth==7752,"Authorized recovery did not load 40MP");var node=ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(x=>x.Key=="exposure");var reference=Digest(vm.Editor.MatchedImage!);Record("loaded-exposure01",vm);
 foreach(var ev in new[]{.2,.3,.1}){vm.Editor.SetToolParameter(node,ev);await Settled(vm);Record("edit-"+ev,vm);}Check(Digest(vm.Editor.MatchedImage!)==reference,"Same exposure no longer reproduces exact full frame");
 await vm.PhotoGradeUndoCommand.ExecuteAsync(null);await Settled(vm);Record("undo",vm);await vm.PhotoGradeRedoCommand.ExecuteAsync(null);await Settled(vm);Record("redo",vm);Check(Digest(vm.Editor.MatchedImage!)==reference,"Redo pixels differ");await vm.SaveSessionAsync(session);Record("saved",vm);
 }
 using(var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(session))){await reopened.OpenSessionCommand.ExecuteAsync(null);await Settled(reopened);Record("independent-vm-reopen",reopened);Check(reopened.Editor.AdjustmentStack.Nodes.Single(n=>n.Type==ColorStudioNodeType.BasicTone).NumericParameters["exposure"]==.1,"Persisted exposure changed");}
 Check(File.ReadAllBytes(recovery).SequenceEqual(original),"Original recovery was changed");
 }
 sealed class Dialogs(string session):IDialogService
 {
 public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[session];public string? ChooseFolder(string title,string? initialDirectory=null)=>null;public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds)=>currentToolIds;public void ShowInfo(string message){}public void ShowError(string message)=>throw new InvalidDataException(message);public bool Confirm(string message,string title)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool showAdvancedDetails)=>false;public void RevealFile(string path){}
 }
}
