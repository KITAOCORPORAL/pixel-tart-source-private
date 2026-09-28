using System.Security.Cryptography;
using System.Text;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>
/// Immutable identity for one Match V4 analysis.  It contains only values required to
/// reproduce the analysis/execution contract; a GPU executor is not allowed to mutate it.
/// </summary>
public sealed record MatchTransformV4(
    string SourceIdentity,
    string ReferenceIdentity,
    ReferenceMatchV4Settings Settings,
    int AlgorithmRevision = 1)
{
    public string CanonicalText => string.Join("|",
        SourceIdentity, ReferenceIdentity, "v4", AlgorithmRevision,
        Settings.ComputeQuality, Settings.BackendSemanticsVersion,
        Settings.MaximumRepresentativeSamples, Settings.SinkhornIterations,
        Settings.Regularization.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Settings.MaximumLuminanceDisplacement.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Settings.MaximumChromaDisplacement.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Settings.ResidualIterations, Settings.ResidualStopThreshold.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Settings.AnalysisResolution, Settings.TileSize, Settings.TileOverlap,
        Settings.NeutralProtection.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Settings.SkinProtection.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Settings.HighlightProtection.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Settings.ShadowProtection.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

    public string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText)));

    public MatchTransformV4 Normalize()
    {
        if (string.IsNullOrWhiteSpace(SourceIdentity)) throw new ArgumentException("Source identity is required.", nameof(SourceIdentity));
        if (string.IsNullOrWhiteSpace(ReferenceIdentity)) throw new ArgumentException("Reference identity is required.", nameof(ReferenceIdentity));
        if (AlgorithmRevision <= 0) throw new ArgumentOutOfRangeException(nameof(AlgorithmRevision));
        Settings.Validate();
        return this with { SourceIdentity = SourceIdentity.Trim(), ReferenceIdentity = ReferenceIdentity.Trim() };
    }
}
