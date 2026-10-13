using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.ColorValidation;
using RAWSelectionAssistant.Core.ValidationTools;
namespace RAWSelectionAssistant.Tests;
[TestClass]
[DoNotParallelize]
public sealed class PublicCubeAndLegacyTests
{
    private static string Data(string file)=>Path.Combine(AppContext.BaseDirectory,"TestData","Batch12",file);
    [TestMethod] public void CubeSampleGridMatchesPinnedProvenanceAndIndependentPythonMetrics()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Data("00_0010-metrics.json")));var m=doc.RootElement;
        Assert.AreEqual(m.GetProperty("sourcePngSha256").GetString()!.ToUpperInvariant(),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Data("00_0010-simple.png")))));
        var bytes=File.ReadAllBytes(Data("00_0010-sensor-grid.csv"));Assert.AreEqual(m.GetProperty("gridSha256").GetString()!.ToUpperInvariant(),Convert.ToHexString(SHA256.HashData(bytes)));
        var p=new DataProvenance("Cube++","Zenodo4153431:00_0010","https://zenodo.org/records/4153431","CC-BY-4.0",RedistributionPermission.Permitted,Convert.ToHexString(SHA256.HashData(bytes)));
        var c=new PublicRawSampleContract("00_0010",p,RawValidationDomain.SensorCodeValues,new(2041,2050,2059),14582,false,1,PublicRawValidation.Version);
        var sum=new ColorVector3();int count=0;
        foreach(var line in File.ReadLines(Data("00_0010-sensor-grid.csv")).Skip(1))
        {var v=line.Split(',').Select(x=>double.Parse(x,CultureInfo.InvariantCulture)).ToArray();var a=PublicRawValidation.SubtractBlack(new(v[2],v[3],v[4]),c);sum=new(sum.X+a.X,sum.Y+a.Y,sum.Z+a.Z);count++;}
        Assert.AreEqual(m.GetProperty("gridCount").GetInt32(),count);
        var gt=m.GetProperty("groundTruth").EnumerateArray().Select(x=>x.GetDouble()).ToArray();var truth=new ColorVector3(gt[0],gt[1],gt[2]);
        Assert.AreEqual(m.GetProperty("angularErrorDegrees").GetDouble(),ReferenceColorValidation.AngularErrorDegrees(sum,truth),1e-9);
        var balanced=PublicRawValidation.Balance(sum,truth,c with{Domain=RawValidationDomain.CameraLinear});
        Assert.AreEqual(m.GetProperty("afterGroundTruthWbNeutralErrorDegrees").GetDouble(),ReferenceColorValidation.AngularErrorDegrees(balanced,new(1,1,1)),1e-9);
        // Algebraic neutralization is not independent scene accuracy, so it is asserted separately.
        var ideal=PublicRawValidation.Balance(truth,truth,c with{Domain=RawValidationDomain.CameraLinear});Assert.AreEqual(ideal.X,ideal.Y,1e-12);Assert.AreEqual(ideal.Y,ideal.Z,1e-12);
    }
    [TestMethod] public void Pv4Pv5Pv6ReopenedLegacyPixelsMatchIndependentBaseShaGoldenForAllOrientations()
    {
        var expected=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Data("pv456-base-golden.json")))!;
        var actual=LegacyPixelCorpus.Capture();Assert.HasCount(72,actual);Assert.HasCount(72,expected);
        foreach(var item in expected)Assert.AreEqual(item.Value,actual[item.Key],item.Key);
    }
}
