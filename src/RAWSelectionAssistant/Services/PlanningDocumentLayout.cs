using System.IO;
using System.Windows;
using System.Windows.Documents;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
public static class PlanningDocumentLayout
{
    public static void Apply(FlowDocument document,PlanningRichLayout? layout)
    {
        layout?.Validate();
        document.Tag=layout?.HeaderFooter??new PlanningHeaderFooter();
        var page=(layout?.Page??new()).Validate();document.PageWidth=page.Width;document.PageHeight=page.Height;document.PagePadding=new Thickness(page.Left,page.Top,page.Right,page.Bottom);document.ColumnWidth=double.PositiveInfinity;
        PlanningDocumentImages.Restore(document,layout?.Images??[]);
        var blocks=document.Blocks.ToArray();foreach(var semantic in layout?.Blocks??[])
        {
            if(semantic.BlockIndex<0||semantic.BlockIndex>=blocks.Length)continue;var block=blocks[semantic.BlockIndex];block.Tag=semantic;block.BreakPageBefore=semantic.PageBreakBefore;
            if(block is Paragraph p && semantic.Style!="Body"){p.FontSize=semantic.Style switch{"Heading1"=>26,"Heading2"=>21,"Heading3"=>17,_=>14};p.FontWeight=semantic.Style=="Body"?FontWeights.Normal:FontWeights.Bold;}
        }
    }
    public static FlowDocument Load(string rich,PlanningRichLayout? layout)
    {var document=new FlowDocument();using var stream=new MemoryStream(Convert.FromBase64String(rich));new TextRange(document.ContentStart,document.ContentEnd).Load(stream,DataFormats.Rtf);Apply(document,layout);return document;}
    public static PlanningRichLayout Capture(FlowDocument document,PlanningPageLayout page,IReadOnlyList<string> losses)
        =>new(page,document.Blocks.Select((b,i)=>(b.Tag as PlanningBlockSemantic??new(i)) with{BlockIndex=i,PageBreakBefore=b.BreakPageBefore}).ToArray(),losses){Images=PlanningDocumentImages.Images(document).Where(i=>i.Tag is PlanningDocumentImage).Select((i,n)=>((PlanningDocumentImage)i.Tag) with{Width=i.Width,Height=i.Height,Order=n,Position=PlanningDocumentImages.Position(i)}).ToArray(),HeaderFooter=document.Tag as PlanningHeaderFooter??new()};
}
