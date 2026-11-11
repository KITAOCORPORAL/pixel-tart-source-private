using System.Buffers.Binary;
using System.Text.Json;
using PortraitHighlightResearch;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PortraitHighlightResearchTests
{
    [TestMethod]
    public void RenderedObserverPinsHistoricalFormulaAndRecordsFirstLoss()
    {
        var random = new Random(77119);
        foreach (var ev in new[] { -5d, 0d, .7, 5d })
        {
            var recipe = new RenderedSrgbExposure(ExposureEv: ev);
            for (var i = 0; i < 2000; i++)
            {
                var input = new ColorVector3(random.NextDouble()*2-.2, random.NextDouble()*2-.2, random.NextDouble()*2-.2);
                double Old(double x) => Math.Clamp(PhotoColorMathV1.EncodeSrgb(PhotoColorMathV1.DecodeSrgb(x)*Math.Pow(2,ev)),0,1);
                var stages = new Dictionary<string,ColorVector3>();
                Assert.AreEqual(new ColorVector3(Old(input.X),Old(input.Y),Old(input.Z)),recipe.Apply(input));
                Assert.AreEqual(recipe.Apply(input),recipe.Apply(input,(stage,value)=>stages.Add(stage,value)));
                Assert.HasCount(4,stages);
            }
        }
        var observed = new Dictionary<string,ColorVector3>();
        new RenderedSrgbExposure(ExposureEv: 2).Apply(new(.8,.6,.4),(stage,value)=>observed.Add(stage,value));
        Assert.IsTrue(observed["exposed-linear"].X>1);
        Assert.IsTrue(observed["encoded-preclip"].X>1);
        Assert.AreEqual(1,observed["output-clipped"].X);
    }

    [TestMethod]
    public void RawAndBasicObserversDoNotChangeOutputAndKeepDomainSpecificStages()
    {
        foreach(var v in new[]{new ColorVector3(0,0,0),new(.18,.18,.18),new(2,1,.3),new(-.1,.4,1),new(20,20,20)})
        {
            var stages=new Dictionary<string,ColorVector3>();
            Assert.AreEqual(ProfessionalRawPipeline.Display(v),ProfessionalRawPipeline.Display(v,(s,x)=>stages.Add(s,x)));
            Assert.IsTrue(stages.ContainsKey("matrix-linear-srgb"));
            Assert.IsTrue(stages.ContainsKey("display-oetf")||stages.ContainsKey("nonpositive-luma-black"));
        }
        using var image=new HighBitDepthImageBuffer(3,1,new float[]{.2f,.1f,.05f,.8f,.6f,.4f,1,1,1});
        var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"diagnostic",NumericParameters:new Dictionary<string,double>{{"exposure",2}});
        using var original=ColorStudioToolProcessor.Apply(image,node);
        var seen=new List<(int,string)>();
        using var traced=ColorStudioToolProcessor.Apply(image,node,CancellationToken.None,(i,s,_)=>seen.Add((i,s)));
        CollectionAssert.AreEqual(original.Rgb32.ToArray(),traced.Rgb32.ToArray());
        Assert.HasCount(9,seen);Assert.IsTrue(seen.All(x=>x.Item1 is >=0 and <3));
    }

    [TestMethod]
    public void CandidatePreservesLinearRatiosAndHasStrictPositiveSlopeOverDeclaredLadder()
    {
        var recipe=new MaxChannelShoulderV1();
        foreach(var shape in new[]{new ColorVector3(1,1,1),new(.76,.42,.26),new(1,.3,.045)})
        {
            double previous=-1;
            for(var i=0;i<=1024;i++)
            {
                var gain=Math.Pow(2,-6+i*12d/1024);var input=new ColorVector3(shape.X*gain,shape.Y*gain,shape.Z*gain);
                var result=recipe.ApplyLinear(input);var maximum=Math.Max(result.X,Math.Max(result.Y,result.Z));
                Assert.IsTrue(maximum>previous&&maximum<1);previous=maximum;
                Assert.AreEqual(input.Y/input.X,result.Y/result.X,2e-15);
                Assert.AreEqual(input.Z/input.X,result.Z/result.X,2e-15);
            }
        }
    }

    [TestMethod]
    public void CandidateRejectsWrongDomainVersionAndInvalidValues()
    {
        Assert.Throws<InvalidDataException>(()=>new MaxChannelShoulderV1(Version:2).Validate());
        Assert.Throws<InvalidDataException>(()=>new MaxChannelShoulderV1(Input:"SensorRaw").Validate());
        Assert.Throws<InvalidDataException>(()=>new MaxChannelShoulderV1(Knee:1).Validate());
        Assert.Throws<InvalidDataException>(()=>new MaxChannelShoulderV1(ExposureEv:6).Validate());
        foreach(var v in new[]{new ColorVector3(-.1,.2,.3),new(double.NaN,.2,.3),new(double.PositiveInfinity,.2,.3)})
            Assert.Throws<InvalidDataException>(()=>new MaxChannelShoulderV1().ApplyLinear(v));
        Assert.Throws<InvalidDataException>(()=>new MaxChannelShoulderV1().ApplyRendered(new(1.1,.2,.3)));
        Assert.AreEqual(new ColorVector3(.1,.2,.3),new MaxChannelShoulderV1().ApplyLinear(new(.1,.2,.3)));
    }

    [TestMethod]
    public void CandidatePreviewSaveReopenAndRgb16RowsUseSameFrozenDefinition()
    {
        var recipe=new MaxChannelShoulderV1(ExposureEv: 5);
        var reopened=JsonSerializer.Deserialize<MaxChannelShoulderV1>(JsonSerializer.Serialize(recipe))!.Validate();
        Assert.AreEqual(recipe,reopened);
        var input=new[]{new ColorVector3(.01,.03,.05),new(.2,.15,.1),new(.5,.3,.2),new(1,.7,.1)};
        var full=input.Select(recipe.ApplyRendered).ToArray();
        var preview=input.Select(reopened.ApplyRendered).Select(v=>new ColorVector3((float)v.X,(float)v.Y,(float)v.Z)).ToArray();
        var row=new byte[input.Length*6];
        for(int i=0;i<input.Length;i++)
        {
            var output=reopened.ApplyRendered(input[i]);var values=new[]{output.X,output.Y,output.Z};
            for(int c=0;c<3;c++)BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(i*6+c*2),(ushort)Math.Clamp(Math.Round((float)values[c]*65535),0,65535));
            Assert.AreEqual(full[i],output);
            Assert.AreEqual((float)full[i].X,preview[i].X);
            Assert.AreEqual((ushort)Math.Round((float)full[i].X*65535),BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(i*6)));
        }
    }
    [TestMethod]
    public void HistoricalPv4Pv5Pv6PreviewReopenAndProductRgb16StayExact()
    {
        var definitions=new[]{
            new ProfessionalRawDefinition(ExposureEv: .7),
            new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1"){SrgbExposure=new(ExposureEv: .7)},
            LegacyRawInstantInput.Create(BatchTenLegacyRawTests.Recipe())};
        foreach(var recipe in definitions)
        {
            var values=Enumerable.Range(0,12*8*3).Select(i=>(i%251)/250f).ToArray();
            using var image=new HighBitDepthImageBuffer(12,8,values,"32f",recipe.IsRenderedInput?"sRGB":ProfessionalRawPipeline.WorkingSpace,1);
            var master=new ProfessionalRawMaster(image,new("frozen-test","test","test",new string('a',64),recipe.DecodeKey,[],[],[],65535));
            using var before=ProfessionalRawPipeline.RenderOutput(master,recipe);
            using var preview=ProfessionalRawPipeline.RenderPreview(master,recipe,12);
            CollectionAssert.AreEqual(before.Rgb32.ToArray(),preview.Rgb32.ToArray());
            var saved=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(recipe))!.Validate();
            Assert.AreEqual(recipe.DecodeKey,saved.DecodeKey);
            using var reopened=ProfessionalRawPipeline.RenderOutput(master,saved);
            CollectionAssert.AreEqual(before.Rgb32.ToArray(),reopened.Rgb32.ToArray());
            using var file=new MemoryStream();TiffExport.WriteRgb48Rows(file,12,8,(y,row)=>ProfessionalRawPipeline.FillOutputRgb16Row(master,saved,y,row));file.Position=0;
            var readback=TiffReadBack.Read(file);CollectionAssert.AreEqual(before.ToRgb48(),readback.Rgb48Samples.ToArray());
        }
    }

}
