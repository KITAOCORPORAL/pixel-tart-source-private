using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
namespace RAWSelectionAssistant.Services;
public static class PlanningDocumentImages
{
 private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FlowDocument,SerializationState> States=new();
 private sealed class SerializationState{public bool Active;}
 public static bool IsSerializing(FlowDocument doc)=>States.TryGetValue(doc,out var state)&&state.Active;
 public static IEnumerable<Inline> Inlines(InlineCollection source){foreach(var i in source)if(i is Span span){foreach(var child in Inlines(span.Inlines))yield return child;}else yield return i;}
 public static IEnumerable<Image> Images(FlowDocument doc)
 {foreach(var p in PlanningTextSearch.Paragraphs(doc.Blocks))foreach(var i in Inlines(p.Inlines).OfType<InlineUIContainer>())if(i.Child is Image image)yield return image;foreach(var b in doc.Blocks.OfType<BlockUIContainer>())if(b.Child is Image image)yield return image;}
 public static string Position(Image image)
 {
  var parts=new List<string>();DependencyObject? node=image.Parent;
  while(node is FrameworkContentElement e){if(node is TableCell cell&&cell.Parent is TableRow row)parts.Add("cell:"+row.Cells.IndexOf(cell));else if(node is TableRow r&&r.Parent is TableRowGroup g)parts.Add("row:"+g.Rows.IndexOf(r));else if(node is Block block&&block.Parent is FlowDocument doc)parts.Add("block:"+doc.Blocks.ToList().IndexOf(block));node=e.Parent;}
  parts.Reverse();return string.Join("/",parts);
 }
 public static string Serialize(FlowDocument doc)
 {
  var replacements=new List<(InlineCollection Parent,Run Marker,InlineUIContainer Original)>();var blocks=new List<(Paragraph Marker,BlockUIContainer Original)>();
  States.GetOrCreateValue(doc).Active=true;
  try{
   foreach(var image in Images(doc).Where(i=>i.Tag is PlanningDocumentImage).ToArray())
   {var meta=(PlanningDocumentImage)image.Tag;var marker=new Run("[KITAO-IMAGE:"+meta.DocumentImageId.ToString("N")+"]");
    if(image.Parent is InlineUIContainer inline){var parent=inline.Parent is Paragraph p?p.Inlines:((Span)inline.Parent).Inlines;parent.InsertBefore(inline,marker);parent.Remove(inline);replacements.Add((parent,marker,inline));}
    else if(image.Parent is BlockUIContainer block){var p=new Paragraph(marker);doc.Blocks.InsertBefore(block,p);doc.Blocks.Remove(block);blocks.Add((p,block));}}
   using var stream=new MemoryStream();new TextRange(doc.ContentStart,doc.ContentEnd).Save(stream,DataFormats.Rtf);return Convert.ToBase64String(stream.ToArray());
  }finally{foreach(var r in replacements){r.Parent.InsertBefore(r.Marker,r.Original);r.Parent.Remove(r.Marker);}foreach(var b in blocks){doc.Blocks.InsertBefore(b.Marker,b.Original);doc.Blocks.Remove(b.Marker);}States.GetOrCreateValue(doc).Active=false;}
 }
 public static void Restore(FlowDocument doc,IReadOnlyList<PlanningDocumentImage> images)
 {
  var ids=new HashSet<Guid>();foreach(var meta in images)
  {if(!ids.Add(meta.DocumentImageId))throw new InvalidDataException("文档图片身份重复，停止读取。");var token="[KITAO-IMAGE:"+meta.DocumentImageId.ToString("N")+"]";foreach(var match in PlanningTextSearch.Find(doc,token,true).Reverse())
   {var range=PlanningTextSearch.Range(match);var start=range.Start;new TextRange(start,range.End).Text="";new InlineUIContainer(Create(meta),start);}}
 }
 public static Image Create(PlanningDocumentImage meta)
 {
  meta.Validate();using var stream=File.OpenRead(PlanningImageStorage.Resolve(meta));var hash=Convert.ToHexString(SHA256.HashData(stream));if(!hash.Equals(meta.CopyHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException($"文档图片 {meta.DocumentImageId}（{meta.Title}）副本哈希不符，停止读取。");stream.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();return new Image{Source=bitmap,Width=meta.Width,Height=meta.Height,Stretch=Stretch.Uniform,Tag=meta,ToolTip=meta.Title+"\n"+meta.Caption};
 }
 public static PlanningDocumentImage Copy(string directory,Guid document,byte[] bytes,AssetLibraryStableReference? source,string placement,string title,string caption,int order,Guid? shot=null)
 {
  var sourceHash=Convert.ToHexString(SHA256.HashData(bytes));if(source is not null&&!sourceHash.Equals(source.ContentHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Changed：素材内容哈希不符，未插入。");
  using var input=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=new MemoryStream();encoder.Save(output);var copy=output.ToArray();var hash=Convert.ToHexString(SHA256.HashData(copy));var fullDirectory=Path.GetFullPath(directory);var parent=Directory.GetParent(fullDirectory);if(parent?.Name!="images"||Path.GetFileName(fullDirectory)!=document.ToString("N"))throw new InvalidDataException("图片副本目录必须为根/images/DocumentId。");var root=parent.Parent!.FullName;PlanningImageStorage.RejectLinks(root,fullDirectory);Directory.CreateDirectory(fullDirectory);var id=Guid.NewGuid();var path=Path.Combine(fullDirectory,id.ToString("N")+".png");var temp=path+".tmp";
  try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(copy);file.Flush(true);}File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  var width=placement=="光标"?160d:360d;return new PlanningDocumentImage(id,document,source,PlanningImageStorage.RelativePath(document,id),hash,placement,title,caption,width,width*bitmap.PixelHeight/bitmap.PixelWidth,order,shot){StorageRoot=root};
 }
}
