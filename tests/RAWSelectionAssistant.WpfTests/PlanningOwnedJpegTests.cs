using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class PlanningOwnedJpegTests
{
    static string Fixture(string name)=>Path.Combine(TestResources.Root,"tests","fixtures","planning-owned-jpeg",name);
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static byte[] Rgb(BitmapSource image){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb24,null,0);var b=new byte[rgb.PixelWidth*rgb.PixelHeight*3];rgb.CopyPixels(b,rgb.PixelWidth*3,0);return b;}
    [TestMethod]public Task EightExifOrientationsMatchPinnedPillowAndImageMagickWithoutSecondRotation()=>RunSta(async()=>
    {
        EnsureTestApplication();var rows=new List<object>();for(int o=1;o<=8;o++)
        {
            var actual=PlanningOwnedReferenceImage.Decode(await File.ReadAllBytesAsync(Fixture($"orientation-{o}.jpg")),".jpg");Assert.AreEqual(o>=5?48:80,actual.PixelWidth);Assert.AreEqual(o>=5?80:48,actual.PixelHeight);
            foreach(var reference in new[]{"pillow","magick"})
            {
                using var stream=File.OpenRead(Fixture($"{reference}-{o}.png"));var expected=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];Assert.AreEqual(expected.PixelWidth,actual.PixelWidth);Assert.AreEqual(expected.PixelHeight,actual.PixelHeight);var a=Rgb(actual);var b=Rgb(expected);var max=a.Zip(b,(x,y)=>Math.Abs(x-y)).Max();Assert.IsLessThanOrEqualTo(3,max,$"{reference} orientation{o}");rows.Add(new{orientation=o,reference,maxAbsolute8bit=max,meanAbsolute8bit=a.Zip(b,(x,y)=>(double)Math.Abs(x-y)).Average()});
            }
        }
        var evidence=Environment.GetEnvironmentVariable("KITAO_OWNED_JPEG_EVIDENCE");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);await File.WriteAllTextAsync(Path.Combine(evidence,"ORIENTATION_COMPARISON.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));}
    });
    [TestMethod]public Task DocumentAndShotOwnedJpegPngPreviewsDocxAndForeignImportShareVerifiedSource()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);var shot=f.Vm.Shots.Single();var external=Path.Combine(f.Root,"old.jpg");var shotRef=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Pose,ExternalReference:external,Title:"镜头JPEG方向6");await f.Shots.SaveAsync(shot with{Name="JPEG镜头预览",References=[shotRef]});await f.Vm.LoadAsync(f.Id);var selected=Fixture("orientation-6.jpg");var selectedHash=Hash(selected);var inspected=await PlanningExternalFile.InspectAsync(shotRef.ReferenceId,"reference",external,selected);Assert.IsTrue(await f.Vm.ReconnectExternalAsync(inspected,true));
        var docRef=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:Fixture("orientation-8.jpg"),Title:"文档JPEG方向8");var pngRef=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:Fixture("plain.png"),Title:"原PNG路径");await f.Vm.AddDocumentReferencesAsync([docRef,pngRef],true);await f.Vm.AddAttachmentsAsync([Fixture("orientation-3.jpg")],true);Assert.IsTrue(await f.Vm.FlushAsync());
        var reopened=f.NewVm();await reopened.LoadAsync(f.Id);foreach(var item in reopened.AllProjectReferences){Assert.IsTrue(item.SourceAvailable);Assert.IsNotNull(item.PreviewPath,item.Availability);using var input=File.OpenRead(item.PreviewPath);var preview=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];if(item.Id!=pngRef.ReferenceId){Assert.AreEqual(48,preview.PixelWidth);Assert.AreEqual(80,preview.PixelHeight);}}
        var view=new PlanningCenterView{DataContext=reopened};await reopened.SelectContentSafelyAsync("镜头清单");view.Render();view.Measure(new Size(1280,900));view.Arrange(new Rect(0,0,1280,900));view.UpdateLayout();var table=((StackPanel)view.FindName("DocumentContent")).Children.OfType<DataGrid>().Single();var row=(PlanningShotEditRow)table.Items[0];Assert.IsNotNull(row.ReferencePreviewPath);view.DataContext=null;
        var output=Path.GetFullPath(Environment.GetEnvironmentVariable("KITAO_OWNED_JPEG_EVIDENCE")??Path.Combine(f.Root,"evidence"));Directory.CreateDirectory(output);var summary=Path.Combine(output,"jpeg-png-15-section-summary.docx");var result=await reopened.ExportPlanningSummaryDocxAsync(summary);Assert.IsNotNull(result);Assert.IsEmpty(result.Warnings);
        using(var doc=WordprocessingDocument.Open(summary,false)){Assert.IsFalse(new OpenXmlValidator().Validate(doc).Any());Assert.AreEqual(4,doc.MainDocumentPart!.ImageParts.Count(),"2 doc refs plus same shot JPEG in shot and reference sections; attachment JPEG must not embed");var headings=doc.MainDocumentPart.Document.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().Count(p=>p.ParagraphProperties?.ParagraphStyleId?.Val=="Heading1");Assert.AreEqual(15,headings);}
        var owned=Path.Combine(output,"jpeg-owned-v1.ptplan");await reopened.ExportProjectPackageAsync(owned);var imported=await PlanningProjectPackage.ImportAsync(owned,Path.Combine(output,"foreign-root"));var importedDoc=(await new PlanningProjectStore(Path.Combine(imported.DataRoot,"planning")).LoadAsync(f.Id)).Document!;var copy=importedDoc.ShotExternalLinks.Single().ProjectCopy!;Assert.IsNull(importedDoc.ShotExternalLinks.Single().ReconnectedFile);var bitmap=PlanningOwnedReferenceImage.Read(copy,f.Id,importedDoc.Package!.DocumentId,shotRef.ReferenceId);Assert.AreEqual(48,bitmap.PixelWidth);Assert.AreEqual(80,bitmap.PixelHeight);Assert.AreEqual(selectedHash,Hash(selected));
        var seed=Path.Combine(output,"jpeg-shot-missing-v1.ptplan");var current=await f.Planning.LoadAsync(f.Id);await PlanningProjectPackage.ExportAsync(seed,new(){Id=f.Id,Name="原生JPEG副本验收"},current with{Document=current.Document! with{References=[],Attachments=[],ShotExternalLinks=[]}},await f.Shots.LoadAsync(f.Id),f.Planning.StorageRoot);
        await File.WriteAllTextAsync(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(new{project=f.Id,shot=shot.ShotId,reference=shotRef.ReferenceId,selected,selectedHash,owned,summary,summaryImages=4,headings=15,seed,importedRoot=imported.DataRoot,scope="Real stores/VM recreation/WPF grid and actual DOCX, not native process restart"},new JsonSerializerOptions{WriteIndented=true}));
    });
    [TestMethod]public Task InvalidMissingReplacedAndUnsupportedOwnedImagesNeverFallbackOrPreventProjectOpen()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var broken=Path.Combine(f.Root,"broken.jpg");await File.WriteAllBytesAsync(broken,[255,216,255,224,0,50,1]);var unknown=Path.Combine(f.Root,"other.gif");await File.WriteAllBytesAsync(unknown,"GIF89a preserved non-JPEG"u8.ToArray());
        var refs=new[]{Fixture("orientation-6.jpg"),Fixture("plain.png"),broken,unknown}.Select(p=>new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:p,Title:Path.GetFileName(p))).ToArray();await f.Vm.AddDocumentReferencesAsync(refs,true);var reopened=f.NewVm();await reopened.LoadAsync(f.Id);foreach(var r in refs.Skip(2)){var item=reopened.AllProjectReferences.Single(i=>i.Id==r.ReferenceId);Assert.IsTrue(item.SourceAvailable);Assert.IsNull(item.PreviewPath);StringAssert.Contains(item.Availability,"已保存但无法预览");}
        var state=await f.Planning.LoadAsync(f.Id);var jpeg=state.Document!.References.Single(r=>r.Source.ReferenceId==refs[0].ReferenceId).ProjectCopy!;var png=state.Document.References.Single(r=>r.Source.ReferenceId==refs[1].ReferenceId).ProjectCopy!;await File.WriteAllBytesAsync(PlanningProjectCopyStorage.Resolve(jpeg),File.ReadAllBytes(Fixture("orientation-8.jpg")));File.Delete(PlanningProjectCopyStorage.Resolve(png));await reopened.RefreshDocumentReferencesAsync();foreach(var r in refs.Take(2)){var item=reopened.AllProjectReferences.Single(i=>i.Id==r.ReferenceId);Assert.IsFalse(item.SourceAvailable);Assert.IsNull(item.PreviewPath);Assert.IsNull(await reopened.ResolveOriginalAsync(item));Assert.IsTrue(File.Exists(r.ExternalReference));}
        var retry=f.NewVm();await retry.LoadAsync(f.Id);Assert.HasCount(4,retry.AllProjectReferences);var path=Path.Combine(f.Root,"failures.docx");var result=await retry.ExportPlanningSummaryDocxAsync(path);Assert.IsNotNull(result);Assert.IsTrue(result.Warnings.Count>=4);using var doc=WordprocessingDocument.Open(path,false);Assert.AreEqual(0,doc.MainDocumentPart!.ImageParts.Count());
    });
    [TestMethod]public Task HeaderBoundsMalformedExifAndAttachmentIdentityRejectBeforeUnsafeDecode()=>RunSta(async()=>
    {
        var data=await File.ReadAllBytesAsync(Fixture("orientation-6.jpg"));var invalid=(byte[])data.Clone();bool changed=false;for(int i=0;i<invalid.Length-12;i++){if(invalid[i]==0x12&&invalid[i+1]==1&&invalid[i+2]==3&&invalid[i+3]==0){invalid[i+8]=9;changed=true;break;}if(invalid[i]==1&&invalid[i+1]==0x12&&invalid[i+2]==0&&invalid[i+3]==3){invalid[i+8]=0;invalid[i+9]=9;changed=true;break;}}Assert.IsTrue(changed,"Synthetic EXIF mutation must find a tag before rejection assertion");Assert.Throws<InvalidDataException>(()=>PlanningOwnedReferenceImage.Decode(invalid,".jpg"));
        var huge=(byte[])data.Clone();for(int i=0;i<huge.Length-9;i++)if(huge[i]==255&&huge[i+1]==0xC0){huge[i+5]=0x7F;huge[i+6]=0xFF;break;}Assert.Throws<InvalidDataException>(()=>PlanningOwnedReferenceImage.Decode(huge,".jpg"));Assert.Throws<InvalidDataException>(()=>PlanningOwnedReferenceImage.Decode(data[..24],".jpg"));
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.AddAttachmentsAsync([Fixture("orientation-6.jpg")],true);var receipt=f.Vm.Attachments.Single().ProjectCopy!;Assert.Throws<InvalidDataException>(()=>PlanningOwnedReferenceImage.Read(receipt,f.Id,f.Vm.PlanningDocumentId!.Value,receipt.ReferenceId));
    });
}
