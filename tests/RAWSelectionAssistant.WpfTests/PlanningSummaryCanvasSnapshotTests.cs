using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Buffers.Binary;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using W=DocumentFormat.OpenXml.Wordprocessing;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class PlanningSummaryCanvasSnapshotTests
{
    private static string Root=>Environment.GetEnvironmentVariable("KITAO_SUMMARY_CANVAS_ROOT")??throw new InvalidOperationException("Explicit isolated summary output root required");
    private static byte[] Png(int width,int height,int variant,bool alpha=false)
    {
        var data=new byte[width*height*4];
        for(var row=0;row<height;row++)for(var column=0;column<width;column++)
        {
            var offset=(row*width+column)*4;data[offset]=(byte)(variant==0?40:205);data[offset+1]=(byte)(column<width/2?95:190);data[offset+2]=(byte)(row<height/2?230:60);data[offset+3]=(byte)(alpha?column*255/Math.Max(1,width-1):255);
        }
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,data,width*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var memory=new MemoryStream();encoder.Save(memory);return memory.ToArray();
    }
    private static PlanningCanvasRevision Revision(Guid project,Guid canvas,byte[] png,int width,int height,int ordinal)=>new(
        Guid.Parse(ordinal==1?"11111111-1111-4111-8111-111111111111":"22222222-2222-4222-8222-222222222222"),canvas,
        new DateTimeOffset(2026,11,2,9+ordinal,0,0,TimeSpan.FromHours(8)),
        new CanvasDocument{ProjectId=project,CanvasId=canvas,PixelWidth=width,PixelHeight=height,Name="团队拼贴",Objects=[new(){CanvasId=canvas,SourcePath=Path.Combine(Root,"offline-original-photo.jpg"),Width=100,Height=100}]},Convert.ToBase64String(png));
    [TestMethod]
    public Task SavedTwoRevisionSummaryEmbedsFrozenPngsInDocumentOrderWithoutReadingSources()=>RunSta(async()=>
    {
        EnsureTestApplication();Directory.CreateDirectory(Root);using var fixture=await PlanningFoundationWpfTests.Fixture.Create();await fixture.Vm.LoadAsync(fixture.Id);fixture.Vm.DocumentTitle="固定快照导出测试";Assert.IsTrue(await fixture.Vm.FlushAsync());
        fixture.Vm.DocumentTitle="团队阅读固定快照样例";fixture.Vm.DocumentBody="本样例仅含可分发合成拼贴。固定快照按保存顺序交付，不随当前画布修改。";Assert.IsTrue(await fixture.Vm.FlushAsync());
        var canvas=Guid.NewGuid();var firstBytes=Png(640,360,0,true);var secondBytes=Png(240,600,1);
        var first=Revision(fixture.Id,canvas,firstBytes,640,360,1) with{RenderVersion=3,SamplingContract=PlanningCanvasSnapshot.SamplingContract,PreviewParameters=PlanningCanvasSnapshot.PreviewParameters,PreviewWidth=640,PreviewHeight=360};
        var second=Revision(fixture.Id,canvas,secondBytes,240,600,2) with{RenderVersion=2,SamplingContract="CanvasPremultipliedBilinear.ArtboardCenters.v2",PreviewWidth=240,PreviewHeight=600};
        await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId!.Value,first);await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId.Value,second);
        var store=new CanvasDocumentStore(Path.Combine(fixture.Root,"canvas"));await store.SaveAsync(second.EditableSnapshot with{Name="当前画布后来修改",Objects=[]});await fixture.Vm.LoadAsync(fixture.Id);
        var persisted=Path.Combine(fixture.Planning.StorageRoot,fixture.Id.ToString("N")+".planning.json");var before=File.ReadAllBytes(persisted);var canvasFile=Directory.GetFiles(store.DirectoryPath,"*.json").Single();var canvasBefore=File.ReadAllBytes(canvasFile);
        var path=Path.Combine(Root,"saved-two-revisions.docx");
        var dialogs=new SnapshotDialogs{Output=path};var viewModel=new RAWSelectionAssistant.ViewModels.PlanningCenterViewModel(fixture.Projects,fixture.Bookings,dialogs,fixture.Shots,fixture.Planning);await viewModel.LoadAsync(fixture.Id);
        var view=new RAWSelectionAssistant.Views.PlanningCenterView{DataContext=viewModel};view.Measure(new Size(1400,900));view.Arrange(new Rect(0,0,1400,900));view.UpdateLayout();
        var export=typeof(RAWSelectionAssistant.Views.PlanningCenterView).GetMethod("ExportPlanningSummaryFromViewAsync",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;await (Task)export.Invoke(view,null)!;Assert.IsEmpty(dialogs.Errors);Assert.AreEqual(1,dialogs.Infos.Count);view.DataContext=null;
        CollectionAssert.AreEqual(before,File.ReadAllBytes(persisted));CollectionAssert.AreEqual(canvasBefore,File.ReadAllBytes(canvasFile));
        using var package=WordprocessingDocument.Open(path,false);var main=package.MainDocumentPart!;var media=main.ImageParts.ToArray();
        await File.WriteAllTextAsync(Path.Combine(Root,"DOCX_INSPECTION.json"),JsonSerializer.Serialize(new{mediaCount=media.Length,drawingCount=main.Document.Descendants<W.Drawing>().Count(),relationships=main.Parts.Select(part=>new{part.RelationshipId,uri=part.OpenXmlPart.Uri.ToString()}),sourceOffline=!File.Exists(first.EditableSnapshot.Objects[0].SourcePath)},new JsonSerializerOptions{WriteIndented=true}));
        Assert.AreEqual(2,media.Length,"Saved fixed snapshots must be actual DOCX images, not metadata-only references");
        Assert.IsEmpty(new OpenXmlValidator().Validate(package));
        var expected=new[]{firstBytes,secondBytes};var drawings=main.Document.Descendants<W.Drawing>().ToArray();Assert.AreEqual(2,drawings.Length);
        for(var index=0;index<drawings.Length;index++)
        {
            var xml=System.Xml.Linq.XElement.Parse(drawings[index].OuterXml);var relationship=xml.Descendants().Single(element=>element.Name.LocalName=="blip").Attributes().Single(attribute=>attribute.Name.LocalName=="embed").Value;
            using var stream=((ImagePart)main.GetPartById(relationship)).GetStream();using var output=new MemoryStream();stream.CopyTo(output);CollectionAssert.AreEqual(Pixels(expected[index]),Pixels(output.ToArray()));
            var extent=xml.Descendants().First(element=>element.Name.LocalName=="extent");var width=long.Parse(extent.Attribute("cx")!.Value)/9525d;var height=long.Parse(extent.Attribute("cy")!.Value)/9525d;
            Assert.IsTrue(width<=520&&height<=360);Assert.AreEqual(index==0?640d/360:240d/600,width/height,.001);
        }
        var text=main.Document.Body!.InnerText;Assert.IsTrue(text.IndexOf(first.RevisionId.ToString(),StringComparison.Ordinal)<text.IndexOf(second.RevisionId.ToString(),StringComparison.Ordinal));
        Assert.AreEqual(15,main.Document.Body.Elements<W.Paragraph>().Count(paragraph=>paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value=="Heading1"));
        Assert.AreEqual(2,fixture.Vm.DocumentCanvasRevisions.Count);Assert.AreEqual(first.PreviewPngBase64,fixture.Vm.DocumentCanvasRevisions[0].PreviewPngBase64);
    });
    [TestMethod]
    public Task MissingCorruptUnsupportedAndOverBudgetSnapshotsHavePlaceholdersAndWarnings()=>RunSta(async()=>
    {
        EnsureTestApplication();Directory.CreateDirectory(Root);using var fixture=await PlanningFoundationWpfTests.Fixture.Create();await fixture.Vm.LoadAsync(fixture.Id);fixture.Vm.DocumentTitle="固定快照导出测试";Assert.IsTrue(await fixture.Vm.FlushAsync());
        var basis=Revision(fixture.Id,Guid.NewGuid(),Png(64,48,0),64,48,1);var oversize=Png(64,48,0);BinaryPrimitives.WriteUInt32BigEndian(oversize.AsSpan(16,4),16384);BinaryPrimitives.WriteUInt32BigEndian(oversize.AsSpan(20,4),16384);
        var values=new string?[]{null,"not base64",Convert.ToBase64String(new byte[40]),Convert.ToBase64String(oversize),new('A',PlanningSummaryOwnedImage.MaximumSnapshotBase64Characters+1)};
        var ids=new List<Guid>();
        foreach(var value in values)
        {
            var revision=basis with{RevisionId=Guid.NewGuid(),PreviewPngBase64=value};ids.Add(revision.RevisionId);
            if(value?.Length>32*1024*1024)continue;
            await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId!.Value,revision);
        }
        await fixture.Vm.LoadAsync(fixture.Id);var path=Path.Combine(Root,"snapshot-placeholders.docx");var result=await fixture.Vm.ExportPlanningSummaryDocxAsync(path);Assert.IsNotNull(result);Assert.AreEqual(4,result.Warnings.Count);
        using(var package=WordprocessingDocument.Open(path,false))
        {
            Assert.IsEmpty(package.MainDocumentPart!.ImageParts);var text=package.MainDocumentPart.Document.Body!.InnerText;
            foreach(var id in ids.Take(4))StringAssert.Contains(text,id.ToString());
            Assert.AreEqual(4,text.Split("[画布固定快照未呈现：").Length-1);StringAssert.Contains(text,"未读取当前画布");Assert.IsEmpty(new OpenXmlValidator().Validate(package));
        }
        var tooLarge=basis with{PreviewPngBase64=values[4]};Assert.ThrowsExactly<InvalidDataException>(()=>PlanningSummaryOwnedImage.ReadSnapshot(tooLarge,fixture.Id,400,500));
        var entry=new PlanningSummaryEntry("编码超预算的既有快照",[]){CanvasRevision=tooLarge};var projection=new PlanningReadableSummary(fixture.Id,fixture.Vm.PlanningDocumentId,[new("文档内容",[entry])]);
        var document=new PlanningDocument().EnsureIdentity(fixture.Id);var bounded=PlanningSummaryDocument.Export(projection,document,Path.Combine(Root,"encoded-budget-placeholder.docx"));Assert.AreEqual(1,bounded.Warnings.Count);
        var header=Png(16,16,0);header[24]=16;Assert.ThrowsExactly<NotSupportedException>(()=>PlanningSummaryOwnedImage.ReadSnapshot(basis with{PreviewPngBase64=Convert.ToBase64String(header)},fixture.Id,500,600));
        var truncated=basis with{RevisionId=Guid.NewGuid(),RenderVersion=3,SamplingContract=PlanningCanvasSnapshot.SamplingContract,PreviewParameters=PlanningCanvasSnapshot.PreviewParameters,PreviewWidth=64,PreviewHeight=48,PreviewPngBase64=Convert.ToBase64String(Png(64,48,0)[..40])};
        await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId!.Value,truncated);await fixture.Vm.LoadAsync(fixture.Id);
        var truncatedResult=await fixture.Vm.ExportPlanningSummaryDocxAsync(Path.Combine(Root,"corrupt-saved-v3.docx"));Assert.IsNotNull(truncatedResult);Assert.AreEqual(5,truncatedResult.Warnings.Count);
        await File.WriteAllTextAsync(Path.Combine(Root,"PLACEHOLDER_WARNINGS.json"),JsonSerializer.Serialize(new{warnings=result.Warnings,encodedOverBudget=bounded.Warnings,maximumBase64Characters=PlanningSummaryOwnedImage.MaximumSnapshotBase64Characters,maximumBytes=PlanningSummaryOwnedImage.MaximumSnapshotBytes,maximumPixels=PlanningSummaryOwnedImage.MaximumSnapshotPixels},new JsonSerializerOptions{WriteIndented=true}));
    });
    [TestMethod]
    public Task SnapshotDecodesAreDeferredAndPageBoundsRespectActualContentArea()=>RunSta(async()=>
    {
        EnsureTestApplication();Directory.CreateDirectory(Root);var project=Guid.NewGuid();var revision=Revision(project,Guid.NewGuid(),Png(300,900,1,true),300,900,1);
        var source=new PlanningDocument().EnsureIdentity(project);source=source with{Package=source.Package! with{CanvasRevisions=[revision],RichLayout=new(new(500,700,80,100,90,110),[],[])}};
        var projection=PlanningReadableSummaryBuilder.Build(new(project,"窄页竖图",source,new(),[]));var warnings=new List<string>();var flow=PlanningSummaryDocument.Create(projection,source,warnings);
        var images=flow.Blocks.OfType<Paragraph>().SelectMany(paragraph=>paragraph.Inlines.OfType<InlineUIContainer>()).Select(inline=>inline.Child).OfType<Image>().ToArray();Assert.AreEqual(1,images.Length);Assert.IsNull(images[0].Source,"Projection must not eagerly allocate snapshot pixel buffers");
        var image=PlanningSummaryOwnedImage.ReadSnapshot(revision,project,330,490);Assert.IsTrue(image.Width<=330);Assert.IsTrue(image.Height<=490*.42);Assert.AreEqual(1d/3,image.Width/image.Height,.001);
        PlanningDocxAdapter.Export(flow,Path.Combine(Root,"narrow-portrait.docx"));Assert.IsNull(images[0].Source,"DOCX adapter must release each transient decoded source after writing");
        var repeated=revision with{RevisionId=Guid.NewGuid(),PreviewPngBase64=Convert.ToBase64String(Png(32,24,0))};var revisions=Enumerable.Range(0,8).Select(_=>repeated with{RevisionId=Guid.NewGuid()}).ToArray();
        var multiple=source with{Package=source.Package! with{CanvasRevisions=revisions}};var projected=PlanningReadableSummaryBuilder.Build(new(project,"逐图释放验证",multiple,new(),[]));
        var deferred=PlanningSummaryDocument.Create(projected,multiple,[]);var slots=deferred.Blocks.OfType<Paragraph>().SelectMany(paragraph=>paragraph.Inlines.OfType<InlineUIContainer>()).Select(inline=>inline.Child).OfType<Image>().ToArray();Assert.AreEqual(8,slots.Length);Assert.IsTrue(slots.All(slot=>slot.Source is null));
        PlanningDocxAdapter.Export(deferred,Path.Combine(Root,"sequential-eight.docx"));Assert.IsTrue(slots.All(slot=>slot.Source is null));
        await Task.CompletedTask;
    });
    [TestMethod]
    public Task SnapshotSummaryFailureNeverOverwritesExistingOutputOrLeavesStages()=>RunSta(async()=>
    {
        EnsureTestApplication();Directory.CreateDirectory(Root);using var fixture=await PlanningFoundationWpfTests.Fixture.Create();await fixture.Vm.LoadAsync(fixture.Id);fixture.Vm.DocumentTitle="固定快照导出测试";Assert.IsTrue(await fixture.Vm.FlushAsync());
        var revision=Revision(fixture.Id,Guid.NewGuid(),Png(32,24,0),32,24,1);await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId!.Value,revision);await fixture.Vm.LoadAsync(fixture.Id);
        var existing=Path.Combine(Root,"must-preserve.docx");await File.WriteAllTextAsync(existing,"previous unchanged document");var before=File.ReadAllBytes(existing);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(()=>fixture.Vm.ExportPlanningSummaryDocxAsync(existing));CollectionAssert.AreEqual(before,File.ReadAllBytes(existing));
        var projection=new PlanningReadableSummary(fixture.Id,fixture.Vm.PlanningDocumentId,[new("文档内容",[new("固定版",[]){CanvasRevision=revision}])]);
        Assert.ThrowsExactly<IOException>(()=>PlanningSummaryDocument.Export(projection,new PlanningDocument().EnsureIdentity(fixture.Id),existing));CollectionAssert.AreEqual(before,File.ReadAllBytes(existing));
        var blocked=Path.Combine(Root,"destination-is-directory.docx");Directory.CreateDirectory(blocked);Assert.Throws<IOException>(()=>PlanningSummaryDocument.Export(projection,new PlanningDocument().EnsureIdentity(fixture.Id),blocked));
        Assert.IsEmpty(Directory.GetFiles(Root,"*.summary-stage.docx"));Assert.IsEmpty(Directory.GetFiles(Root,"*.tmp"));
    });
    [TestMethod]
    public Task ProjectSwitchOrRevisionChangeWhileReadingNeverPublishesMixedSummary()=>RunSta(async()=>
    {
        EnsureTestApplication();Directory.CreateDirectory(Root);using var fixture=await PlanningFoundationWpfTests.Fixture.Create();await fixture.Vm.LoadAsync(fixture.Id);fixture.Vm.DocumentTitle="固定快照导出测试";Assert.IsTrue(await fixture.Vm.FlushAsync());
        var revision=Revision(fixture.Id,Guid.NewGuid(),Png(20,15,0),20,15,1);await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId!.Value,revision);await fixture.Vm.LoadAsync(fixture.Id);
        var changedPath=Path.Combine(Root,"changed-during-read.docx");fixture.Vm.ReadPlanningExpenses=async _=>
        {
            await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId!.Value,revision with{RevisionId=Guid.NewGuid()});await fixture.Vm.LoadAsync(fixture.Id);return [];
        };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(()=>fixture.Vm.ExportPlanningSummaryDocxAsync(changedPath));Assert.IsFalse(File.Exists(changedPath));
        var other=Guid.NewGuid();await fixture.Projects.UpsertAsync(new RAWSelectionAssistant.Core.Models.PhotoProjectRecord{Id=other,Name="另一个项目"});
        fixture.Vm.ReadPlanningExpenses=async _=>{await fixture.Vm.LoadAsync(other);return [];};
        var switchedPath=Path.Combine(Root,"switched-during-read.docx");await Assert.ThrowsExactlyAsync<InvalidOperationException>(()=>fixture.Vm.ExportPlanningSummaryDocxAsync(switchedPath));Assert.IsFalse(File.Exists(switchedPath));Assert.AreEqual(other,fixture.Vm.ProjectId);
        Assert.IsEmpty(Directory.GetFiles(Root,"*.summary-stage.docx"));
    });
    private static byte[] Pixels(byte[] png)
    {
        using var bytes=new MemoryStream(png);var bitmap=new FormatConvertedBitmap(BitmapDecoder.Create(bytes,BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0],PixelFormats.Bgra32,null,0);var result=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(result,bitmap.PixelWidth*4,0);return result;
    }
    [TestMethod]
    public Task MissingRichAndLightRevisionLinksWarnInPlaceWithoutExportingStaleRtfImages()=>RunSta(async()=>
    {
        EnsureTestApplication();Directory.CreateDirectory(Root);using var fixture=await PlanningFoundationWpfTests.Fixture.Create();await fixture.Vm.LoadAsync(fixture.Id);fixture.Vm.DocumentTitle="失效关联反馈";Assert.IsTrue(await fixture.Vm.FlushAsync());
        var canvas=Guid.NewGuid();var valid=Revision(fixture.Id,canvas,Png(40,30,0),40,30,1);await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId!.Value,valid);await fixture.Vm.LoadAsync(fixture.Id);
        var missingBody=Guid.Parse("33333333-3333-4333-8333-333333333333");var missingLight=Guid.Parse("44444444-4444-4444-8444-444444444444");var missingBlock=Guid.Parse("55555555-5555-4555-8555-555555555555");var missingTable=Guid.Parse("66666666-6666-4666-8666-666666666666");
        var rich=new FlowDocument();rich.Blocks.Add(new Paragraph(new Run("正文前段保留")));
        Image StaleImage(){using var stream=new MemoryStream(Png(24,24,1));var bitmap=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];bitmap.Freeze();return new Image{Source=bitmap,Width=24,Height=24};}
        var invalid=new Paragraph(new Run("失效位置前文")){Tag=new PlanningBlockSemantic(1,CanvasId:canvas,CanvasRevisionId:missingBody)};invalid.Inlines.Add(new Span(new InlineUIContainer(StaleImage())));invalid.Inlines.Add(new Run("失效位置后文"));rich.Blocks.Add(invalid);
        rich.Blocks.Add(new BlockUIContainer(StaleImage()){Tag=new PlanningBlockSemantic(2,CanvasId:canvas,CanvasRevisionId:missingBlock)});
        var table=new Table{Tag=new PlanningBlockSemantic(3,CanvasId:canvas,CanvasRevisionId:missingTable)};table.Columns.Add(new TableColumn());var group=new TableRowGroup();table.RowGroups.Add(group);var row=new TableRow();group.Rows.Add(row);var cellContent=new Paragraph(new Run("表格正文保留"));cellContent.Inlines.Add(new InlineUIContainer(StaleImage()));row.Cells.Add(new TableCell(cellContent));rich.Blocks.Add(table);
        rich.Blocks.Add(new BlockUIContainer(StaleImage()){Tag=new PlanningBlockSemantic(4,CanvasId:canvas,CanvasRevisionId:valid.RevisionId)});rich.Blocks.Add(new Paragraph(new Run("正文末段保留")));
        var layout=PlanningDocumentLayout.Capture(rich,new(),[]);fixture.Vm.SaveRichText(PlanningDocumentImages.Serialize(rich),new TextRange(rich.ContentStart,rich.ContentEnd).Text,layout);Assert.IsTrue(await fixture.Vm.FlushAsync());
        Assert.IsTrue(await fixture.Vm.SaveResourceAsync(new PlanningLight{Name="失效灯位位置",CanvasRevisionId=missingLight}));Assert.IsTrue(await fixture.Vm.SaveResourceAsync(new PlanningLight{Name="有效灯位保留",CanvasRevisionId=valid.RevisionId}));
        var source=Path.Combine(fixture.Planning.StorageRoot,fixture.Id.ToString("N")+".planning.json");var before=File.ReadAllBytes(source);var path=Path.Combine(Root,"missing-revision-links.docx");var dialogs=await ExportViaView(fixture,path);
        using var package=WordprocessingDocument.Open(path,false);var text=package.MainDocumentPart!.Document.Body!.InnerText;var drawings=package.MainDocumentPart.ImageParts.ToArray();
        await File.WriteAllTextAsync(Path.Combine(Root,"MISSING_LINK_REPRO.json"),JsonSerializer.Serialize(new{warnings=dialogs.Infos,errors=dialogs.Errors,mediaCount=drawings.Length,missingBodyReported=text.Contains("[画布固定快照未呈现：版本 "+missingBody),missingLightReported=text.Contains("[画布固定快照未呈现：版本 "+missingLight),hasWarningAggregate=text.Contains("导出警告（保留原资料，不替换源文件）")},new JsonSerializerOptions{WriteIndented=true}));
        StringAssert.Contains(text,"[画布固定快照未呈现：版本 "+missingBody);StringAssert.Contains(text,"[画布固定快照未呈现：版本 "+missingLight);StringAssert.Contains(text,"[画布固定快照未呈现：版本 "+missingBlock);StringAssert.Contains(text,"[画布固定快照未呈现：版本 "+missingTable);StringAssert.Contains(text,"表格正文保留");
        Assert.IsTrue(text.IndexOf("失效位置前文",StringComparison.Ordinal)<text.IndexOf("[画布固定快照未呈现：版本 "+missingBody,StringComparison.Ordinal));Assert.IsTrue(text.IndexOf("[画布固定快照未呈现：版本 "+missingBody,StringComparison.Ordinal)<text.IndexOf("失效位置后文",StringComparison.Ordinal));
        StringAssert.Contains(text,"正文前段保留");StringAssert.Contains(text,"正文末段保留");StringAssert.Contains(text,"有效灯位保留");Assert.AreEqual(1,drawings.Length,"Only the valid saved revision may be embedded; stale RTF is not a substitute");
        using var raw=drawings[0].GetStream();using var bytes=new MemoryStream();raw.CopyTo(bytes);CollectionAssert.AreEqual(Pixels(Convert.FromBase64String(valid.PreviewPngBase64!)),Pixels(bytes.ToArray()));
        StringAssert.Contains(dialogs.Infos.Single(),"4项图片/固定快照警告");CollectionAssert.AreEqual(before,File.ReadAllBytes(source));Assert.IsEmpty(new OpenXmlValidator().Validate(package));
    });
    [TestMethod]
    public Task DeferredSnapshotWarningsMatchPlaceholdersReturnedWarningsAndDocxSummary()=>RunSta(async()=>
    {
        EnsureTestApplication();Directory.CreateDirectory(Root);using var fixture=await PlanningFoundationWpfTests.Fixture.Create();await fixture.Vm.LoadAsync(fixture.Id);fixture.Vm.DocumentTitle="延迟警告一致性";Assert.IsTrue(await fixture.Vm.FlushAsync());
        var broken=Revision(fixture.Id,Guid.NewGuid(),Png(24,18,0),24,18,1) with{PreviewPngBase64="invalid base64"};await fixture.Planning.AppendCanvasRevisionAsync(fixture.Id,fixture.Vm.PlanningDocumentId!.Value,broken);await fixture.Vm.LoadAsync(fixture.Id);
        var path=Path.Combine(Root,"deferred-warning-summary.docx");var dialogs=await ExportViaView(fixture,path);var result=await fixture.Vm.ExportPlanningSummaryDocxAsync(Path.Combine(Root,"deferred-warning-result.docx"));Assert.IsNotNull(result);Assert.AreEqual(1,result.Warnings.Count);
        using var package=WordprocessingDocument.Open(path,false);var text=package.MainDocumentPart!.Document.Body!.InnerText;var marker="导出警告（保留原资料，不替换源文件）";
        await File.WriteAllTextAsync(Path.Combine(Root,"DEFERRED_WARNING_REPRO.json"),JsonSerializer.Serialize(new{returnedWarnings=result.Warnings.Count,placeholderCount=text.Split("[画布固定快照未呈现：").Length-1,hasAggregate=text.Contains(marker)},new JsonSerializerOptions{WriteIndented=true}));
        StringAssert.Contains(text,marker);var parts=text.Split(marker);Assert.AreEqual(2,parts.Length);StringAssert.Contains(parts[0],result.Warnings[0]);StringAssert.Contains(parts[1],result.Warnings[0]);Assert.AreEqual(2,text.Split(result.Warnings[0]).Length-1);StringAssert.Contains(dialogs.Infos.Single(),"1项图片/固定快照警告");
    });
    private static async Task<SnapshotDialogs> ExportViaView(PlanningFoundationWpfTests.Fixture fixture,string path)
    {
        var dialogs=new SnapshotDialogs{Output=path};var viewModel=new RAWSelectionAssistant.ViewModels.PlanningCenterViewModel(fixture.Projects,fixture.Bookings,dialogs,fixture.Shots,fixture.Planning);await viewModel.LoadAsync(fixture.Id);
        var view=new RAWSelectionAssistant.Views.PlanningCenterView{DataContext=viewModel};view.Measure(new Size(1400,900));view.Arrange(new Rect(0,0,1400,900));view.UpdateLayout();
        var method=typeof(RAWSelectionAssistant.Views.PlanningCenterView).GetMethod("ExportPlanningSummaryFromViewAsync",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;try{await (Task)method.Invoke(view,null)!;Assert.IsEmpty(dialogs.Errors);Assert.IsTrue(File.Exists(path));return dialogs;}finally{view.DataContext=null;}
    }
    private sealed class SnapshotDialogs:IDialogService
    {
        public required string Output;public List<string> Infos=[];public List<string> Errors=[];
        public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect)=>[];
        public string? ChooseFolder(string title,string? initialDirectory=null)=>null;
        public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>Output;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;
        public void ShowInfo(string message)=>Infos.Add(message);public void ShowError(string message)=>Errors.Add(message);
        public bool Confirm(string message,string title)=>true;public RAWSelectionAssistant.Services.HelpAction ShowHelp()=>RAWSelectionAssistant.Services.HelpAction.None;public void ShowFeedback(){}
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates)=>null;
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
    }
    [TestMethod]
    public Task ExplicitRichBlockAndLightPlacementUseFrozenRevisionInsteadOfEmbeddedStaleImage()=>RunSta(async()=>
    {
        EnsureTestApplication();Directory.CreateDirectory(Root);var project=Guid.NewGuid();var canvas=Guid.NewGuid();var firstBytes=Png(64,48,0);var secondBytes=Png(48,64,1);
        var first=Revision(project,canvas,firstBytes,64,48,1);var second=Revision(project,canvas,secondBytes,48,64,2);var rich=new FlowDocument();rich.Blocks.Add(new Paragraph(new Run("富文本前段")));
        using var png=new MemoryStream(Png(12,12,1));var bitmap=BitmapDecoder.Create(png,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];bitmap.Freeze();rich.Blocks.Add(new Paragraph(new InlineUIContainer(new Image{Source=bitmap,Width=12,Height=12})));rich.Blocks.Add(new Paragraph(new Run("富文本后段")));
        using var rtf=new MemoryStream();new TextRange(rich.ContentStart,rich.ContentEnd).Save(rtf,DataFormats.Rtf);
        var document=new PlanningDocument{Title="明确放置关系",Body="正文"}.EnsureIdentity(project);document=document with{Package=document.Package! with{RichTextPackageBase64=Convert.ToBase64String(rtf.ToArray()),RichLayout=new(new(),[new(1,CanvasId:canvas,CanvasRevisionId:first.RevisionId)],[]),CanvasRevisions=[first,second]},Resources=new(){Items=[new PlanningLight{Name="明确关联灯位",CanvasRevisionId=second.RevisionId}]}};
        var summary=PlanningReadableSummaryBuilder.Build(new(project,"明确位置",document,new(),[]));var path=Path.Combine(Root,"explicit-placements.docx");var result=PlanningSummaryDocument.Export(summary,document,path);Assert.IsEmpty(result.Warnings);
        using var package=WordprocessingDocument.Open(path,false);Assert.AreEqual(2,package.MainDocumentPart!.ImageParts.Count());var sections=new List<int>();var current=0;var expected=new[]{firstBytes,secondBytes};var drawingIndex=0;
        foreach(var block in package.MainDocumentPart.Document.Body!.ChildElements)
        {
            if(block is W.Paragraph paragraph&&paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value=="Heading1")current=int.Parse(paragraph.InnerText[..2]);
            foreach(var drawing in block.Descendants<W.Drawing>())
            {
                sections.Add(current);var xml=System.Xml.Linq.XElement.Parse(drawing.OuterXml);var relationship=xml.Descendants().Single(element=>element.Name.LocalName=="blip").Attributes().Single(attribute=>attribute.Name.LocalName=="embed").Value;using var stream=((ImagePart)package.MainDocumentPart.GetPartById(relationship)).GetStream();using var bytes=new MemoryStream();stream.CopyTo(bytes);CollectionAssert.AreEqual(Pixels(expected[drawingIndex++]),Pixels(bytes.ToArray()));
            }
        }
        CollectionAssert.AreEqual(new[]{2,6},sections);Assert.IsEmpty(new OpenXmlValidator().Validate(package));await Task.CompletedTask;
    });
    [TestMethod]
    public Task RenderExportedDocxPagesForVisualInspection()=>RunSta(async()=>
    {
        EnsureTestApplication();
        foreach(var name in new[]{"saved-two-revisions","snapshot-placeholders","narrow-portrait","explicit-placements","missing-revision-links","deferred-warning-summary"})
        {
        var path=Path.Combine(Root,name+".docx");if(!File.Exists(path))continue;var imported=PlanningDocxAdapter.Import(path);var document=imported.Document;
        using(var package=WordprocessingDocument.Open(path,false))
        {
            var section=package.MainDocumentPart!.Document.Body!.Elements<W.SectionProperties>().Last();var size=section.GetFirstChild<W.PageSize>()!;var margin=section.GetFirstChild<W.PageMargin>()!;
            document.PageWidth=size.Width!.Value/15d;document.PageHeight=size.Height!.Value/15d;document.PagePadding=new Thickness(margin.Left!.Value/15d,margin.Top!.Value/15d,margin.Right!.Value/15d,margin.Bottom!.Value/15d);
        }
        var paginator=((IDocumentPaginatorSource)document).DocumentPaginator;paginator.PageSize=new Size(document.PageWidth,document.PageHeight);paginator.ComputePageCount();
        var output=Path.Combine(Root,"product-readback-pages",name);Directory.CreateDirectory(output);
        for(var index=0;index<paginator.PageCount;index++)
        {
            using var page=paginator.GetPage(index);var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,page.Size.Width,page.Size.Height));drawing.DrawRectangle(new VisualBrush(page.Visual),null,new Rect(0,0,page.Size.Width,page.Size.Height));}
            var bitmap=new RenderTargetBitmap((int)page.Size.Width,(int)page.Size.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,$"page-{index+1}.png"));encoder.Save(stream);
        }
        await File.WriteAllTextAsync(Path.Combine(output,"RENDER.json"),JsonSerializer.Serialize(new{pages=paginator.PageCount,scope="Actual exported DOCX imported by product adapter and WPF paginated; NOT independent Word/WPS/LibreOffice acceptance",losses=imported.Losses},new JsonSerializerOptions{WriteIndented=true}));
        }
    });
}
