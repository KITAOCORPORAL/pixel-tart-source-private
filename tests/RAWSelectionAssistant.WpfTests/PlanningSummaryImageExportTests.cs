using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W=DocumentFormat.OpenXml.Wordprocessing;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class PlanningSummaryImageExportTests
{
 [TestMethod]public Task MultipleOwnedImagesLongBodyRepeatedReferencesMissingCopyAndReopenRemainConsistent()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();var vm=f.Vm;await vm.LoadAsync(f.Id);vm.DocumentTitle="合成多图策划汇总";
  var bytes=PlanningRichFlowFixture.ReferencePng(0);var source=new AssetLibraryStableReference(Guid.NewGuid(),Guid.NewGuid(),Hash(bytes));
  var a=PlanningDocumentImages.Copy(vm.DocumentImageDirectory,vm.PlanningDocumentId!.Value,bytes,source,"参考图","逆光人物自有副本","中文图注甲 保留高光边缘",2);
  var b=PlanningDocumentImages.Copy(vm.DocumentImageDirectory,vm.PlanningDocumentId.Value,Portrait(),null,"参考图","竖幅自有图片","中文图注乙 不拉伸人物",0);
  var c=PlanningDocumentImages.Copy(vm.DocumentImageDirectory,vm.PlanningDocumentId.Value,PlanningRichFlowFixture.ReferencePng(1),null,"情绪板","情绪板自有图片","中文图注丙 安静的色块",1);
  var missing=PlanningDocumentImages.Copy(vm.DocumentImageDirectory,vm.PlanningDocumentId.Value,bytes,null,"灯位图","缺失灯位图","中文图注丁 不替换其他文件",3);
  // Huge saved display sizes used to exceed a page; summary bounds the detached copy only.
  a=a with{Width=4000,Height=2600};b=b with{Width=1000,Height=6000};
  var rich=new FlowDocument();rich.Blocks.Add(new Paragraph(new Run("长正文开头标记")));
  for(var n=0;n<18;n++)rich.Blocks.Add(new Paragraph(new Run($"长正文段落 {n:00} "+string.Concat(Enumerable.Repeat("合成拍摄说明保留人物动作与光线方向，不含私人资料。",7)))));
  void BodyImage(PlanningDocumentImage image){rich.Blocks.Add(new Paragraph(new InlineUIContainer(PlanningDocumentImages.Create(image))));}
  BodyImage(a);BodyImage(a);BodyImage(b);BodyImage(c);BodyImage(missing);rich.Blocks.Add(new Paragraph(new Run("长正文末尾标记")));
  var layout=PlanningDocumentLayout.Capture(rich,new PlanningPageLayout(),[]) with{Images=[a,b,c,missing]};vm.SaveRichText(PlanningDocumentImages.Serialize(rich),new TextRange(rich.ContentStart,rich.ContentEnd).Text,layout);Assert.IsTrue(await vm.FlushAsync());
  var reference=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Lighting,source.LibraryId,source.AssetId,Title:"同一库引用跨区",Note:"当前原件不读取");await vm.AddDocumentReferencesAsync([reference]);var item=vm.AllProjectReferences.Single(x=>x.Id==reference.ReferenceId);await vm.UpdateReferenceAsync(item,"分类","灯光");await vm.UpdateReferenceAsync(vm.AllProjectReferences.Single(x=>x.Id==reference.ReferenceId),"情绪板");
  var external=Path.Combine(f.Root,"external-readable.png");await File.WriteAllBytesAsync(external,bytes);await vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:external,Title:"外链只列状态")]);
  Assert.IsTrue(await vm.FlushAsync());var reopened=f.NewVm();await reopened.LoadAsync(f.Id);var plan=Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json");var before=await File.ReadAllBytesAsync(plan);var originals=new[]{a,b,c}.ToDictionary(i=>i.DocumentImageId,i=>File.ReadAllBytes(PlanningImageStorage.Resolve(i)));File.Delete(PlanningImageStorage.Resolve(missing));
  var originalExport=Path.Combine(f.Root,"images-summary-before-reopen.docx");Assert.IsNotNull(await vm.ExportPlanningSummaryDocxAsync(originalExport));
  var path=Path.Combine(f.Root,"images-summary.docx");var result=await reopened.ExportPlanningSummaryDocxAsync(path);Assert.IsNotNull(result);Assert.AreEqual(15,result.Sections);Assert.AreEqual(2,result.Warnings.Count,"Missing body position and lighting position must both be declared");
  using(var package=WordprocessingDocument.Open(path,false))
  {
   Assert.IsEmpty(new OpenXmlValidator().Validate(package).ToArray());var body=package.MainDocumentPart!.Document.Body!;var headings=body.Elements<W.Paragraph>().Where(p=>p.ParagraphProperties?.ParagraphStyleId?.Val?.Value=="Heading1").Select(p=>p.InnerText).ToArray();CollectionAssert.AreEqual(PlanningReadableSummaryBuilder.SectionOrder.Select((name,index)=>$"{index+1:00} {name}").ToArray(),headings);
   var drawings=Drawings(path);Assert.AreEqual(9,drawings.Count);Assert.AreEqual(5,drawings.Count(x=>x.ImageId==a.DocumentImageId));
   foreach(var row in drawings){Assert.IsTrue(row.Width<=520.01&&row.Height<=360.01);Assert.IsTrue(row.Section>0);}
   CollectionAssert.AreEqual(new[]{a.DocumentImageId,a.DocumentImageId,b.DocumentImageId,c.DocumentImageId},drawings.Where(x=>x.Section==2).Select(x=>x.ImageId).ToArray());
   CollectionAssert.AreEqual(new[]{b.DocumentImageId,a.DocumentImageId},drawings.Where(x=>x.Section==4).Select(x=>x.ImageId).ToArray());
   CollectionAssert.AreEqual(new[]{c.DocumentImageId,a.DocumentImageId},drawings.Where(x=>x.Section==5).Select(x=>x.ImageId).ToArray());Assert.AreEqual(a.DocumentImageId,drawings.Single(x=>x.Section==6).ImageId);
   StringAssert.Contains(body.InnerText,"缺失灯位图");StringAssert.Contains(body.InnerText,"文档图片不可用");StringAssert.Contains(body.InnerText,"外链只列状态");StringAssert.Contains(body.InnerText,"中文图注甲");StringAssert.Contains(body.InnerText,"项目自有历史副本");StringAssert.Contains(body.InnerText,"未读取库当前原件");
  }
  await Assert.ThrowsAsync<InvalidOperationException>(()=>reopened.ExportPlanningSummaryDocxAsync(path));
  Assert.AreEqual(Text(originalExport),Text(path));CollectionAssert.AreEqual(Drawings(originalExport).Select(x=>x.ImageId).ToArray(),Drawings(path).Select(x=>x.ImageId).ToArray());
  CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(plan));foreach(var image in new[]{a,b,c})CollectionAssert.AreEqual(originals[image.DocumentImageId],File.ReadAllBytes(PlanningImageStorage.Resolve(image)));Assert.IsFalse(File.Exists(Path.Combine(f.Root,"planning",PlanningImageStorage.RelativePath(missing.DocumentId,missing.DocumentImageId))));
  var again=Path.Combine(f.Root,"images-summary-again.docx");Assert.IsNotNull(await reopened.ExportPlanningSummaryDocxAsync(again));Assert.AreEqual(Text(path),Text(again));CollectionAssert.AreEqual(Drawings(path).Select(x=>x.ImageId).ToArray(),Drawings(again).Select(x=>x.ImageId).ToArray());
  var read=PlanningDocxAdapter.Import(path);var paginator=new PlanningPaginator(read.Document);paginator.PageSize=new Size(read.Document.PageWidth,read.Document.PageHeight);paginator.ComputePageCount();Assert.IsTrue(paginator.PageCount>2&&paginator.PageCount<35,"Long body must paginate within a bounded page count");
  for(var pageIndex=0;pageIndex<paginator.PageCount;pageIndex++)
  {
   using var page=paginator.GetPage(pageIndex);var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,794,1123));drawing.DrawRectangle(new VisualBrush(page.Visual),null,new Rect(0,0,794,1123));}
   var bitmap=new RenderTargetBitmap(794,1123,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var pixels=new byte[794*1123*4];bitmap.CopyPixels(pixels,794*4,0);var ink=0;for(var pixel=0;pixel<pixels.Length;pixel+=4)if(pixels[pixel]<235||pixels[pixel+1]<235||pixels[pixel+2]<235)ink++;Assert.IsTrue(ink>150,$"Page {pageIndex+1} is blank or nearly blank: {ink} pixels");
  }
  var evidence=Environment.GetEnvironmentVariable("KITAO_SUMMARY_IMAGE_EVIDENCE");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);File.Copy(path,Path.Combine(evidence,"synthetic-owned-images-summary.docx"));await File.WriteAllTextAsync(Path.Combine(evidence,"drawing-positions.json"),JsonSerializer.Serialize(Drawings(path),new JsonSerializerOptions{WriteIndented=true}));await File.WriteAllTextAsync(Path.Combine(evidence,"identity.json"),JsonSerializer.Serialize(new{project=f.Id,document=vm.PlanningDocumentId,a=a.DocumentImageId,b=b.DocumentImageId,c=c.DocumentImageId,missing=missing.DocumentImageId,drawingCount=9,pageCount=paginator.PageCount,saveReopen=true,sourceBytesUnchanged=true,scope="Synthetic real stores and DOCX components; not physical Word/WPS"},new JsonSerializerOptions{WriteIndented=true}));PlanningProposalPdf.ExportDocument(read.Document,Path.Combine(evidence,"synthetic-owned-images-layout-review.pdf"),216);}
 });
 [TestMethod]public Task ChangedUnsupportedOversizedAndForeignImagesAreExplainedNotSubstituted()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var bytes=PlanningRichFlowFixture.ReferencePng(0);var doc=f.Vm.PlanningDocumentId!.Value;
  PlanningDocumentImage Copy(string title)=>PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,doc,bytes,null,"参考图",title,"合成失败图注",0);
  var changed=Copy("变化副本");var changedPath=PlanningImageStorage.Resolve(changed);await File.WriteAllBytesAsync(changedPath,PlanningRichFlowFixture.ReferencePng(1));
  var unsupported=Copy("不支持格式");var unsupportedPath=PlanningImageStorage.Resolve(unsupported);await File.WriteAllTextAsync(unsupportedPath,"not png format "+new string('x',80));unsupported=unsupported with{CopyHash=Hash(await File.ReadAllBytesAsync(unsupportedPath))};
  var oversized=Copy("超大副本");var oversizedPath=PlanningImageStorage.Resolve(oversized);using(var file=new FileStream(oversizedPath,FileMode.Open,FileAccess.Write))file.SetLength(PlanningSummaryOwnedImage.MaximumBytes+1);
  var foreign=Copy("跨文档副本") with{DocumentId=Guid.NewGuid()};var input=new PlanningSummaryInput(f.Id,"安全样例",new PlanningDocument{Package=new(){ProjectId=f.Id,DocumentId=doc,RichLayout=new(new(),[],[]){Images=[changed,unsupported,oversized,foreign]}}},new(),[]);
  var source=input.Document;var warnings=new List<string>();var content=PlanningSummaryDocument.Create(PlanningReadableSummaryBuilder.Build(input),source,warnings);Assert.AreEqual(4,warnings.Count);var all=new TextRange(content.ContentStart,content.ContentEnd).Text;foreach(var title in new[]{"变化副本","不支持格式","超大副本","跨文档副本"})StringAssert.Contains(all,title);Assert.IsEmpty(PlanningDocumentImages.Images(content));
  var path=Path.Combine(f.Root,"unreadable-summary.docx");PlanningDocxAdapter.Export(content,path);var evidence=Environment.GetEnvironmentVariable("KITAO_SUMMARY_IMAGE_EVIDENCE");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);File.Copy(path,Path.Combine(evidence,"synthetic-image-status-summary.docx"));}
 });
 [TestMethod]public Task TableImageKeepsAspectRatioAndFitsCellWithoutChangingSavedDimensions()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var bytes=PlanningRichFlowFixture.ReferencePng(0);var meta=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,bytes,null,"光标","表内图片","中文表内图注",0) with{Width=4000,Height=2400};
  var document=new FlowDocument();var table=new Table();table.Columns.Add(new TableColumn{Width=new GridLength(300)});table.Columns.Add(new TableColumn{Width=new GridLength(300)});var group=new TableRowGroup();table.RowGroups.Add(group);var row=new TableRow();group.Rows.Add(row);row.Cells.Add(new TableCell(new Paragraph(new InlineUIContainer(PlanningDocumentImages.Create(meta)))));row.Cells.Add(new TableCell(new Paragraph(new Run("右侧正文不能被图片挤掉"))));document.Blocks.Add(table);
  var layout=PlanningDocumentLayout.Capture(document,new(),[]);f.Vm.SaveRichText(PlanningDocumentImages.Serialize(document),new TextRange(document.ContentStart,document.ContentEnd).Text,layout);Assert.IsTrue(await f.Vm.FlushAsync());var path=Path.Combine(f.Root,"table-images.docx");Assert.IsNotNull(await f.Vm.ExportPlanningSummaryDocxAsync(path));var image=Drawings(path).Single();Assert.IsTrue(image.Width<=325);Assert.AreEqual(120d/80d,image.Width/image.Height,.01);StringAssert.Contains(Text(path),"右侧正文不能被图片挤掉");Assert.IsTrue(f.Vm.DocumentImages.Single().Width>3000,"Detached export must not shrink persisted layout dimensions");
 });
 [TestMethod]public Task ExclusiveReadDeniedCopyIsExplainedWithoutReadingAnotherFile()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var meta=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,PlanningRichFlowFixture.ReferencePng(0),null,"参考图","占用中的副本","原图不改写",0);var owned=PlanningImageStorage.Resolve(meta);
  using(var held=new FileStream(owned,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
  {
   var source=new PlanningDocument{Package=new(){ProjectId=f.Id,DocumentId=meta.DocumentId,RichLayout=new(new(),[],[]){Images=[meta]}}};var projection=PlanningReadableSummaryBuilder.Build(new(f.Id,"不可读测试",source,new(),[]));var warnings=new List<string>();var doc=PlanningSummaryDocument.Create(projection,source,warnings);Assert.AreEqual(1,warnings.Count);StringAssert.Contains(warnings[0],"占用中的副本");Assert.IsEmpty(PlanningDocumentImages.Images(doc));
  }
  Assert.AreEqual(meta.CopyHash,Hash(File.ReadAllBytes(owned)));
 });
 [TestMethod]public Task OwnedImageWithoutRichInsertionIsExplicitlyRetainedInDocumentContent()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var meta=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,PlanningRichFlowFixture.ReferencePng(0),null,"光标","无正文位置的自有图","原元数据保留",0);
  var source=new PlanningDocument{Package=new(){ProjectId=f.Id,DocumentId=meta.DocumentId,RichLayout=new(new(),[],[]){Images=[meta]}}};var projection=PlanningReadableSummaryBuilder.Build(new(f.Id,"incomplete",source,new(),[]));
  Assert.AreEqual(meta.DocumentImageId,projection.Sections[14].OwnedImages.Single().DocumentImageId);var warnings=new List<string>();var content=PlanningSummaryDocument.Create(projection,source,warnings);Assert.AreEqual(1,PlanningDocumentImages.Images(content).Count());StringAssert.Contains(new TextRange(content.ContentStart,content.ContentEnd).Text,"没有持久富文本插入位置");
 });
 public sealed record DrawingPosition(int Section,Guid ImageId,double Width,double Height);
 static List<DrawingPosition> Drawings(string path)
 {
  using var package=WordprocessingDocument.Open(path,false);var result=new List<DrawingPosition>();var section=0;
  foreach(var block in package.MainDocumentPart!.Document.Body!.ChildElements){if(block is W.Paragraph p&&p.ParagraphProperties?.ParagraphStyleId?.Val?.Value=="Heading1"&&int.TryParse(p.InnerText.AsSpan(0,2),out var index))section=index;
   foreach(var drawing in block.Descendants<W.Drawing>()){var xml=System.Xml.Linq.XElement.Parse(drawing.OuterXml);var property=xml.Descendants().Single(x=>x.Name.LocalName=="docPr");var description=property.Attribute("descr")!.Value;using var meta=JsonDocument.Parse(description["KitaoImageV1:".Length..]);var extent=xml.Descendants().First(x=>x.Name.LocalName=="extent");result.Add(new(section,meta.RootElement.GetProperty("DocumentImageId").GetGuid(),double.Parse(extent.Attribute("cx")!.Value)/9525d,double.Parse(extent.Attribute("cy")!.Value)/9525d));}}
  return result;
 }
 static byte[] Portrait(){var w=240;var h=1440;var data=new byte[w*h*3];for(int i=0;i<data.Length;i++)data[i]=(byte)(30+i%180);var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Rgb24,null,data,w*3);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var memory=new MemoryStream();encoder.Save(memory);return memory.ToArray();}
 static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
 static string Text(string path){using var doc=WordprocessingDocument.Open(path,false);return doc.MainDocumentPart!.Document.Body!.InnerText;}
}
