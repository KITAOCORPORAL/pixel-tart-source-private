using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.Core.Services.Projects;
using DocumentFormat.OpenXml.Packaging;
using W=DocumentFormat.OpenXml.Wordprocessing;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningTableContentTests
{
 internal static IEnumerable<Inline> Flat(InlineCollection inlines){foreach(var inline in inlines)if(inline is Span span){foreach(var nested in Flat(span.Inlines))yield return nested;}else yield return inline;}
 internal static FlowDocument Fixture(int count=6)
 {
  var t=PlanningR7TableTests.Make(count+1,3);t.Tag=new PlanningBlockSemantic(0){RepeatHeader=true};for(var i=0;i<3;i++)t.Columns.Add(new TableColumn{Width=new GridLength(210)});
  for(var r=1;r<=count;r++)
  {var cells=t.RowGroups[0].Rows[r].Cells;cells[0].Blocks.Clear();var bitmap=new BitmapImage();using var stream=new MemoryStream(PlanningRichFlowFixture.ReferencePng(r%3));bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
   var p=new Paragraph(new Run($"镜头{r:00}参考前"));p.Inlines.Add(new InlineUIContainer(new Image{Source=bitmap,Width=90,Height=60}));p.Inlines.Add(new Run("参考后"));cells[0].Blocks.Add(p);
   cells[1].Blocks.Clear();var action=new Paragraph(new Run("第一拍：进入"));action.Inlines.Add(new LineBreak());action.Inlines.Add(new Run("第二拍：转身"));action.Inlines.Add(new LineBreak());action.Inlines.Add(new Run("第三拍：离场"));cells[1].Blocks.Add(action);
   cells[2].Blocks.Clear();var list=new List(new ListItem(new Paragraph(new Run("道具检查")))){MarkerStyle=r%2==0?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,StartIndex=r%2==0?3:1};list.ListItems.Add(new ListItem(new Paragraph(new Run("灯光确认"))));cells[2].Blocks.Add(list);
  }
  var doc=new FlowDocument(t);PlanningDocumentLayout.Apply(doc,PlanningDocumentLayout.Capture(doc,new(),[]));return doc;
 }
 [TestMethod]
 public Task NativeImagesAndListsRoundTripMergedCellsAndRtf()=>RunSta(async()=>
 {
  var doc=Fixture();var table=doc.Blocks.OfType<Table>().Single();PlanningTableEditing.Merge(table,[table.RowGroups[0].Rows[1].Cells[0],table.RowGroups[0].Rows[1].Cells[1]]);
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");try{
   PlanningDocxAdapter.Export(doc,path);using(var pkg=WordprocessingDocument.Open(path,false)){Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(pkg),string.Join(" | ",new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(pkg).Select(e=>e.Description+" "+e.Path?.XPath)));Assert.HasCount(6,pkg.MainDocumentPart!.ImageParts);Assert.HasCount(12,pkg.MainDocumentPart.Document.Descendants<W.NumberingProperties>());Assert.HasCount(1,pkg.MainDocumentPart.Document.Descendants<W.GridSpan>());}
   var imported=PlanningDocxAdapter.Import(path).Document;var back=imported.Blocks.OfType<Table>().Single();Assert.AreEqual(2,back.RowGroups[0].Rows[1].Cells[0].ColumnSpan);Assert.HasCount(6,PlanningTextSearch.Paragraphs(back.RowGroups[0].Rows[1].Cells[0].Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>().Concat(back.RowGroups[0].Rows.Skip(2).SelectMany(r=>PlanningTextSearch.Paragraphs(r.Cells[0].Blocks)).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>()));
   Assert.AreEqual(TextMarkerStyle.Decimal,back.RowGroups[0].Rows[2].Cells[2].Blocks.OfType<List>().Single().MarkerStyle);Assert.AreEqual(3,back.RowGroups[0].Rows[2].Cells[2].Blocks.OfType<List>().Single().StartIndex);
   using var rtf=new MemoryStream();new TextRange(imported.ContentStart,imported.ContentEnd).Save(rtf,DataFormats.Rtf);var restored=PlanningDocumentLayout.Load(Convert.ToBase64String(rtf.ToArray()),PlanningDocumentLayout.Capture(imported,new(),[]));PlanningDocxAdapter.Export(restored,path);Assert.HasCount(6,PlanningDocxAdapter.Import(path).Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows.Skip(1).SelectMany(r=>r.Cells).SelectMany(c=>c.Blocks.OfType<List>()));
  }finally{File.Delete(path);}await Task.CompletedTask;
 });
 [TestMethod]
 public Task InsertImageUsesCellCaretUndoAndReopen()=>RunSta(async()=>
 {
  var doc=Fixture(1);using var stream=new MemoryStream();new TextRange(doc.ContentStart,doc.ContentEnd).Save(stream,DataFormats.Rtf);string? saved=null;PlanningRichLayout? layout=null;
  var editor=new PlanningRichEditor(Convert.ToBase64String(stream.ToArray()),"",(rtf,_,l)=>{saved=rtf;layout=l;},[],PlanningDocumentLayout.Capture(doc,new(),[]));var box=editor.Children.OfType<RichTextBox>().Single();var cell=box.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[1];box.CaretPosition=cell.ContentStart.GetInsertionPosition(LogicalDirection.Forward)!;editor.InsertReferenceImageUndoable(PlanningRichFlowFixture.ReferencePng(1));Assert.IsTrue(PlanningTextSearch.Paragraphs(cell.Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>().Any(),System.Windows.Markup.XamlWriter.Save(box.Document));
  System.Windows.Input.ApplicationCommands.Undo.Execute(null,editor);var undone=box.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[1];Assert.IsFalse(PlanningTextSearch.Paragraphs(undone.Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>().Any());System.Windows.Input.ApplicationCommands.Redo.Execute(null,editor);editor.FlushPending();var reopened=PlanningDocumentLayout.Load(saved!,layout);Assert.IsTrue(PlanningTextSearch.Paragraphs(reopened.Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>().Count()>=2);await Task.CompletedTask;
 });

 [TestMethod]
 public Task StoryboardImagesListsPaginatePrintPdfAndSaveReopen()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await PlanningRichFlowFixture.Populate(f);var doc=Fixture(24);PlanningRichFlowFixture.SaveProposal(f.Vm,doc);Assert.IsTrue(await f.Vm.FlushAsync());
  var reopened=f.NewVm();await reopened.LoadAsync(f.Id);Assert.HasCount(24,reopened.Shots);Assert.HasCount(22,reopened.ResourceCatalog.Items);
  FlowDocument Load()=>PlanningDocumentLayout.Load(reopened.RichTextPackage!,reopened.RichLayout);
  var restored=Load();Assert.HasCount(2,Flat(((Paragraph)restored.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[1].Blocks.FirstBlock).Inlines).OfType<LineBreak>());Assert.HasCount(24,PlanningTextSearch.Paragraphs(restored.Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>());
  var paginator=new PlanningPaginator(restored);Assert.IsGreaterThan(1,paginator.PageCount);Assert.AreEqual(restored.Blocks.OfType<Table>().Count(),paginator.PageCount,"rows="+string.Join(",",restored.Blocks.OfType<Table>().Select(t=>t.RowGroups[0].Rows.Count)));
  Assert.HasCount(24,PlanningTextSearch.Paragraphs(restored.Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>());
  foreach(var img in PlanningTextSearch.Paragraphs(restored.Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>().Select(i=>(Image)i.Child))Assert.IsNotNull(img.Source);
  var output=Path.Combine(Environment.GetEnvironmentVariable("KITAO_TABLE_EVIDENCE")??f.Root,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
  var xps=Path.Combine(output,"storyboard.xps");using(var package=new System.Windows.Xps.Packaging.XpsDocument(xps,FileAccess.ReadWrite)){System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(package).Write(paginator);}
  using(var zip=System.IO.Compression.ZipFile.OpenRead(xps)){Assert.AreEqual(paginator.PageCount,zip.Entries.Count(e=>e.FullName.EndsWith(".fpage",StringComparison.OrdinalIgnoreCase)));Assert.IsTrue(zip.Entries.Any(e=>e.FullName.EndsWith(".png",StringComparison.OrdinalIgnoreCase)));}
  PlanningProposalPdf.ExportDocument(Load(),Path.Combine(output,"storyboard.pdf"),96);
  var path=Path.Combine(output,"storyboard.docx");Assert.IsTrue(await reopened.ExportPlanningDocxAsync(path));var review=reopened.PreviewPlanningDocxImport(path);Assert.IsTrue(await reopened.ApplyPlanningDocxImportAsync(review,[],true));Assert.HasCount(24,reopened.Shots);
  if(Environment.GetEnvironmentVariable("KITAO_TABLE_EVIDENCE") is not null)
  {
   var view=new PlanningCenterView{DataContext=reopened};await reopened.SwitchPlanningModeAsync(true);
   void Image(FrameworkElement element,string name,int width,int height){element.Width=width;element.Height=height;element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(element);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,name));png.Save(stream);}
   Image(view,"table-wide.png",1720,1000);Image(view,"table-narrow.png",900,900);view.DataContext=null;
   for(var n=0;n<Math.Min(paginator.PageCount,3);n++)Image(new System.Windows.Controls.Primitives.DocumentPageView{DocumentPaginator=paginator,PageNumber=n},$"table-page-{n+1}.png",794,1123);
   await File.WriteAllTextAsync(Path.Combine(output,"table-flow.json"),System.Text.Json.JsonSerializer.Serialize(new{shots=24,resources=22,images=24,pages=paginator.PageCount,pdf="IMAGE_PDF",windows="NOT_RUN",wps="NOT_RUN"}));
  }
 });
 [TestMethod]
 public Task TooTallLaterRowAndNestedTableAreRejectedWithoutChangingDestination()=>RunSta(async()=>
 {
  var doc=Fixture(2);var cell=doc.Blocks.OfType<Table>().Single().RowGroups[0].Rows[2].Cells[0];((Image)Flat(((Paragraph)cell.Blocks.FirstBlock).Inlines).OfType<InlineUIContainer>().Single().Child).Height=2000;
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");await File.WriteAllTextAsync(path,"original");try{Assert.Throws<NotSupportedException>(()=>PlanningProposalPdf.ExportDocument(doc,path,96));Assert.AreEqual("original",await File.ReadAllTextAsync(path));}finally{File.Delete(path);}
 });

 [TestMethod]
 public Task CellListButtonsEditUndoAndVerticalMergeKeepsImagePixels()=>RunSta(async()=>
 {
  EnsureTestApplication();var editor=new PlanningRichEditor(null,"",(_,_,_)=>{},[]);var box=editor.Children.OfType<RichTextBox>().Single();box.Document.Blocks.Clear();box.Document.Blocks.Add(Fixture(2).Blocks.FirstBlock);var table=box.Document.Blocks.OfType<Table>().Single();var cell=table.RowGroups[0].Rows[1].Cells[1];box.CaretPosition=cell.ContentStart.GetInsertionPosition(LogicalDirection.Forward)!;
  var tools=editor.Children.OfType<WrapPanel>().Single();tools.Children.OfType<Button>().Single(b=>Equals(b.Content,"编号列表")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.HasCount(1,cell.Blocks.OfType<List>());Assert.AreEqual(TextMarkerStyle.Decimal,cell.Blocks.OfType<List>().Single().MarkerStyle);
  System.Windows.Input.ApplicationCommands.Undo.Execute(null,editor);Assert.IsFalse(box.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[1].Blocks.OfType<List>().Any());
  var doc=Fixture(2);table=doc.Blocks.OfType<Table>().Single();var upper=table.RowGroups[0].Rows[1].Cells[0];var lower=table.RowGroups[0].Rows[2].Cells[0];PlanningTableEditing.Merge(table,[upper,lower]);
  byte[] Pixels(BitmapSource b){var formatted=new FormatConvertedBitmap(b,System.Windows.Media.PixelFormats.Bgra32,null,0);var bytes=new byte[b.PixelWidth*b.PixelHeight*4];formatted.CopyPixels(bytes,b.PixelWidth*4,0);return bytes;}
  var original=PlanningTextSearch.Paragraphs(upper.Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>().Select(i=>Pixels((BitmapSource)((Image)i.Child).Source)).ToArray();
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");try{PlanningDocxAdapter.Export(doc,path);using(var pkg=WordprocessingDocument.Open(path,false)){Assert.HasCount(2,pkg.MainDocumentPart!.Document.Descendants<W.VerticalMerge>());Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(pkg));}
   var restored=PlanningDocxAdapter.Import(path).Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[0];Assert.AreEqual(2,restored.RowSpan);var images=PlanningTextSearch.Paragraphs(restored.Blocks).SelectMany(p=>Flat(p.Inlines)).OfType<InlineUIContainer>().ToArray();Assert.HasCount(2,images);for(var n=0;n<2;n++)CollectionAssert.AreEqual(original[n],Pixels((BitmapSource)((Image)images[n].Child).Source));
  }finally{File.Delete(path);}await Task.CompletedTask;
 });
}
