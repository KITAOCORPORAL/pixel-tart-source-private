using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.ColorValidation;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class PublicColorReferenceTests
{
    private static readonly ObservationConditions Unknown = new(null,null,null,null,null,"Not declared by source");
    private static readonly ObservationConditions D50 = new("D50",.3457,.3585,2,"Synthetic","Explicit synthetic conditions");
    private static readonly ObservationConditions D65 = new("D65",.3127,.3290,2,"Synthetic","Explicit synthetic conditions");
    internal static string Synthetic(ClassicEdition edition=ClassicEdition.BeforeNovember2014, bool comma=false, bool xyz=false)
    {
        var origin=edition==ClassicEdition.BeforeNovember2014?"Before November2014 edition":"November2014 edition and newer";
        var b=new StringBuilder($"ORIGINATOR \"ColorChecker24 - {origin}\"\nNUMBER_OF_FIELDS 4\nBEGIN_DATA_FORMAT\nSAMPLE_NAME {(xyz?"XYZ_X XYZ_Y XYZ_Z":"Lab_L Lab_a Lab_b")}\nEND_DATA_FORMAT\nNUMBER_OF_SETS 24\nBEGIN_DATA\n");
        for(var i=0;i<24;i++)b.Append($"{(char)('A'+i%6)}{i/6+1} {(comma?"50,25":"50.25")} 0 0\n");
        return b.Append("END_DATA\n").ToString();
    }
    internal static DataProvenance Provenance(byte[] b,ClassicEdition e=ClassicEdition.BeforeNovember2014)=>new("SyntheticClassic",e.ToString(),"urn:pixel-tart:synthetic","Original test fixture; not measured chart data",RedistributionPermission.Permitted,Convert.ToHexString(SHA256.HashData(b)));
    private static ChartReference Parse(string s,ClassicEdition e=ClassicEdition.BeforeNovember2014,ObservationConditions? c=null)
    {var b=Encoding.UTF8.GetBytes(s);return ClassicCgatsImporter.Parse(b,Provenance(b,e),e,c??Unknown);}
    [TestMethod] public void DecimalCommaAndDotParseWithExplicitPatchNumberingAndUnknownConditions()
    {
        foreach(var comma in new[]{true,false})foreach(var edition in Enum.GetValues<ClassicEdition>())
        {var c=Parse(Synthetic(edition,comma),edition);Assert.HasCount(24,c.Patches);Assert.AreEqual("A1",c.Patches[0].Id);Assert.AreEqual("F4",c.Patches[23].Id);Assert.AreEqual(50.25,c.Patches[0].Value.X);Assert.IsNull(c.Conditions.WhiteX);Assert.Throws<InvalidOperationException>(()=>ReferenceColorValidation.ToXyz(c.Patches[0],c.Conditions));}
    }
    [TestMethod] public void EditionMismatchAndUnknownOriginatorRejectRatherThanMigrate()
    {
        Assert.Throws<InvalidDataException>(()=>Parse(Synthetic(),ClassicEdition.November2014AndNewer));
        Assert.Throws<InvalidDataException>(()=>Parse(Synthetic().Replace("Before November2014 edition","unknown")));
    }
    [TestMethod] public void HashMismatchInvalidUtf8AndMissingLicenseReject()
    {
        var b=Encoding.UTF8.GetBytes(Synthetic());var p=Provenance(b);
        Assert.Throws<InvalidDataException>(()=>ClassicCgatsImporter.Parse(b,p with{Sha256=new string('0',64)},ClassicEdition.BeforeNovember2014,Unknown));
        Assert.Throws<InvalidDataException>(()=>ClassicCgatsImporter.Parse(b,p with{License=""},ClassicEdition.BeforeNovember2014,Unknown));
        byte[] bad=[255,254];Assert.Throws<InvalidDataException>(()=>ClassicCgatsImporter.Parse(bad,Provenance(bad),ClassicEdition.BeforeNovember2014,Unknown));
    }
    [TestMethod] public void PublicDownloadCannotAuthorizeRedistribution()
    {var p=Provenance(Encoding.UTF8.GetBytes(Synthetic()));Assert.Throws<InvalidOperationException>(()=>(p with{Redistribution=RedistributionPermission.NotEstablished}).RequireRedistribution());p.RequireRedistribution();}
    [TestMethod] public void RejectMalformedCountsDuplicatePatchesNumbersAndColumns()
    {
        var s=Synthetic();foreach(var bad in new[]{s.Replace("NUMBER_OF_SETS 24","NUMBER_OF_SETS 23"),s.Replace("NUMBER_OF_FIELDS 4","NUMBER_OF_FIELDS 5"),s.Replace("B1 50.25","A1 50.25"),s.Replace("A1 50.25","A1 NaN"),s.Replace("A1 50.25","A1 101"),s.Replace("Lab_a","RGB_R"),s.Replace("END_DATA\n",""),s.Replace("A1 50.25 0 0","A1 50.25 0 0 4"),s.Replace("A1 50.25","A1 1,234.5"),s+"extra",s.Replace("NUMBER_OF_SETS 24","NUMBER_OF_SETS 24\nNUMBER_OF_SETS 24")})
            Assert.Throws<InvalidDataException>(()=>Parse(bad),bad);
    }
    [TestMethod] public void CatalogKeepsEditionsAndRejectsChangedPayloadAndConditionsUnderSameVersion()
    {
        var catalog=new ReferenceDataCatalog();var old=Parse(Synthetic());catalog.Add(old);catalog.Add(old);
        var modern=Parse(Synthetic(ClassicEdition.November2014AndNewer),ClassicEdition.November2014AndNewer);catalog.Add(modern);
        Assert.AreEqual(old,catalog.Get("SyntheticClassic",ClassicEdition.BeforeNovember2014));
        Assert.Throws<InvalidDataException>(()=>catalog.Add(old with{Conditions=D50}));
        Assert.Throws<InvalidDataException>(()=>catalog.Add(Parse(Synthetic().Replace("50.25","51.25"))));
    }
    [TestMethod] public void Xyz100ImportIsExplicitAndRangeChecked()
    {var c=Parse(Synthetic(xyz:true),c:D50);Assert.AreEqual(ReferenceEncoding.Xyz100,c.Patches[0].Encoding);Assert.AreEqual(.5025,ReferenceColorValidation.ToXyz(c.Patches[0],D50).X,1e-12);Assert.Throws<InvalidDataException>(()=>Parse(Synthetic(xyz:true).Replace("50.25","201")));}
    [TestMethod] public void GrayLabXyzRoundTripsAtBlackToeMidAndWhite()
    {foreach(var l in new[]{0d,1,8,18,50,100}){var p=new ReferencePatch(1,"A1",ReferenceEncoding.Lab,new(l,0,0));var xyz=ReferenceColorValidation.ToXyz(p,D50);var lab=ReferenceColorValidation.ToLab(xyz,D50);Assert.AreEqual(l,lab.X,1e-10);Assert.AreEqual(0,lab.Y,1e-10);Assert.AreEqual(0,lab.Z,1e-10);}}
    [TestMethod] public void BradfordMapsD50WhiteToD65AndSaturatedGamutIsNotClipped()
    {
        var white=new ReferencePatch(1,"A1",ReferenceEncoding.Lab,new(100,0,0));var rgb=ReferenceColorValidation.ToLinearRgb(white,D50,WorkingColorSpace.LinearSRgb);
        Assert.AreEqual(1,rgb.X,5e-4);Assert.AreEqual(1,rgb.Y,5e-4);Assert.AreEqual(1,rgb.Z,5e-4);
        var saturated=new ReferencePatch(1,"A1",ReferenceEncoding.Lab,new(50,120,-120));rgb=ReferenceColorValidation.ToLinearRgb(saturated,D65,WorkingColorSpace.LinearSRgb);Assert.IsTrue(rgb.X<0||rgb.Y<0||rgb.Z<0||rgb.X>1||rgb.Y>1||rgb.Z>1);
        var lab=ReferenceColorValidation.ToLab(ReferenceColorValidation.ToXyz(saturated,D65),D65);Assert.AreEqual(120,lab.Y,1e-9);Assert.AreEqual(-120,lab.Z,1e-9);
    }
    [TestMethod] public void InvalidWhiteObserverAndAngularVectorsReject()
    {Assert.Throws<InvalidDataException>(()=>(D50 with{WhiteY=.8}).Validate());Assert.Throws<InvalidDataException>(()=>(D50 with{ObserverDegrees=5}).Validate());Assert.Throws<InvalidDataException>(()=>ReferenceColorValidation.AngularErrorDegrees(new(),new(1,1,1)));Assert.Throws<NotSupportedException>(()=>ReferenceColorValidation.ToLinearRgb(new(1,"A1",ReferenceEncoding.Lab,new(50,0,0)),D50 with{ObserverDegrees=10},WorkingColorSpace.LinearSRgb));Assert.Throws<InvalidDataException>(()=>ReferenceColorValidation.ToXyz(new(1,"A1",ReferenceEncoding.Lab,new(double.NaN,0,0)),D50));Assert.AreEqual(90,ReferenceColorValidation.AngularErrorDegrees(new(1,0,0),new(0,1,0)),1e-10);}
    [TestMethod] public void SensorBlackLinearWbAndRepeatedDomainOperationsAreExplicit()
    {
        var p=Provenance([1]);var c=new PublicRawSampleContract("synthetic",p,RawValidationDomain.SensorCodeValues,new(2048,2048,2048),16383,false,6,PublicRawValidation.Version);
        var v=PublicRawValidation.SubtractBlack(new(2047,4096,8192),c);Assert.IsLessThan(0d,v.X);Assert.AreEqual(2048d/14335,v.Y,1e-12);
        var illumination=new ColorVector3(.2,.5,.3);var b=PublicRawValidation.Balance(illumination,illumination,c with{Domain=RawValidationDomain.CameraLinear});Assert.AreEqual(b.X,b.Y,1e-12);Assert.AreEqual(b.Y,b.Z,1e-12);
        Assert.Throws<InvalidOperationException>(()=>PublicRawValidation.Balance(v,illumination,c));Assert.Throws<InvalidOperationException>(()=>PublicRawValidation.Balance(v,illumination,c with{Domain=RawValidationDomain.CameraLinear,WhiteBalanceApplied=true}));Assert.Throws<InvalidOperationException>(()=>PublicRawValidation.SubtractBlack(v,c with{Domain=RawValidationDomain.DisplayEncoded}));Assert.Throws<InvalidDataException>(()=>PublicRawValidation.SubtractBlack(new(20000,0,0),c));Assert.Throws<InvalidDataException>(()=>PublicRawValidation.Balance(v,new(.2,.2,.2),c with{Domain=RawValidationDomain.CameraLinear}));
    }
}

