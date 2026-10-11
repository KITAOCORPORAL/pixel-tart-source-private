using System.Windows;
using System.Windows.Documents;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchThreeDocumentTests
{
    [TestMethod]
    public void RichPackageAndSupportedDocxRoundTrip()
    {
        Exception? error=null;var thread=new Thread(()=>
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid()+".docx");
            try
            {
                var document=new FlowDocument(new Paragraph(new Run("中文标题"){FontWeight=FontWeights.Bold,FontSize=24}));
                var table=new Table();var rows=new TableRowGroup();var row=new TableRow();row.Cells.Add(new TableCell(new Paragraph(new Run("内容"))));rows.Rows.Add(row);table.RowGroups.Add(rows);document.Blocks.Add(table);
                var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(2,2,96,96,System.Windows.Media.PixelFormats.Rgb24,null,new byte[]{255,0,0,0,255,0,0,0,255,255,255,255},6);
                document.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Image{Source=bitmap,Width=240}));
                document.Blocks.Add(new List(new ListItem(new Paragraph(new Run("列表项")))));
                PlanningDocxAdapter.Export(document,path);
                using(var exported=DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(path,false)){var errors=new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(exported).ToArray();Assert.IsEmpty(errors,string.Join("; ",errors.Select(x=>x.Description)));}var imported=PlanningDocxAdapter.Import(path);
                Assert.IsEmpty(imported.Losses);Assert.AreEqual(4,imported.Document.Blocks.Count);
                Assert.AreEqual(FontWeights.Bold,((Run)((Paragraph)imported.Document.Blocks.FirstBlock!).Inlines.FirstInline!).FontWeight);
                using var buffer=new System.IO.MemoryStream();new TextRange(document.ContentStart,document.ContentEnd).Save(buffer,DataFormats.Rtf);buffer.Position=0;
                var recovered=new FlowDocument();new TextRange(recovered.ContentStart,recovered.ContentEnd).Load(buffer,DataFormats.Rtf);Assert.AreEqual(4,recovered.Blocks.Count);
            }
            catch(Exception e){error=e;}finally{if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error is not null)throw error;
    }
}
