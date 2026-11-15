using System.IO;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class MainViewModel
{
 public async Task ResumeWorkflowProjectAsync(Guid projectId,string? studioFile=null)
 {
  var project=(await _projectHistoryService.LoadVisibleAsync()).SingleOrDefault(p=>p.Id==projectId)??throw new InvalidOperationException("指定项目不存在，未建立替代项目。");
  await ContinueProjectAsync(project);
  if(studioFile is not null)
  {
   var document=await Core.Services.Projects.ColorStudioSessionStore.LoadAsync(studioFile);
   if(document.WorkflowProjectId!=projectId)throw new InvalidDataException("工作文件不属于指定项目。");
   await ReferenceColorPage.LoadSessionAsync(studioFile);await ReferenceColorPage.AcceptContextAsync(projectId,null,null);NavigateToSurface("ReferenceColor");
  }
 }
 private async Task EditIndexedProjectAsync()
 {
  await SaveCurrentProjectAsync();
  var roots=Sources.Select(s=>Path.GetFullPath(s.Path).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar).ToArray();
  var photos=_mediaIndex.Files.Where(f=>roots.Any(root=>Path.GetFullPath(f.FullPath).StartsWith(root,StringComparison.OrdinalIgnoreCase))).Select(f=>f.FullPath).Where(p=>new[]{".jpg",".jpeg"}.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase)).Where(File.Exists).ToArray();
  if(photos.Length==0){StatusMessage="请先扫描来源目录，当前项目没有可编辑的索引 JPEG。";return;}
  if(photos.Length>100){StatusMessage="当前索引超过100张 JPEG，请分批编辑；本次未载入照片。";return;}
  if(ReferenceColorPage.Targets.Count>0&&ReferenceColorPage.WorkflowProjectId!=_currentProject.Id){StatusMessage="暗室已有其他批次，请先保存并关闭该批次后继续。";return;}
  ReferenceColorPage.BindWorkflowProject(_currentProject.Id);
  await ReferenceColorPage.AcceptContextAsync(_currentProject.Id,null,null);
  foreach(var path in photos) await ReferenceColorPage.LoadTargetAsync(path);
  NavigateToSurface("ReferenceColor");
 }
 private async Task PrepareCurrentProjectSelectionAsync(IReadOnlyList<string> paths)
 {
  if(ReferenceColorPage.WorkflowProjectId is not Guid workflowId||workflowId!=_currentProject.Id)throw new InvalidOperationException("暗室与当前归片项目身份不一致，请打开对应项目后继续。");
  if(paths.Count==0||paths.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=paths.Count)throw new InvalidOperationException("所选编辑结果存在同名冲突，请先分开处理，未创建相册。");
  await SaveCurrentProjectAsync();
  var folder=Path.Combine(AppDataPaths.OnlineSelectionDirectory,"Prepared",workflowId.ToString("N"),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  var durable=new List<string>();
  foreach(var source in paths){var destination=Path.Combine(folder,Path.GetFileName(source));File.Copy(source,destination,false);if(File.Exists(source+".kitao-export.json"))File.Copy(source+".kitao-export.json",destination+".kitao-export.json",false);durable.Add(destination);}
  OnlineSelectionPage.ProjectName=ProjectName;OnlineSelectionPage.ClientName="客户";OnlineSelectionPage.TargetCountText=durable.Count.ToString();
  await OnlineSelectionPage.CreateForWorkflowAsync(durable,workflowId);
  NavigateToSurface("OnlineSelection");
 }
 private async Task<Guid> AcceptOnlineSelectionWorkflowAsync(SelectionProject selection,SelectionFinalResult result,IReadOnlyList<SelectionAsset> assets,string archive)
 {
  if(result.SelectionProjectId!=selection.Id||!result.IsLocked)throw new InvalidDataException("客户确认结果身份无效。");
  var known=assets.Where(a=>a.ProjectId==selection.Id).ToDictionary(a=>a.Id);var selected=result.Items.Where(x=>x.Selected).ToArray();if(selected.Any(x=>!known.ContainsKey(x.ImageId)))throw new InvalidDataException("未知素材，不进入归片。");
  if(Sources.Count>0||Selections.Count>0||!string.IsNullOrWhiteSpace(ProjectName))await SaveCurrentProjectAsync();
  var history=await _projectHistoryService.LoadVisibleAsync();var existing=selection.WorkflowProjectId is Guid knownId?history.FirstOrDefault(p=>p.Id==knownId):history.FirstOrDefault(p=>p.SourceOnlineSelectionProjectId==selection.Id);if(existing is not null&&existing.SourceOnlineSelectionProjectId is Guid priorSelection&&priorSelection!=selection.Id)throw new InvalidDataException("归片项目关联不属于此选片项目，未覆盖。");var id=existing?.Id??selection.WorkflowProjectId??Guid.NewGuid();
  var project=existing??new PhotoProjectRecord{Id=id,SourceOnlineSelectionProjectId=selection.Id,Name=selection.Name,Category=CollectionCategory.JpegAndRaw,OutputMode=OutputMode.Flat,OutputBaseDirectory=archive};
  project.SourceOnlineSelectionProjectId=selection.Id;
  project.SourceDirectories=project.SourceDirectories.Concat(selected.Select(x=>known[x.ImageId].LocalSourcePath).Where(File.Exists).Select(p=>Path.GetDirectoryName(Path.GetFullPath(p))!)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  project.SelectionInputs=selected.Select(x=>known[x.ImageId].OriginalFileName).ToList();project.SelectionCount=project.SelectionInputs.Count;project.Summary=$"在线选片 {selection.Id:N}，已确认版本 {result.SelectionVersion}/{result.Revision}；仅导入选择，未执行复制。";
  if(ReferenceColorPage.WorkflowProjectId==project.Id){var stems=selected.Select(x=>Path.GetFileNameWithoutExtension(known[x.ImageId].OriginalFileName)).ToHashSet(StringComparer.OrdinalIgnoreCase);foreach(var target in ReferenceColorPage.Targets)target.IsSelected=stems.Contains(Path.GetFileNameWithoutExtension(target.Path));}
  await _projectHistoryService.UpsertAsync(project);await ReloadProjectHistoryAsync();await ContinueProjectAsync(project);CurrentWorkflowStep=2;StatusMessage="客户确认结果已进入对应归片项目；可继续扫描和匹配，原片未修改。";return id;
 }
}
