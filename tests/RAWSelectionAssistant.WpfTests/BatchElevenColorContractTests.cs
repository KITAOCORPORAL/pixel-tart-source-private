using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Publishing;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Services.Publishing;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchElevenColorContractTests
{
    [TestMethod]
    public async Task ControlledGreySaturationExifAndSixteenBitCodecsKeepSameFrozenPixels()
    {
        var root=Path.Combine(Path.GetTempPath(),"Kitao-color11-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            for(ushort orientation=1;orientation<=8;orientation++)
            {
                var d=InstantFilmJpegInput.Definition;var data=new float[24*16*3];
                for(var y=0;y<16;y++)for(var x=0;x<24;x++){var i=(y*24+x)*3;data[i]=x/23f;data[i+1]=y/15f;data[i+2]=(x+y)%2==0?.04045f:.18f;}
                var master=new ProfessionalRawMaster(new(24,16,data,"16","sRGB",orientation),new("test","test","test",new string('f',64),d.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",OutputBits:16,WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB"));
                var full=ProfessionalRawPipeline.RenderOutput(master,d);var tile=ProfessionalRawPipeline.RenderRegion(master,d,0,0,full.Width,full.Height);CollectionAssert.AreEqual(full.Rgb32.ToArray(),tile.Rgb32.ToArray());
                var hq=ProfessionalRawPipeline.RenderPreview(master,d,100);CollectionAssert.AreEqual(full.Rgb32.ToArray(),hq.Rgb32.ToArray());
                foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
                {
                    var path=Path.Combine(root,$"{orientation}-{format}.out");await ProfessionalRawExport.WriteAsync(master,d,path,format);var actual=ProfessionalRawExport.Inspect(path,master,d,format);
                    Assert.AreEqual(1,actual.Orientation);Assert.IsTrue(actual.Srgb);if(format!=StudioExportFormat.Jpeg){Assert.AreEqual(16,actual.BitDepth);Assert.AreEqual(0,actual.MaximumError);}
                }
            }
        }
        finally{Directory.Delete(root,true);}
    }
    [TestMethod]
    public void SeparatePureTiffEncodingValidationHashAndNodeCosts()
    {
        var evidence=Environment.GetEnvironmentVariable("KITAO_BATCH11_EVIDENCE");if(string.IsNullOrEmpty(evidence)){Assert.Inconclusive("Performance output opt-in required.");return;}
        var path=Path.Combine(Path.GetTempPath(),"Kitao-encode11-"+Guid.NewGuid()+".tif");
        try
        {
            var source=new HighBitDepthImageBuffer(1800,1200,Enumerable.Repeat(.18f,1800*1200*3).ToArray(),"16","sRGB");var timings=new List<object>();var watch=System.Diagnostics.Stopwatch.StartNew();
            foreach(var type in new[]{ColorStudioNodeType.BasicTone,ColorStudioNodeType.WhiteBalance,ColorStudioNodeType.Levels,ColorStudioNodeType.Curve})
            {
                watch.Restart();var node=new ColorAdjustmentStackNode(Guid.NewGuid(),type,type.ToString());var rendered=ColorStudioToolProcessor.Apply(source,node);timings.Add(new{stage="node:"+type,milliseconds=watch.Elapsed.TotalMilliseconds,identity=ColorStudioToolProcessor.IsIdentity(node)});GC.KeepAlive(rendered);
            }
            watch.Restart();using(var file=File.Create(path))TiffExport.WriteRgb48(file,source,new(IccProfile:StudioQuickExport.SrgbProfileBytes()));timings.Add(new{stage="pure-TIFF16-encoding-already-rendered-source",milliseconds=watch.Elapsed.TotalMilliseconds});
            watch.Restart();using(var file=File.OpenRead(path))TiffStreamValidator.Validate(file);timings.Add(new{stage="stream-readback-validation",milliseconds=watch.Elapsed.TotalMilliseconds});
            watch.Restart();using(var file=File.OpenRead(path))SHA256.HashData(file);timings.Add(new{stage="encoded-file-hash",milliseconds=watch.Elapsed.TotalMilliseconds});
            Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"pure-encoding-stages.json"),JsonSerializer.Serialize(new{scope="Synthetic1800x1200, no cache; identity tool baselines only, not real node-speed claims",timings},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }
    [TestMethod]
    public void ReducedHqUsesExpectedLinearLightBoxInsteadOfDoubleGamma()
    {
        var d=InstantFilmJpegInput.Definition;var values=new float[]{0,0,0,1,1,1,0,0,0,1,1,1};
        var master=new ProfessionalRawMaster(new(2,2,values,"16","sRGB"),new("test","test","test","hash",d.DecodeKey,[],[],[],65535,OutputSpace:"sRGB"));
        var hq=ProfessionalRawPipeline.RenderPreview(master,d,1);var encoded=1.055*Math.Pow(.5,1/2.4)-.055;
        foreach(var value in hq.Rgb32.ToArray())Assert.AreEqual(encoded,value,1e-6);
        Assert.AreNotEqual(.5f,hq.Rgb32.Span[0]);
    }
}
