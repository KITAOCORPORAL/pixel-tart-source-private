using System.IO;using System.Security.Cryptography;using System.Text.Json;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Xml.Linq;using System.Windows.Threading;
using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Views;
internal static class PhotographerTrialAudit
{
 public static void Run(string manifest,string output)
 {
  output=Path.GetFullPath(output);
  if(Directory.Exists(output))throw new IOException("Fresh delivery evidence directory required");Directory.CreateDirectory(output);Exception? error=null;
  var thread=new Thread(()=>{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var root=Directory.GetCurrentDirectory();var doc=XDocument.Load(Path.Combine(root,"src/RAWSelectionAssistant/App.xaml"));foreach(var item in doc.Descendants().Attributes("Source"))app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+item.Value,UriKind.Relative)));app.Dispatcher.InvokeAsync(async()=>{try{await Execute(manifest,output);}catch(Exception e){error=e;}finally{app.Shutdown();}});app.Run();});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error is not null)throw new InvalidOperationException("Photographer trial audit failed",error);
 }
 private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
 private static void Require(bool ok,string m){if(!ok)throw new InvalidDataException(m);}
 private static void Png(BitmapSource image,string path){var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(image));using var f=File.Create(path);e.Save(f);}
 private static void Screenshot(FrameworkElement view,string path,int width=1600,int height=1000){view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();var b=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);b.Render(view);Png(b,path);}
 private static BitmapSource Contact(IReadOnlyList<(string Label,BitmapSource Image)> images,int columns,string title)
 {
  int cellW=360,cellH=315,header=64;var rows=(images.Count+columns-1)/columns;var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
  {dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(239,237,230)),null,new Rect(0,0,columns*cellW,header+rows*cellH));Text(dc,title,18,20,18);for(int i=0;i<images.Count;i++){int x=(i%columns)*cellW,y=header+(i/columns)*cellH;var item=images[i];double scale=Math.Min((cellW-24d)/item.Image.PixelWidth,245d/item.Image.PixelHeight);double w=item.Image.PixelWidth*scale,h=item.Image.PixelHeight*scale;dc.DrawImage(item.Image,new Rect(x+(cellW-w)/2,y+8+(245-h)/2,w,h));Text(dc,item.Label,x+12,y+264,13);}}
  var bitmap=new RenderTargetBitmap(columns*cellW,header+rows*cellH,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  static void Text(DrawingContext dc,string value,double x,double y,double size){var t=new FormattedText(value,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),size,Brushes.Black,1){MaxTextWidth=1400};dc.DrawText(t,new Point(x,y));}
 }
 private static async Task Execute(string manifest,string output)
 {
  var inputs=JsonSerializer.Deserialize<InstantReviewInput[]>(File.ReadAllText(manifest))!;var records=new List<object>();var pairs=new List<object>();var inputRows=new List<object>();var derived=new List<string>();
  var screen=Path.Combine(output,"screenshots");var sources=Path.Combine(output,"inputs");Directory.CreateDirectory(screen);Directory.CreateDirectory(sources);
  foreach(var item in inputs)
  {
   Require(Hash(item.Original)==item.ExpectedOriginalSha256,"Original source hash");Require(Hash(item.FrozenTiff)==item.ExpectedFrozenSha256,"Frozen source hash");
   var prepared=StudioQuickExport.Load(item.FrozenTiff,800);var preparedPath=Path.Combine(sources,item.Id+".png");StudioQuickExport.Encode(prepared,item.FrozenTiff,preparedPath,StudioExportFormat.Png);derived.Add(preparedPath);
  }
  foreach(var item in inputs)
  {
   Require(Hash(item.Original)==item.ExpectedOriginalSha256,"Original source hash");Require(Hash(item.FrozenTiff)==item.ExpectedFrozenSha256,"Frozen source hash");
   var source=StudioQuickExport.Load(item.FrozenTiff,800);var path=Path.Combine(sources,item.Id+".png");
   var originalHash=Hash(item.Original);var frozenHash=Hash(item.FrozenTiff);var derivedHash=Hash(path);inputRows.Add(new{id=item.Id,originalHash,frozenHash,derivedHash,width=source.PixelWidth,height=source.PixelHeight,resize="WIC existing maximum-edge800, display sRGB, review derivative not original-resolution claim"});
   Console.WriteLine("Color review "+item.Id);var colorDir=Path.Combine(output,"review","01-color",item.Id);Directory.CreateDirectory(colorDir);var cards=new List<(string,BitmapSource)>{("原始输入（同一裁切）",source)};Png(source,Path.Combine(colorDir,"00-input.png"));
   foreach(var choice in new[]{("itype-color","经典彩色 · i-Type／600共用"),("itype-bw","柔调黑白 · 三型号共用"),("sx70-color","复古暖彩 · SX-70"),("reclaimed-blue600","浅蓝单色"),("blue-duochrome600","深蓝双色")})
   {
    using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(output,item.Id==inputs[0].Id?derived[1]:derived[0]));await vm.LoadTargetAsync(path);vm.InstantEngineVersion="V3";vm.InstantInputMode="Original";vm.ResetPhotoV3DraftCommand.Execute(null);vm.SelectedPhotographerPaperId=choice.Item1;vm.InstantPaperFrame=false;vm.InstantAmount=1;vm.InstantV3Seed=20261018;
    vm.InstantV3Texture=0;vm.InstantUneven=0;vm.InstantV3EmulsionDefects=0;vm.InstantV3Aging=0;vm.InstantV3Softness=0;vm.InstantV3Diffusion=0;vm.InstantV3PaperTexture=0;vm.InstantV3PaperScratches=0;vm.InstantV3ScanDust=0;
    vm.InstantLayout="LegacyFree"; // FreezeV3 uses square geometry when null: set reviewed geometry via exact frozen definition below, no algorithm change.
    await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var current=vm.ActiveTarget!.RawDefinition??throw new InvalidDataException(vm.StatusText);var recipe=current.InstantFilm!.V3!;
    recipe=ReferenceColorWorkspaceViewModel.CleanColorPreview(recipe,source.PixelWidth/(double)source.PixelHeight);var definition=current with{InstantFilm=current.InstantFilm with{V3=recipe}};
    var master=vm.ActiveTarget.LinearRawMaster!;using var pixels=ProfessionalRawPipeline.RenderPreview(master,definition,800);var bitmap=RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24());var result=Path.Combine(colorDir,choice.Item1+".png");Png(bitmap,result);cards.Add((choice.Item2,bitmap));
    var row=new{photo=item.Id,group="ColorOnly",sourceSha256=derivedHash,originalHash,frozenHash,definition,seed=recipe.Random.RootSeed,width=pixels.Width,height=pixels.Height,resultSha256=Hash(result),file=result,scope="Existing color response; optics/grain/paper/defects off, same source/aspect; creative simulation not calibration"};records.Add(row);File.WriteAllText(result+".json",JsonSerializer.Serialize(row,new JsonSerializerOptions{WriteIndented=true}));
   }
   Png(Contact(cards,3,item.Id+"｜纯色彩对比 · 同源／无纸框装饰 · 创作模拟"),Path.Combine(output,"review",item.Id+"-color-contact.png"));
   Console.WriteLine("Appearance review "+item.Id);var lookDir=Path.Combine(output,"review","02-appearance",item.Id);Directory.CreateDirectory(lookDir);var looks=new List<(string,BitmapSource)>();
   for(int n=0;n<4;n++)
   {
    using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(output,item.Id==inputs[0].Id?derived[1]:derived[0]));await vm.LoadTargetAsync(path);vm.ResetPhotoV3DraftCommand.Execute(null);vm.SelectedPhotographerPaperId="itype-color";vm.InstantInputMode="Original";vm.InstantV3Seed=20261018;vm.InstantPaperFrame=true;vm.InstantV3Texture=n==0?0:.012;vm.InstantV3PaperTexture=n==0?0:.015;
    if(n==0)vm.InstantImageEffectsDisabled=true;
    if(n==2){vm.InstantV3DefectKind="Bands";vm.InstantV3DefectAmount=.32;vm.InstantV3Leak=.08;}
    if(n==3){await vm.SelectSecondExposureCommand.ExecuteAsync(null);vm.InstantSecondWeight=.35;vm.InstantSecondCropX=.3;vm.InstantSecondCropY=.6;vm.InstantSecondZoom=1.15;}
    await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var def=vm.ActiveTarget!.RawDefinition??throw new InvalidDataException(vm.StatusText);using var pixels=ProfessionalRawPipeline.RenderPreview(vm.ActiveTarget.LinearRawMaster!,def,1000);var bitmap=RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24());var name=new[]{"00-base","01-paper-grain","02-defects","03-double"}[n];var label=new[]{"无成像效果 · 同纸框基准","纸框＋颗粒＋纸面","乳剂条带＋轻漏光","第二曝光 · 权重35%"}[n];var result=Path.Combine(lookDir,name+".png");Png(bitmap,result);looks.Add((label,bitmap));var row=new{photo=item.Id,group="CompleteAppearance",sourceSha256=derivedHash,definition=def,seed=def.InstantFilm!.V3!.Random.RootSeed,width=pixels.Width,height=pixels.Height,file=result,resultSha256=Hash(result),scope="Actual product pipeline, same square crop/frame per group; only listed parameters differ; creative simulation"};records.Add(row);File.WriteAllText(result+".json",JsonSerializer.Serialize(row,new JsonSerializerOptions{WriteIndented=true}));
   }
   Png(Contact(looks,4,item.Id+"｜完整外观 · 同构图／同纸框 · 颗粒／缺陷／双重曝光"),Path.Combine(output,"review",item.Id+"-appearance-contact.png"));
  }
  Console.WriteLine("Page screenshots and one export");using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(Path.Combine(output,"export"),derived[0]));Directory.CreateDirectory(Path.Combine(output,"export"));await workspace.LoadTargetAsync(derived[1]);workspace.UsePhotoGradePage=true;
  var view=new PhotoGradePageView{DataContext=workspace};view.Measure(new Size(1600,1000));view.Arrange(new Rect(0,0,1600,1000));view.UpdateLayout();workspace.Editor.Exposure=.18;await Task.Delay(400);Screenshot(view,Path.Combine(screen,"01-basic.png"));
  ((TabControl)view.FindName("EditModes")).SelectedIndex=1;workspace.Editor.AddAdjustmentNodeCommand.Execute("Curve");workspace.Editor.SetCurvePoints("rgb",[new(0,0),new(.5,.56),new(1,1)]);await Task.Delay(400);Screenshot(view,Path.Combine(screen,"02-curves.png"));
  ((TabControl)view.FindName("EditModes")).SelectedIndex=2;workspace.InstantInputMode="Original";var panel=(InstantV3Editor)view.FindName("V3Editor");await workspace.PreviewPhotographerPapersCommand.ExecuteAsync(null);view.UpdateLayout();Screenshot(view,Path.Combine(screen,"03-paper-selection.png"));
  var grain=(Expander)panel.FindName("GrainAdvanced");grain.IsExpanded=true;grain.BringIntoView();await Dispatcher.Yield(DispatcherPriority.Loaded);Screenshot(view,Path.Combine(screen,"04-advanced.png"));
  grain.IsExpanded=false;var doubleSection=(Expander)panel.FindName("MultipleAdvanced");doubleSection.IsExpanded=true;await workspace.SelectSecondExposureCommand.ExecuteAsync(null);workspace.InstantSecondWeight=.35;doubleSection.BringIntoView();await Dispatcher.Yield(DispatcherPriority.Loaded);Screenshot(view,Path.Combine(screen,"05-double-exposure.png"));
  doubleSection.IsExpanded=false;((ScrollViewer)view.FindName("PaperScroll")).ScrollToTop();await workspace.GenerateInstantFilmCommand.ExecuteAsync(null);var firstId=workspace.ActiveTarget!.ActiveEditVersionId;await workspace.PhotoGradeUndoCommand.ExecuteAsync(null);await workspace.PhotoGradeRedoCommand.ExecuteAsync(null);Require(firstId==workspace.ActiveTarget.ActiveEditVersionId,"Undo/redo actual version");
  var project=Path.Combine(output,"example.ptstudio.json");await workspace.SaveSessionAsync(project);Screenshot(view,Path.Combine(screen,"06-history.png"));
  using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(Path.Combine(output,"export"),derived[0]));await reopened.LoadSessionAsync(project);Require(JsonSerializer.Serialize(reopened.ActiveTarget!.RawDefinition)==JsonSerializer.Serialize(workspace.ActiveTarget.RawDefinition),"Saved definition equality");reopened.QuickExportFormat=StudioExportFormat.Png;await reopened.ExportSelectedCommand.ExecuteAsync(null);Require(reopened.ActiveTarget.ExportStatus==ReferenceExportStatus.Succeeded,reopened.StatusText);Require(reopened.ActiveTarget.LastProfessionalOutput is not null,"Independent output inspection preserved");
  ((TabControl)view.FindName("EditModes")).SelectedIndex=0;Screenshot(view,Path.Combine(screen,"08-mode-unavailable.png"));((TabControl)view.FindName("EditModes")).SelectedIndex=2;
  var exportMenu=(ContextMenu)typeof(StudioExportMenu).GetMethod("CreateMenu",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,[workspace])!;Screenshot(exportMenu,Path.Combine(screen,"07-export-menu.png"),480,340);
  File.WriteAllText(Path.Combine(output,"REVIEW_INDEX.json"),JsonSerializer.Serialize(new{inputs=inputRows,records,pageScreenshots="Real WPF RenderTargetBitmap; no physical mouse/foreground claim",exportPath=reopened.ActiveTarget.OutputPath,exportInspection=reopened.ActiveTarget.LastProfessionalOutput,sourceSafe=inputs.All(x=>Hash(x.Original)==x.ExpectedOriginalSha256),calibrationEligible=false},new JsonSerializerOptions{WriteIndented=true}));
 }
 private sealed class Dialogs(string output,string second):IDialogService
 {public string? ChooseFolder(string title,string? initialDirectory=null)=>output;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[second];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}
