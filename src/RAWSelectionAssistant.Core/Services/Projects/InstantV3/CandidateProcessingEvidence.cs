namespace Kitao.InstantFilm.Research;
/// <summary>Scoped observation of actual phases. No production scheduling or cancellation policy is changed.</summary>
public static class CandidateProcessingEvidence
{
 private static readonly AsyncLocal<Action<string>?> Observer=new();
 public static IDisposable Capture(Action<string> observer){var previous=Observer.Value;Observer.Value=observer;return new Scope(()=>Observer.Value=previous);}
 public static void Enter(string phase,CancellationToken token){Observer.Value?.Invoke(phase);token.ThrowIfCancellationRequested();}
 private sealed class Scope(Action restore):IDisposable{public void Dispose()=>restore();}
}
