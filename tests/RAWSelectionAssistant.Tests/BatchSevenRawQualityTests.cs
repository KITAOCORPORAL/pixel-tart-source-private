using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;

namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchSevenRawQualityTests
{
    private static ProfessionalRawMaster Master(int w,int h,float[] rgb,ushort orientation=1)
    {var d=new ProfessionalRawDefinition();return new(new(w,h,rgb,"16",ProfessionalRawPipeline.WorkingSpace,orientation),new("synthetic","Test","Synthetic",new string('a',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[],65535));}
    [TestMethod]
    public void AreaPreviewAntialiasesCheckerboardAndThinLinesAfterFullDisplayInLinearLight()
    {
        var pixels=new float[16*16*3];for(var y=0;y<16;y++)for(var x=0;x<16;x++)for(var c=0;c<3;c++)pixels[(y*16+x)*3+c]=(x+y)%2==0?.01f:.8f;
        var master=Master(16,16,pixels);var d=new ProfessionalRawDefinition();var full=ProfessionalRawPipeline.Render(master,d);var preview=ProfessionalRawPipeline.RenderPreview(master,d,4);
        double Decode(double v)=>v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);double Encode(double v)=>v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055;
        for(var y=0;y<4;y++)for(var x=0;x<4;x++)for(var c=0;c<3;c++){double sum=0;for(var sy=y*4;sy<(y+1)*4;sy++)for(var sx=x*4;sx<(x+1)*4;sx++)sum+=Decode(full.Rgb32.Span[(sy*16+sx)*3+c]);Assert.AreEqual(Encode(sum/16),preview.Rgb32.Span[(y*4+x)*3+c],1e-6);}
        pixels=new float[16*16*3];for(var y=0;y<16;y++)for(var c=0;c<3;c++)pixels[(y*16+1)*3+c]=.8f;
        var line=ProfessionalRawPipeline.RenderPreview(Master(16,16,pixels),d,4);Assert.IsTrue(line.Rgb32.Span[0]>0,"A one-pixel line must not vanish under nearest sampling.");
    }
    [TestMethod]
    public void RegionUsesExactOriginalPixelsForAllExifOrientations()
    {
        var master=Master(3,2,Enumerable.Range(0,18).Select(i=>i/30f).ToArray());var d=new ProfessionalRawDefinition();
        foreach(ushort orientation in Enumerable.Range(1,8).Select(i=>(ushort)i)){var current=master with{Image=new(3,2,master.Image.Rgb32,"16",ProfessionalRawPipeline.WorkingSpace,orientation)};var size=ProfessionalRawPipeline.OrientedSize(current);var region=ProfessionalRawPipeline.RenderRegion(current,d,0,0,size.Width,size.Height);for(var y=0;y<size.Height;y++)for(var x=0;x<size.Width;x++){var(sx,sy)=orientation switch{2=>(2-x,y),3=>(2-x,1-y),4=>(x,1-y),5=>(y,x),6=>(y,1-x),7=>(2-y,1-x),8=>(2-y,x),_=>(x,y)};var v=ProfessionalRawPipeline.RenderPixel(current,d,sx,sy);Assert.AreEqual((float)v.X,region.Rgb32.Span[(y*size.Width+x)*3]);}}
    }
    [TestMethod]
    public async Task StreamingRowsMatchHistoricalFullTiffBytesAndCancelAtomically()
    {
        using var temp=new TempDirectory();var master=Master(11,7,Enumerable.Range(0,11*7*3).Select(i=>i/190f).ToArray());var d=new ProfessionalRawDefinition(ExposureEv:.5);var full=ProfessionalRawPipeline.Render(master,d);
        using var old=new MemoryStream();TiffExport.WriteRgb48(old,full);using var rows=new MemoryStream();TiffExport.WriteRgb48Rows(rows,11,7,(y,row)=>ProfessionalRawPipeline.FillRgb16Row(master,d,y,row));CollectionAssert.AreEqual(old.ToArray(),rows.ToArray());rows.Position=0;var checkedFile=TiffStreamValidator.Validate(rows);Assert.AreEqual(16,checkedFile.BitsPerSample);
        var path=temp.Combine("cancel.tif");using var cancel=new CancellationTokenSource();await Assert.ThrowsAsync<OperationCanceledException>(()=>AtomicTiffWriter.WriteRowsAsync(path,11,7,(y,row)=>{ProfessionalRawPipeline.FillRgb16Row(master,d,y,row);if(y==2)cancel.Cancel();},token:cancel.Token));Assert.IsFalse(File.Exists(path));Assert.AreEqual(0,Directory.GetFiles(temp.Path,"*.tmp").Length);
        rows.SetLength(rows.Length-1);rows.Position=0;Assert.Throws<InvalidDataException>(()=>TiffStreamValidator.Validate(rows));
    }
}
