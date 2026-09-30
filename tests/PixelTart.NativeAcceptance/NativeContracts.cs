using System.Diagnostics;
using System.Text.Json;

namespace PixelTart.NativeAcceptance;

internal readonly record struct ScreenPoint(int X, int Y);
internal readonly record struct ScreenBounds(double X, double Y, double Width, double Height)
{
    public bool Valid => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
    public bool Contains(ScreenPoint p) => Valid && p.X >= X && p.X < X + Width && p.Y >= Y && p.Y < Y + Height;
    public ScreenPoint Point(double x, double y)
    {
        if (!Valid || !double.IsFinite(x) || !double.IsFinite(y) || x is < 0 or >= 1 || y is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(x));
        return new(checked((int)Math.Floor(X + Width * x)), checked((int)Math.Floor(Y + Height * y)));
    }
}
internal static class NativeCoordinateMapper
{
    public static void ValidateDpi(ScreenBounds bounds, double dipWidth, double dipHeight, double scaleX, double scaleY, uint windowDpi)
    {
        if (!bounds.Valid || !double.IsFinite(dipWidth) || !double.IsFinite(dipHeight) || dipWidth <= 0 || dipHeight <= 0 ||
            !double.IsFinite(scaleX) || !double.IsFinite(scaleY) || scaleX <= 0 || scaleY <= 0 || windowDpi == 0 ||
            Math.Abs(scaleX - windowDpi / 96d) > .01 || Math.Abs(scaleY - windowDpi / 96d) > .01 ||
            Math.Abs(bounds.Width - dipWidth * scaleX) > 1 || Math.Abs(bounds.Height - dipHeight * scaleY) > 1)
            throw new InvalidDataException("Observer DIP/physical bounds disagree with target HWND DPI.");
    }
    public static (int X, int Y) Absolute(ScreenPoint point, ScreenBounds desktop)
    {
        if (!desktop.Contains(point) || desktop.Width <= 1 || desktop.Height <= 1) throw new ArgumentOutOfRangeException(nameof(point));
        return ((int)Math.Round((point.X - desktop.X) * 65535 / (desktop.Width - 1)),
            (int)Math.Round((point.Y - desktop.Y) * 65535 / (desktop.Height - 1)));
    }
}

internal sealed record TargetIdentity(int TargetPid, long TargetHwnd, string ExpectedProcessPath, string ActualProcessPath,
    int WindowPid, long ProcessStartTicks, long ExpectedStartTicks, bool Alive, bool Foreground, uint Dpi, ScreenBounds Bounds);

internal static class TargetGuard
{
    public static void Validate(TargetIdentity target, ScreenPoint? point = null)
    {
        if (!target.Alive || !target.Foreground || target.TargetPid <= 0 || target.TargetHwnd == 0 || target.WindowPid != target.TargetPid ||
            target.ProcessStartTicks != target.ExpectedStartTicks || target.Dpi == 0 || !target.Bounds.Valid ||
            !Path.GetFileName(target.ExpectedProcessPath).Equals("KitaoPhotoSelector.exe", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(target.ExpectedProcessPath).Equals(Path.GetFullPath(target.ActualProcessPath), StringComparison.OrdinalIgnoreCase) ||
            point is { } p && !target.Bounds.Contains(p))
            throw new InvalidOperationException("Scoped Pixel Tart PID/HWND/path/start-time/foreground/bounds guard rejected operation.");
    }
}

internal sealed class NativeWait
{
    public static async Task<T> UntilAsync<T>(Func<CancellationToken, Task<T>> read, Func<T, bool> predicate,
        TimeSpan timeout, CancellationToken token = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        try
        {
            while (true)
            {
                var state = await read(deadline.Token);
                if (predicate(state)) return state;
                await Task.Delay(25, deadline.Token); // polling cadence, never the success predicate
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Native state predicate was not satisfied."); }
    }
}

internal sealed class NativeEvidenceReader(string fixtureDirectory)
{
    private readonly string _directory = Path.GetFullPath(fixtureDirectory);
    private readonly SemaphoreSlim _serial = new(1, 1);
    public async Task<JsonElement> ReadAsync(CancellationToken token)
    {
        await _serial.WaitAsync(token);
        try
        {
            var nonce = Guid.NewGuid().ToString();
            await File.WriteAllTextAsync(Path.Combine(_directory, "native-observe-request.txt"), nonce, token);
            return await NativeWait.UntilAsync(async ct =>
            {
                try
                {
                    await using var file = new FileStream(Path.Combine(_directory, "native-observe-response.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var document = await JsonDocument.ParseAsync(file, cancellationToken: ct);
                    return document.RootElement.Clone();
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return default; }
            }, state => state.ValueKind == JsonValueKind.Object && state.GetProperty("Nonce").GetString() == nonce, TimeSpan.FromSeconds(5), token);
        }
        finally { _serial.Release(); }
    }
    public static bool NodesSynchronized(JsonElement state)
    {
        if (!state.GetProperty("ModelRenderSynchronized").GetBoolean() || state.GetProperty("Drag").GetProperty("Active").GetBoolean()) return false;
        var model = state.GetProperty("ModelIds").EnumerateArray().Select(p => p.GetString()).ToArray();
        var visual = state.GetProperty("Drag").GetProperty("Rows").EnumerateArray().Select(p => p.GetProperty("Id").GetString()).ToArray();
        var render = state.GetProperty("RenderedNodeIds").EnumerateArray().Select(p => p.GetString()).ToArray();
        return model.Length > 0 && model.SequenceEqual(visual) && model.SequenceEqual(render);
    }
    public static void ValidateFreshNonce(JsonElement response, string nonce, DateTimeOffset now)
    {
        if (!Guid.TryParse(nonce, out _) || response.GetProperty("Nonce").GetString() != nonce ||
            now - response.GetProperty("Timestamp").GetDateTimeOffset() is var age && (age < TimeSpan.Zero || age > TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("Native input requires a matching, fresh product observer response.");
    }
}
