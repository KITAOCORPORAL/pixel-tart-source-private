using System.IO;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class MainViewModel
{
 private async Task<Guid> AcceptOnlineSelectionWorkflowAsync(SelectionProject selection,SelectionFinalResult result,IReadOnlyList<SelectionAsset> assets,string archive)
 {
  if(result.SelectionProjectId!=selection.Id||!result.IsLocked)throw new InvalidDataException("客户确认结果身份无效。");
  var known=assets.Where(a=>a.ProjectId==selection.Id).ToDictionary(a=>a.Id);var selected=result.Items.Where(x=>x.Selected).ToArray();if(selected.Any(x=>!known.ContainsKey(x.ImageId)))throw new InvalidDataException("未知素材，不进入归片。");
  if(Sources.Count>0||Selections.Count>0||!string.IsNullOrWhiteSpace(ProjectName))await SaveCurrentProjectAsync();
  var history=await _projectHistoryService.LoadVisibleAsync();var existing=selection.WorkflowProjectId is Guid knownId?history.FirstOrDefault(p=>p.Id==knownId):history.FirstOrDefault(p=>p.SourceOnlineSelectionProjectId==selection.Id);if(existing is not null&&existing.SourceOnlineSelectionProjectId!=selection.Id)throw new InvalidDataException("归片项目关联不属于此选片项目，未覆盖。");var id=existing?.Id??selection.WorkflowProjectId??Guid.NewGuid();
  var project=existing??new PhotoProjectRecord{Id=id,SourceOnlineSelectionProjectId=selection.Id,Name=selection.Name,Category=CollectionCategory.JpegAndRaw,OutputMode=OutputMode.Flat,OutputBaseDirectory=archive};
  project.SourceDirectories=selected.Select(x=>known[x.ImageId].LocalSourcePath).Where(File.Exists).Select(p=>Path.GetDirectoryName(Path.GetFullPath(p))!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  project.SelectionInputs=selected.Select(x=>known[x.ImageId].OriginalFileName).ToList();project.SelectionCount=project.SelectionInputs.Count;project.Summary=$"在线选片 {selection.Id:N}，已确认版本 {result.SelectionVersion}/{result.Revision}；仅导入选择，未执行复制。";
  await _projectHistoryService.UpsertAsync(project);await ReloadProjectHistoryAsync();await ContinueProjectAsync(project);CurrentWorkflowStep=2;StatusMessage="客户确认结果已进入对应归片项目；可继续扫描和匹配，原片未修改。";return id;
 }
}
