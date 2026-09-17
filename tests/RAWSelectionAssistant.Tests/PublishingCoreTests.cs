using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Publishing;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PublishingNamingTests
{
    [TestMethod]
    public void NeverOverwritesAndSameFolderRequiresSafeSuffix()
    {
        using var temp = new TempDirectory(); var source=temp.CreateFile("photo.jpg",[1]);
        var sameFolder=PublishingExportService.ResolveDestination(source,temp.Path,new(Dimensions:new(false),WatermarksEnabled:false,Suffix:""));
        StringAssert.EndsWith(sameFolder,"photo_social.jpg"); File.WriteAllBytes(sameFolder,[2]);
        var numbered=PublishingExportService.ResolveDestination(source,temp.Path,new(Dimensions:new(false),WatermarksEnabled:false,Suffix:""));
        StringAssert.EndsWith(numbered,"photo_social_2.jpg");
    }
}

[TestClass]
public sealed class PublishingPresetTests
{
    [TestMethod]
    public async Task PersistsEveryExportAndWatermarkSetting()
    {
        using var temp=new TempDirectory();var path=temp.Combine("presets.json");var store=new PublishingPresetStore(path);
        var layer=new WatermarkLayer(Guid.NewGuid(),WatermarkLayerType.Text,Text:"Kitao",Opacity:.4,Position:WatermarkPosition.TopLeft,ColorAdjustments:new(45,.2,-.1,true));
        var preset=new PublishingPreset(Guid.NewGuid(),"Kitao Logo",new(new(false,PublishingSizeMode.Original,JpegQuality:93),true,[layer],PublishingOutputFormat.Png,"_web"),DateTimeOffset.UtcNow);
        await store.SaveAsync(preset);var loaded=(await new PublishingPresetStore(path).LoadAsync()).Single(item=>item.Id==preset.Id);
        Assert.AreEqual("_web",loaded.Options.Suffix);Assert.IsFalse(loaded.Options.EffectiveDimensions.Enabled);Assert.AreEqual(93,loaded.Options.EffectiveDimensions.JpegQuality);Assert.AreEqual(WatermarkPosition.TopLeft,loaded.Options.EffectiveWatermarkLayers[0].Position);Assert.IsTrue(loaded.Options.EffectiveWatermarkLayers[0].EffectiveColorAdjustments.Invert);
    }

    [TestMethod]
    public async Task ProjectDefaultPresetRoundTrips()
    {
        using var temp = new TempDirectory(); var store = new ProjectPublishingDefaultStore(temp.Combine("project-defaults.json"));
        var projectId = Guid.NewGuid(); var presetId = Guid.NewGuid();
        await store.SetAsync(new(projectId, presetId));
        Assert.AreEqual(presetId, await new ProjectPublishingDefaultStore(temp.Combine("project-defaults.json")).GetAsync(projectId));
    }
}

[TestClass]
public sealed class PublishingFolderInputTests
{
    [TestMethod]
    public void ScansJpegAndPngAndExplicitlyExcludesTiff()
    {
        using var temp=new TempDirectory();temp.CreateFile("a.jpg");temp.CreateFile("b.JPEG");temp.CreateFile("c.png");temp.CreateFile("not-supported.tiff");temp.CreateFile("notes.txt");
        var result=PublishingFolderInput.Scan(temp.Path);Assert.HasCount(3,result);Assert.IsFalse(result.Any(path=>Path.GetExtension(path).Equals(".tiff",StringComparison.OrdinalIgnoreCase)));
    }
}

[TestClass]
public sealed class PublishingSourceSafetyTests
{
    [TestMethod]
    public async Task CompressionWatermarkAndCombinedFlowsNeverMutateSource()
    {
        using var temp=new TempDirectory();var source=temp.CreateFile("source.png",[1,2,3,4,5]);var before=SHA256.HashData(await File.ReadAllBytesAsync(source));
        var variants=new[]
        {
            new PublishingOptions(new(true),false,[]),
            new PublishingOptions(new(false),true,[new(Guid.NewGuid(),WatermarkLayerType.Text,Text:"Copyright")]),
            new PublishingOptions(new(true),true,[new(Guid.NewGuid(),WatermarkLayerType.Text,Text:"Copyright",ColorAdjustments:new(20,.1,.1,true))])
        };
        foreach(var (options,index) in variants.Select((value,index)=>(value,index)))
        {
            var output=temp.Combine("out"+index);Directory.CreateDirectory(output);var result=await new PublishingExportService(new CopyRenderer()).ExportAsync(Guid.NewGuid(),new([source],output,options));Assert.AreEqual(TaskLifecycleState.Completed,result.State);CollectionAssert.AreEqual(before,SHA256.HashData(await File.ReadAllBytesAsync(source)));
        }
    }

    private sealed class CopyRenderer:IPublishingRenderer
    {
        public Task RenderAsync(string sourcePath,string destinationPath,PublishingOptions options,CancellationToken cancellationToken=default)=>File.WriteAllBytesAsync(destinationPath,File.ReadAllBytes(sourcePath),cancellationToken);
        public Task VerifyAsync(string imagePath,CancellationToken cancellationToken=default){Assert.IsGreaterThan(0,new FileInfo(imagePath).Length);return Task.CompletedTask;}
    }
}

[TestClass]
public sealed class PublishingCompressionTests
{
    [TestMethod] public void CompressionSettingsCanBeDisabledWithoutDisablingWatermarks(){var options=new PublishingOptions(new(false,PublishingSizeMode.Original),true,[new(Guid.NewGuid(),WatermarkLayerType.Text,Text:"Mark")]);options.Validate();Assert.IsFalse(options.EffectiveDimensions.Enabled);Assert.IsTrue(options.WatermarksEnabled);}
}
[TestClass] public sealed class PublishingImageWatermarkTests{[TestMethod]public void TransparentLogoUsesRelativeWidthAndNineGridPosition(){var layer=new WatermarkLayer(Guid.NewGuid(),WatermarkLayerType.Image,ImagePath:Path.GetFullPath("logo.png"),WidthPercent:12,Position:WatermarkPosition.BottomRight);layer.Validate();Assert.AreEqual(12,layer.WidthPercent);Assert.AreEqual(WatermarkPosition.BottomRight,layer.Position);}}
[TestClass] public sealed class PublishingTextWatermarkTests{[TestMethod]public void TextLayerCarriesFontWeightColorOpacityAndSpacing(){var layer=new WatermarkLayer(Guid.NewGuid(),WatermarkLayerType.Text,Text:"Kitao Soma",FontFamily:"Arial",FontWeight:"Bold",FontSize:40,Color:"#FFFFFFFF",LetterSpacing:2,Opacity:.6);layer.Validate();Assert.AreEqual("Bold",layer.FontWeight);Assert.AreEqual(2,layer.LetterSpacing);}}
[TestClass] public sealed class PublishingHslTests{[TestMethod]public void HslAndInvertAreOutputOnlySettings(){var values=new WatermarkColorAdjustments(120,.3,-.2,true).Validate();Assert.AreEqual(120,values.Hue);Assert.IsTrue(values.Invert);}}
[TestClass] public sealed class PublishingNoCompressionWatermarkTests{[TestMethod]public void OriginalSizeWatermarkPathIsValid(){new PublishingOptions(new(false,PublishingSizeMode.Original),true,[new(Guid.NewGuid(),WatermarkLayerType.Text,Text:"Mark")]).Validate();}}
