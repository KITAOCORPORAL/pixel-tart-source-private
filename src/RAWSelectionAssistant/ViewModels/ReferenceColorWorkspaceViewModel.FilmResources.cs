using System.IO;
using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 public string FilmResourceSearch{get;set;}="";public string FilmResourceFilter{get;set;}="全部";
 public IReadOnlyList<PhotographerPaper> FilteredFilmPapers=>OrderFilm(PhotographerPapers.Where(p=>(string.IsNullOrWhiteSpace(FilmResourceSearch)||p.Name.Contains(FilmResourceSearch,StringComparison.CurrentCultureIgnoreCase)||p.Group.Contains(FilmResourceSearch))&&(FilmResourceFilter!="收藏"||R53FavoriteResources.Contains("paper:"+p.ProfileId))&&(FilmResourceFilter!="最近"||R53RecentResources.Contains("paper:"+p.ProfileId))),p=>"paper:"+p.ProfileId).ToArray();
 public IReadOnlyList<LightPrintFrame> FilteredFilmFrames=>OrderFilm(LightPrintFrameChoices.Where(f=>f.Shippable&&(string.IsNullOrWhiteSpace(FilmResourceSearch)||f.Name.Contains(FilmResourceSearch,StringComparison.CurrentCultureIgnoreCase))&&(FilmResourceFilter!="收藏"||R53FavoriteResources.Contains("frame:"+f.Id))&&(FilmResourceFilter!="最近"||R53RecentResources.Contains("frame:"+f.Id))),f=>"frame:"+f.Id).ToArray();
 private IEnumerable<T> OrderFilm<T>(IEnumerable<T> rows,Func<T,string> key)=>FilmResourceFilter=="最近"?rows.OrderBy(item=>R53RecentResources.ToList().IndexOf(key(item))):rows;
 public void SetFilmResourceFilter(string search,string filter){FilmResourceSearch=search;FilmResourceFilter=filter;OnPropertyChanged(nameof(FilteredFilmPapers));OnPropertyChanged(nameof(FilteredFilmFrames));}
 public void SelectFilmPaper(string id){if(IsLightPrintLocked)return;SelectedPhotographerPaperId=id;RecordR53Resource("paper:"+id);RefreshFilmResourceChoices();RequestLightPrintPreview();}
 public void SelectFilmFrame(string id){if(IsLightPrintLocked)return;SelectedLightPrintFrameId=id;RecordR53Resource("frame:"+id);RefreshFilmResourceChoices();}
 public void FavoriteFilmResource(string key){ToggleR53Favorite(key);RefreshFilmResourceChoices();}
 public bool IsFilmResourceFavorite(string key)=>R53FavoriteResources.Contains(key);
 public void SaveFilmDraft(){if(ActiveTarget is not{} target||IsLightPrintLocked||IsInstantGenerating)return;try{var film=BuildInstantDefinition(target,target.RawDefinition,target.ActiveEditVersionId,false).InstantFilm;Layout=Layout with{FilmDraft=film,FilmDraftFrameId=SelectedLightPrintFrameId,FilmDraftInputMode=InstantInputMode};ScheduleR53Autosave();LightPrintStatus="参数已存入当前项目";}catch(Exception error)when(error is IOException or InvalidDataException or NotSupportedException or ArgumentException){LightPrintStatus="参数未保存："+error.Message;}}
 public void RestoreFilmDraft(){if(IsLightPrintLocked||IsInstantGenerating||Layout.FilmDraft is not{} film)return;RestoreInstantControls(film);if(Layout.FilmDraftFrameId is{} frame)SelectedLightPrintFrameId=frame;InstantInputMode=Layout.FilmDraftInputMode??"CurrentEdits";RequestLightPrintPreview();LightPrintStatus="已恢复项目中的参数";}
 private void RefreshFilmResourceChoices(){OnPropertyChanged(nameof(FilteredFilmPapers));OnPropertyChanged(nameof(FilteredFilmFrames));}
}
