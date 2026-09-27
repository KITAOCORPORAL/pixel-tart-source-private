using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ProfessionalPipelineNoOpPrecisionTests
{
    [TestMethod]
    public void Rgb48NoOpColorStudioToTiff16PreservesPrecision()
    {
        var samples = new ushort[300 * 3]; for (var i = 0; i < 300; i++) samples[i * 3] = (ushort)(i * 200);
        var input = new HighBitDepthImageBuffer(300, 1, samples, "16", "sRGB");
        var node = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.Preset, "Disabled", false);
        var result = new ColorStudioRenderPipeline().Render(input, null!, null, new ColorAdjustmentStack([node]));
        Assert.IsNotNull(result.ProcessingPixels);
        var working = PrecisionAnalysisService.Analyze(result.ProcessingPixels!.ToRgb48());
        Assert.IsGreaterThan(256, working.UniqueValues); Assert.IsFalse(working.LooksLike8BitPromotion);
        using var stream = new MemoryStream(); TiffExport.WriteRgb48(stream, result.ProcessingPixels); stream.Position = 0;
        var readBack = TiffReadBack.Read(stream); var exported = PrecisionAnalysisService.Analyze(readBack.Rgb48Samples.Span);
        Assert.IsGreaterThan(256, exported.UniqueValues); Assert.IsFalse(exported.LooksLike8BitPromotion);
    }

    [TestMethod]
    public void QuantizationDetectorRejectsRgb8PromotedToRgb48()
    {
        var promoted = Enumerable.Range(0, 256).SelectMany(value => new ushort[] { (ushort)(value * 257), (ushort)(value * 257), (ushort)(value * 257) }).ToArray();
        var analysis = PrecisionAnalysisService.Analyze(promoted);
        Assert.IsTrue(analysis.LooksLike8BitPromotion); Assert.AreEqual(1d, analysis.Promoted8BitRatio);
    }

    [TestMethod]
    public void ProfessionalReferenceMatchUsesFloatBufferWithoutPrecisionBreak()
    {
        var samples = new ushort[300 * 3];
        for (var i = 0; i < 300; i++)
        {
            samples[i * 3] = (ushort)(i * 200);
            samples[i * 3 + 1] = (ushort)(i * 173);
            samples[i * 3 + 2] = (ushort)(i * 149);
        }
        var input = new HighBitDepthImageBuffer(300, 1, samples, "16", "sRGB");
        var display = input.ToVisualRgb24();
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "reference-float", display));
        var look = new ReferenceLook(Guid.NewGuid(), "identity", null,
            [new(Guid.NewGuid(), null, "reference", "synthetic", "synthetic", 1, analysis)],
            new ReferenceLookParameters(MatchStrength: 0), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var node = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "参考仿色", true,
            new Dictionary<string, double> { ["match_strength"] = 0 });

        var result = new ColorStudioRenderPipeline().Render(input, analysis, look, new ColorAdjustmentStack([node]));

        Assert.IsNotNull(result.ProcessingPixels);
        Assert.IsNotNull(result.Precision);
        Assert.IsFalse(result.Precision!.HasPrecisionBreak);
        var uniqueFloatLevels = result.ProcessingPixels!.Rgb32.Span.ToArray().Distinct().Count();
        Assert.IsGreaterThan(256, uniqueFloatLevels);
        Assert.IsFalse(PrecisionAnalysisService.Analyze(result.ProcessingPixels.ToRgb48()).LooksLike8BitPromotion);
    }

    [TestMethod]
    public void ProfessionalV4UsesFloatOutputAndPreservesFineTonalSteps()
    {
        var sourceValues = new ushort[320 * 3]; var referenceValues = new ushort[320 * 3];
        for (var i = 0; i < 320; i++)
        {
            sourceValues[i * 3] = (ushort)(i * 191); sourceValues[i * 3 + 1] = (ushort)(i * 157); sourceValues[i * 3 + 2] = (ushort)(i * 113);
            referenceValues[i * 3] = (ushort)Math.Min(65535, i * 191 + 900); referenceValues[i * 3 + 1] = (ushort)Math.Min(65535, i * 157 + 400); referenceValues[i * 3 + 2] = (ushort)Math.Min(65535, i * 113 + 200);
        }
        var source = new HighBitDepthImageBuffer(320, 1, sourceValues, "16", "sRGB");
        var reference = new HighBitDepthImageBuffer(320, 1, referenceValues, "16", "sRGB");
        var result = new ReferenceMatchV4Engine().Match(source, reference,
            settings: new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 64, SinkhornIterations: 4, ResidualIterations: 0),
            preferGpu: false);

        Assert.AreEqual(ReferenceMatchV4BackendKind.Cpu, result.Backend);
        Assert.AreEqual(source.Width, result.Pixels.Width);
        Assert.IsGreaterThan(256, result.Pixels.Rgb32.Span.ToArray().Distinct().Count());
        Assert.IsFalse(PrecisionAnalysisService.Analyze(result.Pixels.ToRgb48()).LooksLike8BitPromotion);
    }
}
