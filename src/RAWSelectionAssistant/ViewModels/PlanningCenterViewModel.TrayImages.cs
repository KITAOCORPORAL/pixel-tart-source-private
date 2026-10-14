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
 public PlanningImageInventory ListUnreferencedImageCopies()
 {
  var doc=PlanningDocumentId??throw new InvalidOperationException("请先打开策划。");
  var memory=_planningUndo.Concat(_planningRedo).Where(s=>s.Project==ProjectId).SelectMany(s=>s.Document.Package?.RichLayout?.Images??[])
    .Concat(DocumentImages).Where(i=>i.DocumentId==doc).Select(i=>i.DocumentImageId);
  return PlanningImageInventoryScanner.Scan(_planning.StorageRoot,doc,memory);
 }
 public Func<AssetLibraryStableReference,Task<AssetResolutionResult>>? ResolveTrayImage {get;set;}
 public IReadOnlyList<PlanningDocumentImage> DocumentImages=>RichLayout?.Images??[];
 public string DocumentImageDirectory=>ProjectId is Guid id?PlanningImageStorage.DocumentDirectory(_planning.StorageRoot,PlanningDocumentId!.Value):throw new InvalidOperationException("请先打开策划。");
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
  if(new FileInfo(resolved.ResolvedPath).Length>64L*1024*1024)throw new InvalidDataException("图片超过64MB，未插入；请先制作合适尺寸的文档图片。");
  var bytes=await File.ReadAllBytesAsync(resolved.ResolvedPath);if(project!=ProjectId||doc!=PlanningDocumentId)throw new InvalidOperationException("策划已切换，取消插入。");
  return PlanningDocumentImages.Copy(directory,doc,bytes,item.Entry.Reference,placement,Path.GetFileNameWithoutExtension(resolved.ResolvedPath),item.Entry.OptionalNote??"",DocumentImages.Count,shot);
 }
 public async Task<bool> InsertSimpleImagesAsync(IReadOnlyList<PlanningDocumentImage> images)
 {
  if(!await FlushAsync())return false;if(images.Any(i=>i.DocumentId!=PlanningDocumentId))throw new InvalidOperationException("图片不属于当前文档。");
  CapturePlanningUndo();var doc=RichTextPackage is {} data?PlanningDocumentLayout.Load(data,RichLayout):new FlowDocument(new Paragraph(new Run(DocumentBody)));
  foreach(var meta in images){var p=new Paragraph();p.Inlines.Add(new InlineUIContainer(PlanningDocumentImages.Create(meta)));p.Inlines.Add(new LineBreak());p.Inlines.Add(new Run(meta.Title+" — "+meta.Caption));var label=meta.Placement=="指定镜头"?"镜头图片 · "+Shots.First(s=>s.ShotId==meta.ShotId).Name:meta.Placement;var heading=new Paragraph(new Run(label)){FontWeight=FontWeights.Bold};if(meta.Placement=="封面"&&doc.Blocks.FirstBlock is {} first){doc.Blocks.InsertBefore(first,heading);doc.Blocks.InsertAfter(heading,p);}else{doc.Blocks.Add(heading);doc.Blocks.Add(p);}}
  SaveFlowDocument(doc);EditorGeneration++;var saved=await FlushProgrammaticPlanningAsync();PresentationChanged();return saved;
 }
 private void SaveFlowDocument(FlowDocument doc)
 {SaveRichText(PlanningDocumentImages.Serialize(doc),new TextRange(doc.ContentStart,doc.ContentEnd).Text,PlanningDocumentLayout.Capture(doc,RichLayout?.Page??new(),RichLayout?.ImportLosses??[]));}
 public async Task<bool> EditDocumentImageAsync(Guid id,string title,string caption,int direction=0,double? width=null)
 {
  if(!await FlushAsync()||RichTextPackage is null)return false;var doc=PlanningDocumentLayout.Load(RichTextPackage,RichLayout);var image=PlanningDocumentImages.Images(doc).Single(i=>(i.Tag as PlanningDocumentImage)?.DocumentImageId==id);var meta=(PlanningDocumentImage)image.Tag;
  CapturePlanningUndo();image.Tag=meta with{Title=title,Caption=caption};if(width is double requested){if(!double.IsFinite(requested)||requested<20||requested>1000)throw new InvalidOperationException("图片宽度须为20–1000 DIP。");image.Width=requested;image.Height=requested*meta.Height/meta.Width;}image.ToolTip=title+"\n"+caption;
  var inline=image.Parent as InlineUIContainer;Paragraph? paragraph=null;for(DependencyObject? node=inline;node is FrameworkContentElement e;node=e.Parent){if(node is Paragraph found){paragraph=found;break;}}
  if(paragraph is not null && inline is not null)
  {
   var nextImage=PlanningDocumentImages.Inlines(paragraph.Inlines).OfType<InlineUIContainer>().FirstOrDefault(i=>i.ElementStart.CompareTo(inline.ElementEnd)>=0 && i.Child is System.Windows.Controls.Image);
   foreach(var match in PlanningTextSearch.Find(doc,meta.Title+" — "+meta.Caption,true).Where(m=>ReferenceEquals(m.Paragraph,paragraph)).Reverse())
   {
    var range=PlanningTextSearch.Range(match);
    if(range.Start.CompareTo(inline.ElementEnd)>=0 && (nextImage is null||range.End.CompareTo(nextImage.ElementStart)<=0))PlanningTextSearch.Replace(match,title+" — "+caption);
   }
  }
  if(direction!=0){if(paragraph?.Parent!=doc)throw new InvalidOperationException("表内图片顺序请在进阶正文中编辑，不跨单元格移动。");var owned=doc.Blocks.OfType<Paragraph>().Where(p=>PlanningDocumentImages.Inlines(p.Inlines).OfType<InlineUIContainer>().Any(i=>i.Child is System.Windows.Controls.Image {Tag:PlanningDocumentImage})).ToArray();var index=Array.IndexOf(owned,paragraph);Block? adjacent=index+direction>=0&&index+direction<owned.Length?owned[index+direction]:null;if(adjacent is not null){doc.Blocks.Remove(paragraph);if(direction<0)doc.Blocks.InsertBefore(adjacent,paragraph);else doc.Blocks.InsertAfter(adjacent,paragraph);}}
  SaveFlowDocument(doc);EditorGeneration++;var saved=await FlushProgrammaticPlanningAsync();PresentationChanged();return saved;
 }
}
