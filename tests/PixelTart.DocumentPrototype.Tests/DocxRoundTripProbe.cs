using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Validation;
[assembly: DoNotParallelize]
namespace PixelTart.DocumentPrototype.Tests;
[TestClass]
public sealed class DocxRoundTripProbe
{
    [TestMethod]
    public void ParagraphTableAndPageSettingsRoundTripWithSchemaValidation()
    {
        using var data = new MemoryStream();
        using (var doc=WordprocessingDocument.Create(data,WordprocessingDocumentType.Document,true))
        {
            var main=doc.AddMainDocumentPart();
            main.Document=new Document(new Body(
                new Paragraph(new Run(new RunProperties(new Bold()),new Text("摄影策划基础验证"))),
                new Table(new TableProperties(new TableWidth{Width="5000",Type=TableWidthUnitValues.Pct}),
                    new TableGrid(new GridColumn{Width="4000"}),new TableRow(new TableCell(new Paragraph(new Run(new Text("镜头 01")))))) ,
                new SectionProperties(new PageSize{Width=11906,Height=16838},new PageMargin{Top=1440,Bottom=1440,Left=1440,Right=1440,Header=720,Footer=720,Gutter=0})));
            main.Document.Save();
            Assert.IsFalse(new OpenXmlValidator().Validate(doc).Any());
        }
        data.Position=0;
        using(var reopened=WordprocessingDocument.Open(data,false))
        {
            var body=reopened.MainDocumentPart!.Document!.Body!;
            Assert.AreEqual("摄影策划基础验证",body.Elements<Paragraph>().First().InnerText);
            Assert.AreEqual("镜头 01",body.Elements<Table>().Single().InnerText);
            Assert.IsNotNull(body.Descendants<Bold>().Single());
            Assert.AreEqual(11906U,body.Descendants<PageSize>().Single().Width!.Value);
        }
    }
}
