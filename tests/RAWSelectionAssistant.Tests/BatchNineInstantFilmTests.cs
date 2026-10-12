using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchNineInstantFilmTests
{
    [TestMethod]
    public void V1RemainsReadableAndV2BypassIsExactWithoutImplicitMigration()
    {
        var original=new ColorVector3(.2,.4,.7);
        var v1=new InstantFilmDefinition(true,Amount:0);
        Assert.AreNotEqual(original,InstantFilmPipeline.Apply(original,.2,.6,v1));
        var reopened=JsonSerializer.Deserialize<InstantFilmDefinition>(JsonSerializer.Serialize(v1))!;
        Assert.AreEqual(1,reopened.AlgorithmVersion);
        Assert.AreEqual(InstantFilmPipeline.Apply(original,.2,.6,v1),InstantFilmPipeline.Apply(original,.2,.6,reopened));
        var v2=v1 with{AlgorithmVersion=2,ImageEffectsDisabled=true};
        foreach(var paper in new[]{false,true})Assert.AreEqual(original,InstantFilmPipeline.Apply(original,.2,.6,v2 with{PaperFrame=paper}));
        Assert.Throws<InvalidDataException>(()=>(v1 with{ImageEffectsDisabled=true}).Validate());
    }
    [TestMethod]
    public void CropPositionFitAndLegacyHaveOneIsotropicMapping()
    {
        var film=new InstantFilmDefinition(true,AlgorithmVersion:2){PaperGeometry=new()};
        var center=InstantPaperLayout.Create(600,400,film);
        Assert.AreEqual(400,center.WindowWidth);Assert.AreEqual(400,center.WindowHeight);
        Assert.AreEqual(460,center.Width);Assert.AreEqual(530,center.Height);
        Assert.IsTrue(center.TrySource(center.Left,center.Top,out var x,out var y));Assert.AreEqual(100,x);Assert.AreEqual(0,y);
        var right=InstantPaperLayout.Create(600,400,film with{PaperGeometry=new(CropX:1)});
        Assert.IsTrue(right.TrySource(right.Left,right.Top,out x,out y));Assert.AreEqual(200,x);
        var fit=InstantPaperLayout.Create(600,400,film with{PaperGeometry=new(Fit:"FitEntire")});
        Assert.AreEqual(600,fit.WindowWidth);Assert.IsFalse(fit.TrySource(fit.Left,fit.Top,out _,out _));
        Assert.IsTrue(fit.TrySource(fit.Left,fit.Top+100,out x,out y));Assert.AreEqual(0,x);Assert.AreEqual(0,y);
        var legacy=InstantPaperLayout.Create(600,400,new InstantFilmDefinition(true));Assert.AreEqual(632,legacy.Width);Assert.AreEqual(480,legacy.Height);
    }
    [TestMethod]
    public void AllExifOrientationsMatchFullRowsTilesAndSavedDice()
    {
        for(ushort orientation=1;orientation<=8;orientation++)foreach(var fit in new[]{"CropFill","FitEntire"})
        {
            var definition=new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");
            var film=new InstantFilmDefinition(true,AlgorithmVersion:2){PaperGeometry=new(Fit:fit,CropX:.8,CropY:.2)};
            film=film with{DiceEnabled=true,Snapshot=InstantFilmRandom.Roll(film,20261012)};
            definition=definition with{InstantFilm=film};
            var data=Enumerable.Range(0,12*8*3).Select(i=>(i%255)/255f).ToArray();
            var contract=new ProfessionalRawContract("test","test","test","hash",definition.DecodeKey,[],[],[],255,OutputSpace:"sRGB");
            var master=new ProfessionalRawMaster(new(12,8,data,"8","sRGB",orientation),contract);
            var full=ProfessionalRawPipeline.RenderOutput(master,definition);var saved=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(definition))!;
            CollectionAssert.AreEqual(full.Rgb32.ToArray(),ProfessionalRawPipeline.RenderOutput(master,saved).Rgb32.ToArray());
            var tile=ProfessionalRawPipeline.RenderRegion(master,definition,1,1,3,3).Rgb32.ToArray();
            for(var y=0;y<3;y++)for(var x=0;x<3;x++)for(var c=0;c<3;c++)Assert.AreEqual(full.Rgb32.Span[((y+1)*full.Width+x+1)*3+c],tile[(y*3+x)*3+c]);
            var row=new byte[full.Width*6];ProfessionalRawPipeline.FillOutputRgb16Row(master,definition,1,row);
            for(var x=0;x<full.Width*3;x++)Assert.AreEqual((ushort)Math.Round(full.Rgb32.Span[full.Width*3+x]*65535),System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(x*2)));
        }
    }
    [TestMethod]
    public void InputRecipeSeparatesCacheKeysAndKeepsLegacyFingerprint()
    {
        var original=new ProfessionalRawDefinition();Assert.IsFalse(JsonSerializer.Serialize(original).Contains("InstantInput"));
        var a=new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1"){InstantInput=new(1,"Original",Guid.NewGuid())};var b=a with{InstantInput=a.InstantInput! with{Mode="CurrentEdits"}};
        Assert.AreNotEqual(a.DecodeKey,b.DecodeKey);Assert.AreEqual(a.DecodeKey,JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(a))!.DecodeKey);
    }
}

