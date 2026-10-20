using System.IO;using System.Text.Json;using System.Security.Cryptography;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Xml.Linq;
using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Views;using RAWSelectionAssistant.Core.Services.Projects;
internal static class R3VisualAudit
{
 public static void Run(string inputs,string output)
 {
  inputs=Path.GetFullPath(inputs);output=Path.GetFullPath(output);if(Directory.Exists(output))throw new IOException("Fresh visual output required");Directory.CreateDirectory(output);Exception? error=null;
  var thread=new Thread(()=>{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var doc=XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(),"src/RAWSelectionAssistant/App.xaml"));foreach(var a in doc.Descendants().Attributes("Source"))app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+a.Value,UriKind.Relative)));app.Dispatcher.InvokeAsync(async()=>{try{await Execute(inputs,output);}catch(Exception e){error=e;}finally{app.Shutdown();}});app.Run();});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error is not null)throw new InvalidOperationException("R3 visual audit failed",error);
 }
 static void Shot(FrameworkElement view,string path,int w,int h)
 {view.Measure(new Size(w,h));view.Arrange(new Rect(0,0,w,h));view.UpdateLayout();var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bitmap));using var f=File.Create(path);enc.Save(f);}
 static string Pixels(BitmapSource image){var b=new FormatConvertedBitmap(image,PixelFormats.Rgb48,null,0);var bytes=new byte[b.PixelWidth*b.PixelHeight*6];b.CopyPixels(bytes,b.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(bytes));}
 static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
 static void Need(bool value,string message){if(!value)throw new InvalidDataException(message);}
 static async Task Execute(string inputs,string output)
 {
  var results=new List<object>();
  foreach(var size in new[]{(1920,1080),(1366,768)})
  {
   Console.WriteLine("R3 size "+size);var exportsFolder=Path.Combine(output,"export-"+size.Item1);Directory.CreateDirectory(exportsFolder);
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(exportsFolder));await vm.LoadTargetAsync(Path.Combine(inputs,"canon-tnan8886.png"));await vm.LoadTargetAsync(Path.Combine(inputs,"sony-dsc08015.png"));await vm.ActivateTargetCommand.ExecuteAsync(vm.Targets[0]);vm.UsePhotoGradePage=true;vm.InstantCameraAnimation=false;
   var page=new PhotoGradePageView{DataContext=vm};var tabs=(TabControl)page.FindName("EditModes");tabs.SelectedIndex=0;await vm.RefreshPreviewAnalysisAsync();Shot(page,Path.Combine(output,$"01-adjust-{size.Item1}.png"),size.Item1,size.Item2);
   tabs.SelectedIndex=1;await vm.PreviewPhotographerPapersCommand.ExecuteAsync(null);Shot(page,Path.Combine(output,$"02-paper-{size.Item1}.png"),size.Item1,size.Item2);
   vm.SelectedPhotographerPaperId="itype-color";vm.RequestLightPrintPreview();await vm.LightPrintPreviewWork;await vm.AnalysisWork;Shot(page,Path.Combine(output,$"03-preview-{size.Item1}.png"),size.Item1,size.Item2);
   vm.SelectedLightPrintFrameId="generic.white.circle";await vm.LightPrintPreviewWork;await vm.AnalysisWork;((ScrollViewer)page.FindName("PaperScroll")).ScrollToVerticalOffset(300);Shot(page,Path.Combine(output,$"04-frame-{size.Item1}.png"),size.Item1,size.Item2);
   await vm.DevelopLightPrintCommand.ExecuteAsync(null);Need(vm.IsLightPrintLocked,vm.StatusText);var variant=vm.ActiveTarget!.ActiveEditVersionId;Shot(page,Path.Combine(output,$"05-locked-{size.Item1}.png"),size.Item1,size.Item2);
   tabs.SelectedIndex=4;Shot(page,Path.Combine(output,$"06-variants-{size.Item1}.png"),size.Item1,size.Item2);
   var dialog=new DarkroomExportDialog(vm);
   var body=(FrameworkElement)dialog.Content;body.DataContext=vm;dialog.Content=null;
   var composite=new Grid();composite.Children.Add(new System.Windows.Shapes.Rectangle{Fill=new ImageBrush(BitmapFrame.Create(new Uri(Path.GetFullPath(Path.Combine(output,$"06-variants-{size.Item1}.png")))))});
   composite.Children.Add(new System.Windows.Shapes.Rectangle{Fill=new SolidColorBrush(Color.FromArgb(115,0,0,0))});
   composite.Children.Add(new Border{Width=640,Height=350,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Background=new SolidColorBrush(Color.FromRgb(29,29,32)),BorderBrush=new SolidColorBrush(Color.FromRgb(60,60,65)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Child=body});
   Shot(composite,Path.Combine(output,$"07-export-{size.Item1}.png"),size.Item1,size.Item2);dialog.Close();
   var project=Path.Combine(output,"example-"+size.Item1+".ptstudio.json");await vm.SaveSessionAsync(project);
   using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(exportsFolder));await reopened.LoadSessionAsync(project);Need(JsonSerializer.Serialize(reopened.ActiveTarget!.RawDefinition)==JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition),"Reopen definition mismatch");var formats=new List<object>();
   foreach(var format in new[]{StudioExportFormat.Png,StudioExportFormat.Tiff,StudioExportFormat.Jpeg}){reopened.QuickExportFormat=format;foreach(var t in reopened.Targets)t.IsSelected=ReferenceEquals(t,reopened.ActiveTarget);await reopened.ExportSelectedCommand.ExecuteAsync(null);Need(reopened.ActiveTarget.ExportStatus==ReferenceExportStatus.Succeeded,reopened.ActiveTarget.Error??reopened.StatusText);formats.Add(new{format,path=reopened.ActiveTarget.OutputPath,sha256=Hash(reopened.ActiveTarget.OutputPath!),inspection=reopened.ActiveTarget.LastProfessionalOutput});}
   results.Add(new{width=size.Item1,height=size.Item2,variant,sourceHash=Hash(Path.Combine(inputs,"canon-tnan8886.png")),definition=reopened.ActiveTarget.RawDefinition,exports=formats,scope="Real compiled WPF offscreen views, not native desktop screenshots; actual mouse NOT_RUN"});page.DataContext=null;
  }
  var workflows=new List<object>();
  foreach(var name in new[]{"canon-tnan8886.png","sony-dsc08015.png"})
  {
   var folder=Path.Combine(output,"two-photo",Path.GetFileNameWithoutExtension(name));Directory.CreateDirectory(folder);
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await vm.LoadTargetAsync(Path.Combine(inputs,name));vm.EnableLightPrintWorkspace();vm.InstantCameraAnimation=false;
   var original=Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!);vm.SelectedPhotographerPaperId="blue-duochrome600";await vm.LightPrintPreviewWork;var paper=Pixels(vm.Editor.MatchedImage!);Need(original!=paper,"Paper did not change "+name);
   vm.SelectedLightPrintFrameId="generic.white.circle";await vm.LightPrintPreviewWork;var framed=Pixels(vm.Editor.MatchedImage!);Need(framed!=paper,"Frame did not change "+name);
   vm.SelectedPhotographerPaperId="itype-bw";vm.SelectedPhotographerPaperId="sx70-color";vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;var ready=Pixels(vm.Editor.MatchedImage!);
   await vm.DevelopLightPrintCommand.ExecuteAsync(null);Need(ready==Pixels(vm.Editor.MatchedImage!),"Latest preview mismatch");Need(vm.LastInstantApplyTimings!.DemoMs<100,"Unexpected animation wait");var old=vm.ActiveTarget!.ActiveEditVersionId!.Value;var frozen=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);var oldPixels=Pixels(vm.Editor.MatchedImage!);
   vm.ReshootLightPrintCommand.Execute(null);vm.SelectedPhotographerPaperId="itype-bw";await vm.LightPrintPreviewWork;await vm.DevelopLightPrintCommand.ExecuteAsync(null);var newer=vm.ActiveTarget.ActiveEditVersionId;Need(newer!=old,"Missing variant");await vm.SwitchEditVersionAsync(old);Need(frozen==JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition),"Old variant mismatch");Need(oldPixels==Pixels(vm.Editor.MatchedImage!),"Old pixels mismatch");
   await vm.PhotoGradeUndoCommand.ExecuteAsync(null);await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Need(frozen==JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition),"Undo redo mismatch");
   var project=Path.Combine(folder,"example.ptstudio.json");await vm.SaveSessionAsync(project);using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(folder));await reopened.LoadSessionAsync(project);Need(frozen==JsonSerializer.Serialize(reopened.ActiveTarget!.RawDefinition),"Reopen mismatch");Need(oldPixels==Pixels(reopened.Editor.MatchedImage!),"Reopen pixels mismatch");
   var exports=new List<object>();foreach(var format in new[]{StudioExportFormat.Png,StudioExportFormat.Tiff,StudioExportFormat.Jpeg}){reopened.QuickExportFormat=format;await reopened.ExportSelectedCommand.ExecuteAsync(null);Need(reopened.ActiveTarget.ExportStatus==ReferenceExportStatus.Succeeded,reopened.StatusText);exports.Add(new{format,path=reopened.ActiveTarget.OutputPath,sha256=Hash(reopened.ActiveTarget.OutputPath!),inspection=reopened.ActiveTarget.LastProfessionalOutput});}
   workflows.Add(new{name,inputHash=Hash(Path.Combine(inputs,name)),paperChanges=true,frameChanges=true,latestWins=true,developExact=true,oldVariant=old,newVariant=newer,switchExact=true,undoRedo=true,reopenExact=true,animationMs=vm.LastInstantApplyTimings!.DemoMs,exports});
  }
  File.WriteAllText(Path.Combine(output,"TWO_PHOTO_RESULT.json"),JsonSerializer.Serialize(workflows,new JsonSerializerOptions{WriteIndented=true}));
  File.WriteAllText(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
  File.WriteAllText(Path.Combine(output,"ASSEMBLY_IDENTITY.json"),JsonSerializer.Serialize(new{wpfPath=typeof(PhotoGradePageView).Assembly.Location,wpfSha256=Hash(typeof(PhotoGradePageView).Assembly.Location),coreSha256=Hash(typeof(LightPrintFrames).Assembly.Location),source=StartupDiagnostics.ProductSourceSha,scope="Offscreen compiled published controls; export screenshot is labeled content overlay, not desktop capture"},new JsonSerializerOptions{WriteIndented=true}));
 }
 sealed class Dialogs(string output):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>output;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
