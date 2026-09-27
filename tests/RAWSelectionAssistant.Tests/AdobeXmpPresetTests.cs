using RAWSelectionAssistant.Core.Services.Presets;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AdobeXmpPresetTests
{
    [TestMethod]
    public void ParserMapsSupportedFieldsAndPreservesUnsupportedState()
    {
        var xml = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\" xmlns:crs=\"http://ns.adobe.com/camera-raw-settings/1.0/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description crs:PresetName=\"暖光\" crs:Exposure2012=\"1.25\" crs:Contrast2012=\"-8\" crs:ToneCurvePV2012=\"0,0;255,255\" /></rdf:RDF></x:xmpmeta>";
        var preset = AdobeXmpPresetParser.Parse("暖光.xmp", System.Text.Encoding.UTF8.GetBytes(xml));
        Assert.AreEqual("暖光", preset.Name); Assert.AreEqual(1.25, preset.NumericParameters["exposure"]);
        Assert.AreEqual(PresetFieldSupport.Exact, preset.Fields.Single(x => x.SourceName == "Exposure2012").Support);
        Assert.AreEqual(PresetFieldSupport.Unsupported, preset.Fields.Single(x => x.SourceName == "ToneCurvePV2012").Support);
        Assert.IsTrue(preset.IsCompatible); Assert.IsFalse(string.IsNullOrWhiteSpace(preset.SourceHash));
    }

    [TestMethod]
    public void StrengthInterpolationHasZeroAndFullIdentity()
    {
        var before = new Dictionary<string, double> { ["exposure"] = 0, ["saturation"] = 10 }; var preset = new Dictionary<string, double> { ["exposure"] = 2, ["saturation"] = 30 };
        var zero = PresetStrengthInterpolator.Interpolate(before, preset, 0); var half = PresetStrengthInterpolator.Interpolate(before, preset, .5); var full = PresetStrengthInterpolator.Interpolate(before, preset, 1);
        Assert.AreEqual(0, zero["exposure"]); Assert.AreEqual(1, half["exposure"]); Assert.AreEqual(2, full["exposure"]); Assert.AreEqual(20, half["saturation"]);
    }

    [TestMethod]
    public async Task PresetStoreRoundTripsSourceHashAndFields()
    {
        var root = Path.Combine(Path.GetTempPath(), "pixel-tart-xmp-" + Guid.NewGuid().ToString("N"));
        try { var preset = AdobeXmpPresetParser.Parse("x.xmp", System.Text.Encoding.UTF8.GetBytes("<root Exposure2012=\"1\" />")); var store = new AdobeXmpPresetStore(root); await store.SaveAsync(preset); var loaded = await store.LoadAsync(); Assert.HasCount(1, loaded); Assert.AreEqual(preset.SourceHash, loaded[0].SourceHash); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
