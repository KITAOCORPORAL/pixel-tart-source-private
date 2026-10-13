using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.ColorValidation;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class PairedCaptureMetadataTests
{
    // Deliberately artificial unit-test metadata and byte blobs: never delivered as a physical sample or calibrated data.
    private static PairedCaptureSample Mock(TempDirectory t)
    {
        string Save(string name){File.WriteAllText(t.Combine(name),"UNIT TEST ONLY - NOT RAW/SCAN/ICC");return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(t.Combine(name))));}
        var raw=Save("sample.raw");var scan=Save("sample.tif");var icc=Save("scanner.icc");var at=new DateTimeOffset(2026,1,1,10,0,0,TimeSpan.Zero);
        var e=new ExposureMetadata("Test digital",at,.01,8,100,0,"Manual","Measured test light","Unit test only");
        return new(1,"UNIT-TEST-NOT-CAPTURE","UNIT-TEST","sample.raw",raw,"sample.tif",scan,
            new("i-Type Color","TEST-BATCH",new DateOnly(2025,12,1),"unit-test evidence","unit-test history",5,8),e,e with{Camera="Polaroid I-2"},
            new(21,900,"shielded",at.AddSeconds(1),"unit-test handling"),new("Test scanner","TEST","no auto; RGB16",1200,16,"scanner.icc",icc,true,"ScannerTestV1",at.AddMinutes(20)),
            at.AddMinutes(30),"unit-test method","CameraInputTestV1","CreativePresetPendingMeasurement","test registration","test QC",true,false);
    }
    [TestMethod] public void MetadataRoundTripsWithSeparateStageIdentitiesButDoesNotClaimCalibration()
    {using var t=new TempDirectory();var s=Mock(t);var i=new PairedCaptureImporter();using var json=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(s));var r=i.Read(json);Assert.AreEqual(s,r);var q=i.Check(r,t.Path);Assert.IsTrue(q.Accepted);Assert.IsFalse(q.CalibrationEligible);Assert.IsTrue(q.Warnings.Any(x=>x.Contains("pending physical")));}
    [TestMethod] public void FabricatedClaimUnknownSchemaMissingStageAndWrongCameraReject()
    {using var t=new TempDirectory();var s=Mock(t);var i=new PairedCaptureImporter();foreach(var bad in new[]{s with{IsPhysicalCapture=false},s with{IsCalibrationClaim=true},s with{SchemaVersion=2},s with{Scanner=null!},s with{InstantExposure=s.InstantExposure with{Camera="A7M4"}}})Assert.IsFalse(i.Check(bad,t.Path).Accepted);}
    [TestMethod] public void InvalidChronologyExposureStorageAndDevelopmentReject()
    {using var t=new TempDirectory();var s=Mock(t);var i=new PairedCaptureImporter();foreach(var bad in new[]{s with{MeasuredAt=s.InstantExposure.CapturedAt},s with{DigitalExposure=s.DigitalExposure with{ShutterSeconds=0}},s with{Film=s.Film with{StorageMinCelsius=9,StorageMaxCelsius=2}},s with{Development=s.Development with{DurationSeconds=double.NaN}}})Assert.IsFalse(i.Check(bad,t.Path).Accepted);}
    [TestMethod] public void ArtifactHashTraversalMissingIccAndScannerAutomationAreNotIgnored()
    {using var t=new TempDirectory();var s=Mock(t);var i=new PairedCaptureImporter();foreach(var bad in new[]{s with{RawSha256=new string('0',64)},s with{RawPath="../escape.raw"},s with{Scanner=s.Scanner with{IccPath="missing.icc"}}})Assert.IsFalse(i.Check(bad,t.Path).Accepted);var q=i.Check(s with{Scanner=s.Scanner with{AutomaticCorrectionsDisabled=false,ScannerCharacterizationVersion=""}},t.Path);Assert.IsTrue(q.Accepted);Assert.IsFalse(q.CalibrationEligible);Assert.IsTrue(q.Warnings.Any(x=>x.Contains("film-only")));}
    [TestMethod] public void JsonDuplicateUnknownMissingAndMalformedFieldsReject()
    {var i=new PairedCaptureImporter();foreach(var text in new[]{"{\"SchemaVersion\":1,\"SchemaVersion\":2}","{\"Unexpected\":true}","{}","broken"}){using var json=new MemoryStream(Encoding.UTF8.GetBytes(text));Assert.Throws<InvalidDataException>(()=>i.Read(json));}}
}
