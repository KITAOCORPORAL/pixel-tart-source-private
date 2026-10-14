using System.IO;
using System.Windows;
using System.Windows.Documents;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed record PlanningTrayChoice(InspirationTrayEntry Entry,string Collection)
{public string Label=>Collection+" · "+(Entry.OptionalNote??Entry.Reference.AssetId.ToString());}
public sealed partial class PlanningCenterViewModel
{
 public Func<AssetLibraryStableReference,Task<AssetResolutionResult>>? ResolveTrayImage {get;set;}
 public IReadOnlyList<PlanningDocumentImage> DocumentImages=>RichLayout?.Images??[];
 public string DocumentImageDirectory=>ProjectId is Guid id?Path.Combine(Path.GetDirectoryName(_planning.DraftPath(id))!,"images",PlanningDocumentId!.Value.ToString("N")):throw new InvalidOperationException("请先打开策划。");
 public async Task<IReadOnlyList<PlanningTrayChoice>> ListTrayImagesAsync()
 {
  if(_inspiration is null)return [];await _inspiration.InitializeAsync();var list=new List<PlanningTrayChoice>();list.AddRange((await _inspiration.ListAsync()).Select(e=>new PlanningTrayChoice(e,"当前托盘")));
  foreach(var c in (await _inspiration.ListCollectionsAsync()).Where(c=>c.ProjectId==ProjectId))list.AddRange((await _inspiration.ListCollectionEntriesAsync(c.CollectionId)).Select(e=>new PlanningTrayChoice(e,c.Name)));
  return list;
 }
 public async Task<PlanningDocumentImage> PrepareTrayImageAsync(PlanningTrayChoice item,string placement,Guid? shot)
 {
  var project=ProjectId;var doc=PlanningDocumentId??throw new InvalidOperationException("未打开文档。");var directory=DocumentImageDirectory;
  if(ResolveTrayImage is null)throw new InvalidOperationException("素材库解析未连接。");var resolved=await ResolveTrayImage(item.Entry.Reference);
  if(project!=ProjectId||doc!=PlanningDocumentId)throw new InvalidOperationException("策划已切换，取消插入。");
  if(resolved.Status!=AssetResolutionStatus.Available||resolved.ResolvedPath is null)throw new InvalidOperationException(resolved.Status+"："+(resolved.Message??"素材暂不可用"));
  var bytes=await File.ReadAllBytesAsync(resolved.ResolvedPath);if(project!=ProjectId||doc!=PlanningDocumentId)throw new InvalidOperationException("策划已切换，取消插入。");
  return PlanningDocumentImages.Copy(directory,doc,bytes,item.Entry.Reference,placement,Path.GetFileNameWithoutExtension(resolved.ResolvedPath),item.Entry.OptionalNote??"",DocumentImages.Count,shot);
 }
 public async Task<bool> InsertSimpleImagesAsync(IReadOnlyList<PlanningDocumentImage> images)
 {
  if(!await FlushAsync())return false;if(images.Any(i=>i.DocumentId!=PlanningDocumentId))throw new InvalidOperationException("图片不属于当前文档。");
  CapturePlanningUndo();var doc=RichTextPackage is {} data?PlanningDocumentLayout.Load(data,RichLayout):new FlowDocument(new Paragraph(new Run(DocumentBody)));
  foreach(var meta in images){var p=new Paragraph();p.Inlines.Add(new InlineUIContainer(PlanningDocumentImages.Create(meta)));p.Inlines.Add(new LineBreak());p.Inlines.Add(new Run(meta.Title+" — "+meta.Caption));if(meta.Placement=="封面"&&doc.Blocks.FirstBlock is {} first)doc.Blocks.InsertBefore(first,p);else doc.Blocks.Add(p);}
  SaveFlowDocument(doc);EditorGeneration++;var saved=await FlushProgrammaticPlanningAsync();PresentationChanged();return saved;
 }
 private void SaveFlowDocument(FlowDocument doc)
 {SaveRichText(PlanningDocumentImages.Serialize(doc),new TextRange(doc.ContentStart,doc.ContentEnd).Text,PlanningDocumentLayout.Capture(doc,RichLayout?.Page??new(),RichLayout?.ImportLosses??[]));}
 public async Task<bool> EditDocumentImageAsync(Guid id,string title,string caption,int direction=0)
 {
  if(!await FlushAsync()||RichTextPackage is null)return false;var doc=PlanningDocumentLayout.Load(RichTextPackage,RichLayout);var image=PlanningDocumentImages.Images(doc).Single(i=>(i.Tag as PlanningDocumentImage)?.DocumentImageId==id);var meta=(PlanningDocumentImage)image.Tag;
  CapturePlanningUndo();image.Tag=meta with{Title=title,Caption=caption};image.ToolTip=title+"\n"+caption;
  var inline=image.Parent as InlineUIContainer;var paragraph=inline?.Parent as Paragraph;
  if(paragraph is not null){var runs=paragraph.Inlines.OfType<Run>().ToArray();if(runs.Length==1&&runs[0].Text==meta.Title+" — "+meta.Caption)runs[0].Text=title+" — "+caption;}
  if(direction!=0){if(paragraph?.Parent!=doc)throw new InvalidOperationException("表内图片顺序请在进阶正文中编辑，不跨单元格移动。");var adjacent=direction<0?paragraph.PreviousBlock:paragraph.NextBlock;if(adjacent is not null){doc.Blocks.Remove(paragraph);if(direction<0)doc.Blocks.InsertBefore(adjacent,paragraph);else doc.Blocks.InsertAfter(adjacent,paragraph);}}
  SaveFlowDocument(doc);EditorGeneration++;var saved=await FlushProgrammaticPlanningAsync();PresentationChanged();return saved;
 }
}
