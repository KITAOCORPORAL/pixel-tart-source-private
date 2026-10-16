using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
internal static class SonyWorkspaceAudit
{
 public static async Task Run(string copy,string output)
 {
  if(Directory.Exists(output)||SonyProbe.Hash(copy)!=SonyProbe.AuthorizedSha)throw new InvalidDataException("Exact authorized Sony and fresh isolated root required");Directory.CreateDirectory(output);
  if(Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME")!="1")throw new InvalidOperationException("Isolated app data required before constructing workspace");
  var rows=new List<object>();string status="FAILED";var versions=new StudioVersionRepository(Path.Combine(output,"versions.private"));var session=Path.Combine(output,"session.private.ptstudio.json");
  byte[] Pixels(BitmapSource image){var stride=(image.PixelWidth*image.Format.BitsPerPixel+7)/8;var pixels=new byte[stride*image.PixelHeight];image.CopyPixels(pixels,stride,0);return pixels;}
  try
  {
   using(var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(output),versionRepository:versions))
   {
    await workspace.LoadTargetAsync(copy);if(workspace.ActiveTarget is null||workspace.ActiveTarget.Thumbnail is null)throw new InvalidDataException("Real workspace thumbnail missing");
    rows.Add(new{operation="real-workspace-legacy-load-thumbnail",status="PASSED",workspace.ActiveTarget.PixelWidth,workspace.ActiveTarget.PixelHeight});
    await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);if(!workspace.IsProfessionalRaw||workspace.Editor.MatchedImage is null)throw new InvalidDataException("PV4 workspace failed: "+workspace.StatusText);
    workspace.RawUseCameraWhiteBalance=false;workspace.RawRedGain=1.05;workspace.RawBlueGain=.95;workspace.RawExposureEv=.5;
    await workspace.ApplyProfessionalRawCommand.ExecuteAsync(null);if(workspace.ActiveTarget.RawDefinition?.ExposureEv!=.5)throw new InvalidDataException("Nondefault workspace definition not applied");
    await workspace.UpdateProfessionalViewportAsync(new Rect(100,100,128,128),1,1);
    rows.Add(new{operation="workspace-nondefault-and-native-detail",status="PASSED",workspace.RawPipelineStatus,workspace.ActiveTarget.RawDefinition});
    var before=Pixels(workspace.Editor.MatchedImage!);await workspace.SaveSessionAsync(session);
    using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(output),versionRepository:versions);await restored.LoadSessionAsync(session);
    if(!restored.IsProfessionalRaw||restored.Editor.MatchedImage is null||!before.SequenceEqual(Pixels(restored.Editor.MatchedImage)))throw new InvalidDataException("Real workspace reopened preview mismatch");
    rows.Add(new{operation="workspace-save-reopen-preview-exact",status="PASSED",previewSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(before))});
    await restored.UpdateProfessionalViewportAsync(new Rect(0,0,128,128),1,1);restored.StopProcessingCommand.Execute(null);await restored.ProfessionalDetailWork;
    rows.Add(new{operation="workspace-stop-command",status="EXECUTED_AFTER_DETAIL",limitation="Not claimed as in-flight native decode or real foreground switch cancellation"});
   }
   if(SonyProbe.Hash(copy)!=SonyProbe.AuthorizedSha)throw new InvalidDataException("Copy changed");status="PASSED_REAL_VIEWMODEL_NOT_DESKTOP";
  }
  catch(Exception error){rows.Add(new{status="FAILED",error=error.ToString()});}
  File.WriteAllText(Path.Combine(output,"SONY_WORKSPACE.json"),JsonSerializer.Serialize(new{status,sourceSha256=SonyProbe.AuthorizedSha,rows,desktopStartup="NOT_RUN",physicalDpi="NOT_RUN",noWindowCreated=true,backendOnly=true},new JsonSerializerOptions{WriteIndented=true}));
 }
 private sealed class Dialogs(string root):IDialogService
 {
  public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string message){}public void ShowError(string message){}public void ShowFeedback(){}public bool Confirm(string message,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
 }
}
