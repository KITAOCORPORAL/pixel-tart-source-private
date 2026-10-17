using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research;

public sealed record CandidateResponse(double ExposureEv=0, double Toe=.025, double Shoulder=.3,
    double Black=.012, double Saturation=.9, double ApparentDensity=1);
public sealed record CandidateProvenance(string EvidenceStatus, IReadOnlyList<string> SourceIds, IReadOnlyList<string> SampleIds,
    IReadOnlyList<string> Observations, IReadOnlyList<string> ContraryEvidence, bool CalibrationEligible=false);
public sealed record CandidateProfile(string Id, string LegacyId, string ChemicalFamily, string PaperEra,
    string Palette, CandidateResponse Response, CandidateProvenance Provenance);
public sealed record CandidateIdentity(string ChemicalFamily,string PaperEra,int ProfileVersion=1);
public sealed record CandidateOptics(int Version=1, double DetailMix=.1, double HighlightDiffusion=.07, int Radius=2);
public sealed record CandidateEmulsion(int Version=1, double Microstructure=.0015, double DevelopmentUnevenness=.004,
    double DevelopmentDefects=0, double ExpiredAging=0);
public sealed record CandidateSurface(int Version=1, bool Frame=true, string BaseColor="Warm", double PaperTexture=.002,
    double PaperScratches=0, double ScanDust=0);
public sealed record CandidateRecipe(int AlgorithmVersion, string ProfileId, CandidateIdentity Identity, CandidateResponse Response,
    CandidateProvenance Provenance, CandidateOptics Optics, CandidateEmulsion Emulsion, CandidateSurface Surface,
    InstantPaperGeometry Geometry, InstantRandomSnapshot Random, double Amount=1)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CandidateCreativeControls? Creative {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CandidatePaperControls? Paper {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CandidateDefectControls? Defects {get;init;}
    public const string Domain="OrientedDisplaySrgbD65Float-EotfOnce-LinearResponseOpticsEmulsion-OetfOnce-PaperScanV3";
    public const string Admission="ExplicitCreativeV3_BaseNodesScopedAdmission20261017";
    public CandidateProfile Profile=>CandidateProfiles.Available.Single(p=>p.Id==ProfileId);
    public CandidateRecipe Validate()
    {
        if(AlgorithmVersion!=3 || !CandidateProfiles.Available.Any(p=>p.Id==ProfileId) || Identity is null || Response is null || Provenance is null || Optics is null || Emulsion is null || Surface is null || Geometry is null || Random is null)
            throw new InvalidDataException("Incomplete/unknown research recipe.");
        static void Range(double v,double lo,double hi){if(!double.IsFinite(v)||v<lo||v>hi)throw new InvalidDataException("Candidate parameter out of bounds.");}
        if(Identity.ProfileVersion!=1||Identity.ChemicalFamily!=Profile.ChemicalFamily||Identity.PaperEra!=Profile.PaperEra)throw new InvalidDataException("Frozen candidate identity differs from registered version.");
        Range(Amount,0,1); Range(Response.ExposureEv,-2,2);Range(Response.Toe,0,.3);Range(Response.Shoulder,0,2);
        Range(Response.Black,0,.15);Range(Response.Saturation,0,1.5);Range(Response.ApparentDensity,.5,2);
        if(Provenance.EvidenceStatus!="CreativeSimulation"||Provenance.CalibrationEligible||Provenance.SourceIds is null||Provenance.SampleIds is null||Provenance.Observations is null||Provenance.ContraryEvidence is null)
            throw new InvalidDataException("Uncontrolled research recipes cannot assert calibration.");
        if(Optics.Version!=1||Optics.Radius!=2||Emulsion.Version!=1||Surface.Version!=1||Surface.BaseColor is not("Warm" or "Neutral" or "Cool"))throw new InvalidDataException("Unknown candidate component version.");
        foreach(var v in new[]{Optics.DetailMix,Optics.HighlightDiffusion,Emulsion.Microstructure,Emulsion.DevelopmentUnevenness,Emulsion.DevelopmentDefects,Emulsion.ExpiredAging,Surface.PaperTexture,Surface.PaperScratches,Surface.ScanDust})Range(v,0,1);
        if(Creative is{} creative){if(creative.Version is not(1 or 2)||creative.Stock!="Base"&&!Enumerable.Range(0,6).Any(i=>creative.Stock=="Womb01351.Stock"+i))throw new InvalidDataException("Unknown creative version");foreach(var v in new[]{creative.ShadowCast,creative.HighlightWarmth,creative.TemperatureAppearance})Range(v,-1,1);foreach(var v in new[]{creative.Fog,creative.FrameDrift,creative.Fade,creative.LensVignette,creative.DevelopmentEdge})Range(v,0,1);}
        if(Paper is{} paper){if(paper.Version is not(1 or 2)||paper.Pattern is not("Plain" or "Leather" or "Dots" or "Stripes" or "Foil")||paper.Window is not("Rectangle" or "Circle"))throw new InvalidDataException("Unknown paper version");foreach(var v in new[]{paper.Emboss,paper.EdgeLine,paper.Reflection})Range(v,0,1);Range(paper.ReflectionAngle,-180,180);Range(paper.ReflectionWidth,.01,1);Range(paper.Zoom,1,4);if(paper.Version==1&&paper.Zoom!=1)throw new NotSupportedException("Zoom requires explicit paper component2");}
        if(Defects is{} defect){if(defect.Version!=1||defect.Streams!="V3IndependentStreams1"||defect.Kind is not("None" or "Unspread" or "Bands" or "Fog" or "Islands" or "Crescent" or "BlueFailure"))throw new InvalidDataException("Unknown defect version");foreach(var v in new[]{defect.Amount,defect.Crystal,defect.Leak})Range(v,0,1);}
        Geometry.Validate();
        new InstantFilmDefinition(true,Profile.LegacyId,InstantFilmProfiles.Available.Single(p=>p.Id==Profile.LegacyId).ChemicalGeneration,DiceEnabled:true,Tier:Random.Tier,Snapshot:Random).Validate();
        return this;
    }
    public CandidateRecipe Freeze()
    {
        Validate();
        return this with
        {
            Provenance=Provenance with { SourceIds=Array.AsReadOnly(Provenance.SourceIds.ToArray()),
                SampleIds=Array.AsReadOnly(Provenance.SampleIds.ToArray()), Observations=Array.AsReadOnly(Provenance.Observations.ToArray()),
                ContraryEvidence=Array.AsReadOnly(Provenance.ContraryEvidence.ToArray()) },
            Random=Random with { Geometry=Array.AsReadOnly(Random.Geometry.ToArray()) }
        };
    }
    public static CandidateRecipe Create(string id,ulong seed=20261016)
    {
        var p=CandidateProfiles.Available.Single(p=>p.Id==id);
        var old=InstantFilmProfiles.Available.Single(old=>old.Id==p.LegacyId);
        var d=new InstantFilmDefinition(true,old.Id,old.ChemicalGeneration);
        return new(3,id,new(p.ChemicalFamily,p.PaperEra),p.Response,p.Provenance,new(),new(),new(),new(),InstantFilmRandom.Roll(d,seed));
    }
}
public static class CandidateProfiles
{
    private static CandidateProvenance Evidence(string[] samples,string observation)=>new("CreativeSimulation",samples.Select(x=>x+":source").ToArray(),samples,[observation],
        ["Exposure, scan transfer, postprocessing and manufacturing batch uncontrolled; no measured paired scene; provider observations not physical truth."]);
    // Same reference response object for i-Type and 600: no invented separation by marketing name.
    private static readonly CandidateResponse Modern=new();
    public static IReadOnlyList<CandidateProfile> Available {get;}=Array.AsReadOnly(new[]{
        new CandidateProfile("modern-itype-v3","itype-color","ModernIntegralColor","Modern; batch unknown","Color",Modern,Evidence(["GROK-POL-0001","GROK-POL-0002","GROK-POL-0003"],"Provider reports lifted shadows/warm highlights in these scans only; creative hypothesis.")),
        new CandidateProfile("modern-600-v3","600-color","ModernIntegralColor","Modern; batch unknown","Color",Modern,Evidence(["GROK-POL-0018"],"No verified evidence for a separate i-Type/600 response; shared modern candidate.")),
        new CandidateProfile("modern-bw-v3","itype-bw","ModernIntegralSilver","Modern; batch unknown","NeutralBW",new(Saturation:0),Evidence(["GROK-POL-0020","GROK-POL-0021"],"Literature links only; neutral linear-luminance response is a creative choice.")),
        new CandidateProfile("sx70-v3","sx70-color","SX70CreativeCandidate","Modern SX-70 candidate, not historical Time-Zero","Color",new(Toe:.04,Shoulder:.4,Black:.018,Saturation:.83),Evidence(["GROK-POL-0011"],"General technical context, not measured SX-70 color; warm response is creative.")),
        new CandidateProfile("reclaimed-blue-v3","reclaimed-blue600","ReclaimedBlue","Modern special edition; batch unknown","WhiteBlue",new(Saturation:0),Evidence(["GROK-POL-0016","GROK-POL-0017"],"Blue-to-light mapping is a creative interpretation; no fetched controlled measurement.")),
        new CandidateProfile("blue-duochrome-v3","blue-duochrome600","BlueDuochrome","Modern special edition; batch unknown","BlackBlue",new(Black:.004,Saturation:0),Evidence(["GROK-POL-0017"],"Black-to-blue palette intentionally differs from Reclaimed Blue; literature context only."))});
    public static IReadOnlyList<(string Id,string Status,string Reason)> ResearchOnly {get;}=Array.AsReadOnly(new[]{
        ("reclaimed-green600","AwaitingEvidence","No admitted source/image pair or response"),
        ("go","AwaitingEvidence","Format/era and scan evidence incomplete"),
        ("type55","HistoricalContextOnly","Not modern i-Type color calibration"),
        ("type665","HistoricalContextOnly","Not modern i-Type color calibration"),
        ("type669","HistoricalContextOnly","Not modern i-Type color calibration"),
        ("time-zero","HistoricalAgedContextOnly","Not current SX-70 calibration"),
        ("spectra","AwaitingEvidence","No candidate"),("8x10","AwaitingEvidence","No candidate")});
}
