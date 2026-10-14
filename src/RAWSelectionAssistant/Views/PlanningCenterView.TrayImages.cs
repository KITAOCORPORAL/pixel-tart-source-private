using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;
public partial class PlanningCenterView
{
 internal async Task OpenTrayPickerAsync()
 {
  if(_vm is not {} owner||!await FlushDocumentAsync())return;var project=owner.ProjectId;var document=owner.PlanningDocumentId;var generation=owner.EditorGeneration;var revision=_documentRenderRevision;
  var rich=DocumentContent.Children.OfType<PlanningRichEditor>().FirstOrDefault();var caret=rich?.CaptureImageInsertion();var choices=await owner.ListTrayImagesAsync();
  if(owner.ProjectId!=project||owner.PlanningDocumentId!=document)return;
  ModalContent.Children.Clear();Heading(ModalContent,"从灵感托盘插入");var search=new TextBox{Margin=new Thickness(4),ToolTip="搜索托盘/收藏名称、说明或素材ID"};ModalContent.Children.Add(search);
  var list=new ListBox{SelectionMode=SelectionMode.Extended,DisplayMemberPath="Label",ItemsSource=choices,Height=200};ModalContent.Children.Add(list);
  search.TextChanged+=(_,_)=>list.ItemsSource=choices.Where(c=>c.Label.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).ToArray();
  var placement=new ComboBox{ItemsSource=rich is null?new[]{"封面","参考图","情绪板","指定镜头"}:new[]{"光标"},SelectedIndex=0,Margin=new Thickness(4)};ModalContent.Children.Add(placement);
  var shots=new ComboBox{ItemsSource=owner.Shots,DisplayMemberPath="Name",SelectedItem=owner.SelectedShot,Margin=new Thickness(4)};ModalContent.Children.Add(shots);
  var preview=new Image{Height=140,Stretch=Stretch.Uniform};ModalContent.Children.Add(preview);var status=new TextBlock{TextWrapping=TextWrapping.Wrap};ModalContent.Children.Add(status);var active=true;var busy=false;
  ModalContent.Children.Add(AsyncAction("预览选中图片",async()=>{if(list.SelectedItem is not PlanningTrayChoice item)return;var result=owner.ResolveTrayImage is null?null:await owner.ResolveTrayImage(item.Entry.Reference);if(!active)return;status.Text=result?.Status+" · "+result?.Message;preview.Source=null;if(result?.Status==RAWSelectionAssistant.Core.Services.AssetLibrary.AssetResolutionStatus.Available&&result.ResolvedPath is {} path){var bytes=await System.IO.File.ReadAllBytesAsync(path);if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).Equals(item.Entry.Reference.ContentHash,StringComparison.OrdinalIgnoreCase)){status.Text="Changed：内容哈希不符";return;}using var stream=new System.IO.MemoryStream(bytes);var b=new System.Windows.Media.Imaging.BitmapImage();b.BeginInit();b.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;b.StreamSource=stream;b.EndInit();b.Freeze();preview.Source=b;}}));
  ModalContent.Children.Add(AsyncAction("插入选中图片",async()=>
  {
   if(busy)return;
   if(!active||owner.ProjectId!=project||owner.PlanningDocumentId!=document||owner.EditorGeneration!=generation||revision!=_documentRenderRevision){status.Text="文档已变化，请重新打开选图。";return;}
   var selected=list.SelectedItems.Cast<PlanningTrayChoice>().ToArray();if(selected.Length==0){status.Text="请选择图片，可多选。";return;}
   var where=placement.SelectedItem?.ToString()??"参考图";var shot=(shots.SelectedItem as RAWSelectionAssistant.Core.Services.Projects.ProjectShot)?.ShotId;
   if(where=="指定镜头"&&shot is null){status.Text="请选择目标镜头。";return;}
   busy=true;
   var prepared=new List<PlanningDocumentImage>();var failures=new List<string>();
   foreach(var choice in selected){try{prepared.Add(await owner.PrepareTrayImageAsync(choice,where,where=="指定镜头"?shot:null));}catch(Exception e){failures.Add(choice.Label+"："+e.Message);}}
   busy=false;
   if(!active||owner.ProjectId!=project||owner.EditorGeneration!=generation||revision!=_documentRenderRevision||!ReferenceEquals(_vm,owner)||ModalOverlay.Visibility!=Visibility.Visible){foreach(var p in prepared)System.IO.File.Delete(p.CopyPath);return;}
   var saved=true;
   if(prepared.Count>0)
   {
    try{if(rich is not null){rich.InsertOwnedImages(prepared,caret!);saved=await owner.FlushAsync();}else {saved=await owner.InsertSimpleImagesAsync(prepared);}}
    finally{foreach(var copy in prepared)if(!owner.DocumentImages.Any(i=>i.DocumentImageId==copy.DocumentImageId)&&System.IO.File.Exists(copy.CopyPath))System.IO.File.Delete(copy.CopyPath);}
   }
   if(!saved)failures.Add(owner.SaveStatus+"；图片副本和恢复草稿已保留，使用保存/重试，不要重复插入。");
   status.Text=string.Join("\n",failures);if(failures.Count==0){active=false;ModalOverlay.Visibility=Visibility.Collapsed;Render();}
  },"PrimaryButton"));
  ModalContent.Children.Add(Action("取消",()=>{active=false;ModalOverlay.Visibility=Visibility.Collapsed;}));ModalOverlay.Visibility=Visibility.Visible;FocusOverlay(ModalContent);
 }
 private void RenderOwnedImages()
 {
  if(_vm is not {} owner)return;DocumentContent.Children.Add(AsyncAction("从灵感托盘插入",OpenTrayPickerAsync));

  var history=new WrapPanel();history.Children.Add(AsyncAction("撤销图片放置",async()=>{await owner.UndoPlanningAsync();}));history.Children.Add(AsyncAction("重做策划操作",async()=>{await owner.RedoPlanningAsync();}));DocumentContent.Children.Add(history);
  foreach(var meta in owner.DocumentImages)
  {
   var panel=new StackPanel{Margin=new Thickness(4)};panel.Children.Add(Text(meta.Placement+" · "+meta.OriginLabel,12));try{var image=PlanningDocumentImages.Create(meta);image.MaxWidth=360;image.MaxHeight=200;panel.Children.Add(image);}catch(Exception e){panel.Children.Add(Text(e.Message,12));}
   var width=new TextBox{Text=meta.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),ToolTip="图片宽度DIP 20–1000"};panel.Children.Add(width);var title=new TextBox{Text=meta.Title};var caption=new TextBox{Text=meta.Caption,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap};panel.Children.Add(title);panel.Children.Add(caption);
   panel.Children.Add(AsyncAction("保存图片标题/说明",async()=>{if(!double.TryParse(width.Text,out var w))throw new InvalidOperationException("图片宽度必须是数字。");await owner.EditDocumentImageAsync(meta.DocumentImageId,title.Text,caption.Text,width:w);}));
   if(meta.Source is {} source)panel.Children.Add(AsyncAction("明确重新定位原素材（不替换文档副本）",async()=>{if(owner.RelocateAsset is null)throw new InvalidOperationException("素材重定位服务未连接。");if(owner.Dialogs.Confirm("仅重新定位来源，已保存文档图片副本保持不变。继续？","重新定位来源"))await owner.RelocateAsset(source.LibraryId,source.AssetId);}));
   var row=new WrapPanel();foreach(var delta in new[]{-1,1})row.Children.Add(AsyncAction(delta<0?"图片上移":"图片下移",async()=>{await owner.EditDocumentImageAsync(meta.DocumentImageId,title.Text,caption.Text,delta);}));panel.Children.Add(row);DocumentContent.Children.Add(new Expander{Header=meta.Title+" · "+meta.Placement,Content=panel,IsExpanded=!owner.IsAdvancedPlanning});
  }
 }
}
