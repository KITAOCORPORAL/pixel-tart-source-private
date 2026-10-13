using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAWSelectionAssistant.Core.Services.ColorValidation;

public sealed record FilmStockMetadata(string Model, string Batch, DateOnly ProductionDate, string ProductionEvidence,
    string StorageHistory, double StorageMinCelsius, double StorageMaxCelsius);
public sealed record ExposureMetadata(string Camera, DateTimeOffset CapturedAt, double ShutterSeconds, double Aperture,
    double Iso, double CompensationEv, string Mode, string Illuminant, string IlluminantMeasurementEvidence);
public sealed record DevelopmentMetadata(double TemperatureCelsius, double DurationSeconds, string LightShielding,
    DateTimeOffset EjectedAt, string Handling);
public sealed record ScannerMetadata(string Device, string Serial, string Settings, int Dpi, int BitsPerChannel,
    string IccPath, string IccSha256, bool AutomaticCorrectionsDisabled, string ScannerCharacterizationVersion,
    DateTimeOffset ScannedAt);
public sealed record PairedCaptureSample(int SchemaVersion, string SampleId, string SceneId, string RawPath, string RawSha256,
    string ScanPath, string ScanSha256, FilmStockMetadata Film, ExposureMetadata DigitalExposure, ExposureMetadata InstantExposure,
    DevelopmentMetadata Development, ScannerMetadata Scanner, DateTimeOffset MeasuredAt, string MeasurementMethod,
    string CameraInputCorrectionVersion, string FilmCharacterizationVersion, string RegistrationEvidence, string QualityEvidence,
    bool IsPhysicalCapture, bool IsCalibrationClaim);
public sealed record SampleQualityReport(bool Accepted, bool CalibrationEligible, IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
public interface IPairedCaptureImporter
{
    PairedCaptureSample Read(Stream json);
    SampleQualityReport Check(PairedCaptureSample sample, string root);
}
/// <summary>No fitting/LUT generation here. Camera input, film characterization and scanner response remain independent identities.</summary>
public sealed class PairedCaptureImporter : IPairedCaptureImporter
{
    public const int SchemaVersion = 1;
    public PairedCaptureSample Read(Stream json)
    {
        try
        {
            // Reject duplicate JSON property names as well as unknown fields, not last-value-wins.
            using var document = JsonDocument.Parse(json);
            CheckDuplicates(document.RootElement);
            return document.RootElement.Deserialize<PairedCaptureSample>(new JsonSerializerOptions
            { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true })
                ?? throw new InvalidDataException("Empty paired sample.");
        }
        catch (JsonException e) { throw new InvalidDataException("Invalid paired sample metadata.", e); }
    }
    private static void CheckDuplicates(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in e.EnumerateObject()) { if(!names.Add(p.Name)) throw new InvalidDataException("Duplicate metadata field."); CheckDuplicates(p.Value); }
        }
        else if(e.ValueKind == JsonValueKind.Array) foreach(var child in e.EnumerateArray()) CheckDuplicates(child);
    }
    public SampleQualityReport Check(PairedCaptureSample s, string root)
    {
        var errors = new List<string>(); var warnings = new List<string>();
        void Need(bool condition, string reason) { if(!condition) errors.Add(reason); }
        static bool Text(string? x) => !string.IsNullOrWhiteSpace(x);
        static bool Finite(double x) => double.IsFinite(x);
        Need(s.SchemaVersion == SchemaVersion,"Unknown paired schema.");
        Need(Text(s.SampleId)&&Text(s.SceneId)&&s.IsPhysicalCapture,"Physical sample/scene identity required; no fabricated capture accepted.");
        Need(s.Film is not null && s.DigitalExposure is not null && s.InstantExposure is not null && s.Development is not null && s.Scanner is not null,"Required stage metadata absent.");
        if(errors.Count>0) return new(false,false,errors.AsReadOnly(),warnings.AsReadOnly());
        var f=s.Film!; var d=s.Development!;var scan=s.Scanner!;
        Need(Text(f.Model)&&Text(f.Batch)&&Text(f.ProductionEvidence)&&Text(f.StorageHistory)&&f.ProductionDate!=default,
            "Film model/batch/production/storage evidence required.");
        Need(Finite(f.StorageMinCelsius)&&Finite(f.StorageMaxCelsius)&&f.StorageMinCelsius<=f.StorageMaxCelsius,"Invalid storage temperature history.");
        foreach(var e in new[]{s.DigitalExposure!,s.InstantExposure!})
        {
            Need(Text(e.Camera)&&Text(e.Mode)&&Text(e.Illuminant)&&Text(e.IlluminantMeasurementEvidence)&&e.CapturedAt!=default,"Exposure/light evidence missing.");
            Need(Finite(e.ShutterSeconds)&&e.ShutterSeconds>0&&Finite(e.Aperture)&&e.Aperture>0&&Finite(e.Iso)&&e.Iso>0&&Finite(e.CompensationEv),"Invalid exposure numbers.");
        }
        Need(s.InstantExposure!.Camera == "Polaroid I-2","Instant capture must identify I-2, not digital response.");
        Need(f.ProductionDate<=DateOnly.FromDateTime(s.InstantExposure.CapturedAt.UtcDateTime),"Production after capture.");
        Need(Finite(d.TemperatureCelsius)&&Finite(d.DurationSeconds)&&d.DurationSeconds>0&&Text(d.LightShielding)&&Text(d.Handling)&&d.EjectedAt!=default,
            "Development temperature/time/handling missing.");
        Need(d.EjectedAt>=s.InstantExposure.CapturedAt&&scan.ScannedAt>=d.EjectedAt.AddSeconds(Finite(d.DurationSeconds)&&d.DurationSeconds is >0 and <864000?d.DurationSeconds:0)&&s.MeasuredAt>=scan.ScannedAt,
            "Invalid capture/development/scan/measurement chronology.");
        Need(Text(scan.Device)&&Text(scan.Serial)&&Text(scan.Settings)&&scan.Dpi>0&&scan.BitsPerChannel is 8 or 16&&scan.ScannedAt!=default,"Scanner settings missing.");
        Need(Text(s.CameraInputCorrectionVersion)&&Text(s.FilmCharacterizationVersion)&&Text(s.RegistrationEvidence)&&Text(s.QualityEvidence)&&Text(s.MeasurementMethod),"Stage/registration/quality/measurement identity missing.");
        VerifyFile(root,s.RawPath,s.RawSha256,errors); VerifyFile(root,s.ScanPath,s.ScanSha256,errors); VerifyFile(root,scan.IccPath,scan.IccSha256,errors);
        if(!scan.AutomaticCorrectionsDisabled) warnings.Add("Scanner automatic correction prevents film-only attribution.");
        if(!Text(scan.ScannerCharacterizationVersion)) warnings.Add("Scanner response uncharacterized: scanned bias cannot be fitted as film response.");
        if(s.FilmCharacterizationVersion == "CreativePresetPendingMeasurement") warnings.Add("Creative preset / pending physical measurement; not calibrated i-Type/600 LUT.");
        if(d.TemperatureCelsius is <13 or >28) warnings.Add("Outside general manufacturer development temperature guidance; record, do not correct.");
        warnings.Add("Metadata/hash QC only: pixel registration, clipping, blur, scanner and film measurement review not performed.");
        if(s.IsCalibrationClaim) errors.Add("This batch never admits calibration claims; independent physical calibration review required.");
        return new(errors.Count==0,false,errors.AsReadOnly(),warnings.AsReadOnly());
    }
    private static void VerifyFile(string root,string path,string hash,List<string> errors)
    {
        try
        {
            var basePath=Path.GetFullPath(root)+Path.DirectorySeparatorChar;
            if(string.IsNullOrWhiteSpace(path)||Path.IsPathRooted(path)) throw new InvalidDataException("Expected root-relative artifact path.");
            var full=Path.GetFullPath(Path.Combine(root,path));
            if(!full.StartsWith(basePath,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Artifact escapes sample root.");
            var current=basePath.TrimEnd(Path.DirectorySeparatorChar);
            foreach(var part in Path.GetRelativePath(root,full).Split(Path.DirectorySeparatorChar))
            { current=Path.Combine(current,part); if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new InvalidDataException("Reparse artifacts refused."); }
            using var file=File.OpenRead(full);
            if(!Convert.ToHexString(SHA256.HashData(file)).Equals(hash,StringComparison.OrdinalIgnoreCase))errors.Add("Artifact hash mismatch: "+path);
        }
        catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { errors.Add("Artifact rejected: "+path+" ("+e.GetType().Name+")"); }
    }
}
