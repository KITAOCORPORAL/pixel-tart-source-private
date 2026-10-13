using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.ColorValidation;

public enum ReferenceEncoding { Lab, Xyz100 }
public enum ClassicEdition { BeforeNovember2014, November2014AndNewer }
public enum RedistributionPermission { NotEstablished, Permitted }
public sealed record DataProvenance(string Dataset, string Version, string SourceUri, string License,
    RedistributionPermission Redistribution, string Sha256)
{
    public void Validate()
    {
        if (new[] { Dataset, Version, License }.Any(string.IsNullOrWhiteSpace) ||
            !Uri.TryCreate(SourceUri, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http" or "urn") ||
            !Regex.IsMatch(Sha256 ?? "", "^[a-fA-F0-9]{64}$") || !Enum.IsDefined(Redistribution))
            throw new InvalidDataException("Incomplete source/version/license/hash identity.");
    }
    public void RequireRedistribution()
    {
        Validate();
        if (Redistribution != RedistributionPermission.Permitted)
            throw new InvalidOperationException("Public download is not redistribution permission.");
    }
}
// Null is an explicit unknown, never a silent D50/2-degree default.
public sealed record ObservationConditions(string? Illuminant, double? WhiteX, double? WhiteY,
    int? ObserverDegrees, string? MeasurementCondition, string Evidence)
{
    public void Validate(bool requireKnown = false)
    {
        if (string.IsNullOrWhiteSpace(Evidence) || (WhiteX is null) != (WhiteY is null) ||
            WhiteX is {} x && (!double.IsFinite(x) || !double.IsFinite(WhiteY!.Value) || x <= 0 || WhiteY <= 0 || x + WhiteY >= 1) ||
            ObserverDegrees is not (null or 2 or 10)) throw new InvalidDataException("Invalid observation conditions.");
        if (requireKnown && (WhiteX is null || ObserverDegrees is null || string.IsNullOrWhiteSpace(Illuminant)))
            throw new InvalidOperationException("White point/observer unknown: conversion refused.");
    }
}
public sealed record ReferencePatch(int Number, string Id, ReferenceEncoding Encoding, ColorVector3 Value);
public sealed record ChartReference(DataProvenance Provenance, ClassicEdition Edition, ObservationConditions Conditions,
    IReadOnlyList<ReferencePatch> Patches, string Originator);
// A camera response cannot be passed to a reference color API without an explicit calibration transform.
public sealed record CameraPatchResponse(string SampleId, string PatchId, ColorVector3 CameraLinearRgb,
    DataProvenance Provenance, string InputCorrectionVersion);

/// <summary>Strict supported CGATS subset; X-Rite Classic Lab and explicit XYZ100 imports. Original bytes are hashed first.</summary>
public static partial class ClassicCgatsImporter
{
    public const string ParserVersion = "ClassicCgatsV1";
    public static ChartReference Parse(ReadOnlySpan<byte> bytes, DataProvenance source, ClassicEdition expectedEdition,
        ObservationConditions conditions)
    {
        source.Validate(); conditions.Validate();
        if (!Enum.IsDefined(expectedEdition)) throw new InvalidDataException("Unknown chart edition.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!hash.Equals(source.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Source hash mismatch.");
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException e) { throw new InvalidDataException("Invalid UTF-8.", e); }
        text = text.TrimStart('\uFEFF');
        var lines = text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith('#')).ToArray();
        string? origin = null; int? fields = null, sets = null; string[]? columns = null;
        var patches = new List<ReferencePatch>(); var state = 0; var sawFormat = false; var sawData = false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (line == "BEGIN_DATA_FORMAT") { if (state != 0 || sawFormat) Fail("Duplicate/misordered format."); state = 1; sawFormat = true; continue; }
            if (line == "END_DATA_FORMAT") { if (state != 1 || columns is null) Fail("Missing format."); state = 0; continue; }
            if (line == "BEGIN_DATA") { if (state != 0 || !sawFormat || columns is null || sawData || fields != 4 || sets != 24) Fail("Invalid data header."); state = 2; sawData = true; continue; }
            if (line == "END_DATA") { if (state != 2) Fail("Misordered data end."); state = 3; continue; }
            if (state == 3) Fail("Unexpected content after data.");
            if (state == 1) { if (columns is not null) Fail("Multiple column lines."); columns = Tokens(line); continue; }
            if (state == 2)
            {
                var v = Tokens(line);
                if (v.Length != 4 || !PatchId().IsMatch(v[0]) || !ids.Add(v[0])) Fail("Invalid/duplicate patch or field count.");
                var encoding = columns!.SequenceEqual(new[] { "SAMPLE_NAME", "Lab_L", "Lab_a", "Lab_b" }) ? ReferenceEncoding.Lab :
                    columns.SequenceEqual(new[] { "SAMPLE_NAME", "XYZ_X", "XYZ_Y", "XYZ_Z" }) ? ReferenceEncoding.Xyz100 : throw new InvalidDataException("Unsupported columns.");
                var value = new ColorVector3(Number(v[1]), Number(v[2]), Number(v[3]));
                if (encoding == ReferenceEncoding.Lab ? value.X is < 0 or > 100 || Math.Abs(value.Y) > 160 || Math.Abs(value.Z) > 160 :
                    value.X is < 0 or > 200 || value.Y is < 0 or > 100 || value.Z is < 0 or > 200) Fail("Patch range outside supported reflective reference bounds.");
                patches.Add(new((v[0][1] - '1') * 6 + v[0][0] - 'A' + 1, v[0], encoding, value)); continue;
            }
            if (line.StartsWith("ORIGINATOR ", StringComparison.Ordinal)) { if (origin is not null) Fail("Duplicate originator."); origin = line[11..].Trim('"'); }
            else if (line.StartsWith("NUMBER_OF_FIELDS ", StringComparison.Ordinal)) { if (fields is not null) Fail("Duplicate field count."); fields = Integer(line[17..]); }
            else if (line.StartsWith("NUMBER_OF_SETS ", StringComparison.Ordinal)) { if (sets is not null) Fail("Duplicate set count."); sets = Integer(line[15..]); }
            // Vendor descriptive preamble is retained in original bytes, not interpreted as numeric conditions.
        }
        if (state != 3 || patches.Count != 24 || origin is null) Fail("Incomplete Classic reference.");
        var actual = origin!.Contains("Before November2014 edition", StringComparison.Ordinal) ? ClassicEdition.BeforeNovember2014 :
            origin.Contains("November2014 edition and newer", StringComparison.Ordinal) ? ClassicEdition.November2014AndNewer : throw new InvalidDataException("Unknown originator/version.");
        if (actual != expectedEdition || source.Version != actual.ToString()) Fail("Version conflict: no automatic edition migration.");
        return new(source, actual, conditions, Array.AsReadOnly(patches.OrderBy(x => x.Number).ToArray()), origin);
    }
    private static string[] Tokens(string line) => Regex.Split(line, @"\s+");
    private static int Integer(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : throw new InvalidDataException("Invalid integer.");
    private static double Number(string value)
    {
        // Both official editions supported; commas are decimal separators, never CSV delimiters or thousands grouping.
        if (!NumericToken().IsMatch(value) || !double.TryParse(value.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw new InvalidDataException("Invalid numeric token.");
        return n;
    }
    private static void Fail(string message) => throw new InvalidDataException(message);
    [GeneratedRegex("^[A-F][1-4]$")] private static partial Regex PatchId();
    [GeneratedRegex(@"^[+-]?\d+(?:[.,]\d+)?(?:[eE][+-]?\d+)?$")] private static partial Regex NumericToken();
}

/// <summary>Separate editions coexist. A changed payload or changed conditions under the same identity is rejected, not replaced.</summary>
public sealed class ReferenceDataCatalog
{
    private readonly Dictionary<(string, string), ChartReference> entries = [];
    public void Add(ChartReference reference)
    {
        var key = (reference.Provenance.Dataset, reference.Provenance.Version);
        if (entries.TryGetValue(key, out var old))
        {
            if (old.Provenance != reference.Provenance || old.Edition != reference.Edition || old.Conditions != reference.Conditions || old.Originator != reference.Originator || !old.Patches.SequenceEqual(reference.Patches)) throw new InvalidDataException("Reference identity conflict; assign a new version and review.");
            return;
        }
        entries.Add(key, reference);
    }
    public ChartReference Get(string dataset, ClassicEdition edition) => entries[(dataset, edition.ToString())];
}
