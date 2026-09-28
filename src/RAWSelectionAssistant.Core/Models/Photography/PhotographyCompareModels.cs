using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Models.Photography;

public readonly record struct FacePoint(double X, double Y, double Confidence = 1);
public sealed record FaceObservation(
    double X, double Y, double Width, double Height,
    FacePoint? LeftEye = null, FacePoint? RightEye = null,
    double Confidence = 0,
    string? SubjectKey = null)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public bool HasEyes => LeftEye is not null && RightEye is not null;
    public FacePoint EyeMidpoint => HasEyes ? new((LeftEye!.Value.X + RightEye!.Value.X) / 2, (LeftEye!.Value.Y + RightEye!.Value.Y) / 2, Math.Min(LeftEye.Value.Confidence, RightEye.Value.Confidence)) : new(CenterX, CenterY, Confidence);
    public double EyeDistance => HasEyes ? Math.Sqrt(Math.Pow(RightEye!.Value.X - LeftEye!.Value.X, 2) + Math.Pow(RightEye.Value.Y - LeftEye.Value.Y, 2)) : 0;
    public double RollDegrees => HasEyes ? Math.Atan2(RightEye!.Value.Y - LeftEye!.Value.Y, RightEye.Value.X - LeftEye.Value.X) * 180 / Math.PI : 0;
}

public enum FaceLockFallback { None, NoFace, LowConfidence, Ambiguous }
public sealed record FaceLockTransform(double TranslateX, double TranslateY, double Scale, double RollDegrees, FaceLockFallback Fallback = FaceLockFallback.None)
{
    public static FaceLockTransform Identity(FaceLockFallback fallback = FaceLockFallback.None) => new(0, 0, 1, 0, fallback);

    /// <summary>Scale, then rotate about the origin, then translate (WPF TransformGroup order).</summary>
    public FacePoint Apply(FacePoint point)
    {
        var radians = RollDegrees * Math.PI / 180;
        var x = point.X * Scale;
        var y = point.Y * Scale;
        return new(x * Math.Cos(radians) - y * Math.Sin(radians) + TranslateX,
            x * Math.Sin(radians) + y * Math.Cos(radians) + TranslateY,
            point.Confidence);
    }
}

public static class FaceLockPlanner
{
    public static FaceObservation? SelectPrimary(IEnumerable<FaceObservation> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        return faces.Where(face => face.Confidence > 0).OrderByDescending(face => face.HasEyes).ThenByDescending(face => face.EyeDistance).ThenByDescending(face => face.Width * face.Height).ThenBy(face => face.Y).ThenBy(face => face.X).ThenBy(face => face.SubjectKey, StringComparer.Ordinal).FirstOrDefault();
    }

    public static FaceLockTransform Plan(FaceObservation? source, FaceObservation? target, double minimumConfidence = .35)
    {
        if (source is null || target is null) return FaceLockTransform.Identity(FaceLockFallback.NoFace);
        if (source.Confidence < minimumConfidence || target.Confidence < minimumConfidence) return FaceLockTransform.Identity(FaceLockFallback.LowConfidence);
        var sourcePoint = source.EyeMidpoint; var targetPoint = target.EyeMidpoint;
        var sourceDistance = source.EyeDistance > 1e-6 ? source.EyeDistance : Math.Max(source.Width, source.Height);
        var targetDistance = target.EyeDistance > 1e-6 ? target.EyeDistance : Math.Max(target.Width, target.Height);
        if (sourceDistance <= 1e-6 || targetDistance <= 1e-6) return FaceLockTransform.Identity(FaceLockFallback.LowConfidence);
        var scale = targetDistance / sourceDistance;
        var roll = target.RollDegrees - source.RollDegrees;
        var radians = roll * Math.PI / 180;
        return new(targetPoint.X - scale * (sourcePoint.X * Math.Cos(radians) - sourcePoint.Y * Math.Sin(radians)),
            targetPoint.Y - scale * (sourcePoint.X * Math.Sin(radians) + sourcePoint.Y * Math.Cos(radians)), scale, roll);
    }
}

public enum CompareAction { Rating, Pick, Reject }
public sealed record CompareViewport(double Zoom = 1, double PanX = 0, double PanY = 0)
{
    public CompareViewport Normalize() => new(Math.Clamp(Zoom, 1, 2), PanX, PanY);
}
public sealed record TwoUpCompareState(Guid PrimaryId, Guid ChallengerId, CompareViewport Viewport, bool FaceLockEnabled = false, bool IsSwapped = false)
{
    public TwoUpCompareState Swap() => this with { PrimaryId = ChallengerId, ChallengerId = PrimaryId, IsSwapped = !IsSwapped };
    public TwoUpCompareState WithViewport(CompareViewport viewport) => this with { Viewport = viewport.Normalize() };
}
public sealed record RapidCompareState(Guid BestId, Guid ChallengerId)
{
    public RapidCompareState PromoteChallenger(Guid nextChallenger) => new(ChallengerId, nextChallenger);
}

/// <summary>Platform adapter boundary. A missing detector must remain observable by the product UI.</summary>
public interface IFaceObservationProvider
{
    bool IsAvailable { get; }
    string Status { get; }
    Task<IReadOnlyList<FaceObservation>> DetectAsync(string imageIdentity, CancellationToken cancellationToken = default);
}

public sealed class UnavailableFaceObservationProvider : IFaceObservationProvider
{
    public bool IsAvailable => false;
    public string Status => "FACE DETECTOR NOT AVAILABLE";
    public Task<IReadOnlyList<FaceObservation>> DetectAsync(string imageIdentity, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FaceObservation>>([]);
}
