using System.Runtime.InteropServices;
using System.Text.Json;

namespace PixelTart.NativeAcceptance;

internal sealed class PhysicalDpiScope : IDisposable
{
    private readonly nint _previous = Win32.SetThreadDpiAwarenessContext(new nint(-4)); // per-monitor v2, thread-local only
    public PhysicalDpiScope() { if (_previous == 0) throw new InvalidOperationException("Cannot establish physical coordinate DPI scope."); }
    public void Dispose() => Win32.SetThreadDpiAwarenessContext(_previous);
}

internal sealed class NativeInput(PixelTartProcessHost host)
{
    private long _sequence;
    private readonly object _gate = new();
    private readonly string _log = Path.Combine(host.RuntimeRoot, "native-input.jsonl");
    private readonly HashSet<ushort> _heldKeys = [];
    private ScreenPoint? _lastPoint;
    private bool _leftHeld;

    public void Move(ScreenPoint point, string control, string nonce)
    {
        using var dpi = new PhysicalDpiScope();
        var desktop = new ScreenBounds(Win32.GetSystemMetrics(76), Win32.GetSystemMetrics(77), Win32.GetSystemMetrics(78), Win32.GetSystemMetrics(79));
        var absolute = NativeCoordinateMapper.Absolute(point, desktop);
        Send(new() { Type = 0, Mouse = new() { X = absolute.X, Y = absolute.Y, Flags = 0x8000 | 0x4000 | 0x0001 } }, "MouseMove", point, control, nonce);
        _lastPoint = point;
    }
    public void LeftDown(string control, string nonce)
    {
        if (_lastPoint is null || _leftHeld) throw new InvalidOperationException("Move before mouse down; no duplicate down.");
        Send(new() { Type = 0, Mouse = new() { Flags = 2 } }, "LeftDown", _lastPoint, control, nonce, "Left"); _leftHeld = true;
    }
    public void LeftUp(string control, string nonce)
    {
        if (!_leftHeld) return;
        Send(new() { Type = 0, Mouse = new() { Flags = 4 } }, "LeftUp", _lastPoint, control, nonce, "Left"); _leftHeld = false;
    }
    public void Click(ScreenPoint point, string control, string nonce)
    { Move(point, control, nonce); LeftDown(control, nonce); LeftUp(control, nonce); }
    public void DoubleClick(ScreenPoint point, string control, string nonce)
    { Click(point, control, nonce); Click(point, control, nonce); }
    public void Wheel(ScreenPoint point, int delta, string control, string nonce)
    {
        if (delta == 0 || Math.Abs((long)delta) > 1200) throw new ArgumentOutOfRangeException(nameof(delta));
        Move(point, control, nonce);
        Send(new() { Type = 0, Mouse = new() { Flags = 0x0800, Data = unchecked((uint)delta) } }, "Wheel", point, control, nonce, wheel: delta);
    }
    public async Task DragAsync(ScreenPoint from, ScreenPoint to, string control, string nonce, CancellationToken token)
    {
        Move(from, control, nonce); LeftDown(control, nonce);
        try
        {
            for (var i = 1; i <= 12; i++)
            {
                token.ThrowIfCancellationRequested();
                Move(new(from.X + (to.X - from.X) * i / 12, from.Y + (to.Y - from.Y) * i / 12), control, nonce);
                await Task.Delay(15, token); // gesture cadence, never an acceptance wait
            }
        }
        finally { LeftUp(control, nonce); }
    }
    public void Key(ushort key, bool up, string control, string nonce)
    {
        // Only keys required by this matrix. No Win/Alt, shell chords or text injection API.
        if (key is not (0x10 or 0x11 or 0x1B or 0x20 or 0x09 or 0x0D or 0x59 or 0x5A)) throw new ArgumentOutOfRangeException(nameof(key));
        if (up && !_heldKeys.Contains(key)) return;
        if (!up && _heldKeys.Contains(key)) throw new InvalidOperationException("Key already held.");
        Send(new() { Type = 1, Key = new() { Vk = key, Flags = up ? 2u : 0u } }, up ? "KeyUp" : "KeyDown", null, control, nonce, keys: key);
        if (up) _heldKeys.Remove(key); else _heldKeys.Add(key);
    }
    public void Chord(ushort modifier, ushort key, string control, string nonce)
    {
        if (modifier is not (0x10 or 0x11)) throw new ArgumentOutOfRangeException(nameof(modifier));
        Key(modifier, false, control, nonce);
        try { Key(key, false, control, nonce); Key(key, true, control, nonce); }
        finally { Key(modifier, true, control, nonce); }
    }
    private void Send(Win32.INPUT input, string action, ScreenPoint? point, string control, string nonce, string? button = null, int wheel = 0, ushort? keys = null)
    {
        if (!Guid.TryParse(nonce, out _)) throw new InvalidDataException("Fresh observer nonce required.");
        lock (_gate)
        {
            using var dpi = new PhysicalDpiScope();
            var identity = host.Validate(point); // repeat for every individual OS input, not just initial click
            using (var response = new FileStream(Path.Combine(host.RuntimeRoot, "ColorStudioFixture", "native-observe-response.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var document = JsonDocument.Parse(response))
                NativeEvidenceReader.ValidateFreshNonce(document.RootElement, nonce, DateTimeOffset.UtcNow);
            // Button/wheel SendInput acts at the OS cursor, not the previously requested coordinate.
            if (input.Type == 0 && action != "MouseMove" &&
                (!Win32.GetCursorPos(out var cursor) || point is not { } expected || cursor.X != expected.X || cursor.Y != expected.Y))
                throw new InvalidOperationException("OS cursor moved since guarded pointer positioning; refuse input.");
            var sequence = ++_sequence;
            void Log(string outcome, uint sent = 0) => File.AppendAllText(_log, JsonSerializer.Serialize(new
            {
                Sequence = sequence, Timestamp = DateTimeOffset.UtcNow, identity.TargetPid, identity.TargetHwnd,
                identity.ExpectedProcessPath, identity.ActualProcessPath, Action = action,
                ScreenX = point?.X, ScreenY = point?.Y, Button = button, WheelDelta = wheel, Keys = keys,
                DpiScale = identity.Dpi / 96d, TargetControl = control, ObserverNonce = nonce, Outcome = outcome, Sent = sent
            }) + Environment.NewLine);
            Log("INTENT");
            var sent = Win32.SendInput(1, [input], Marshal.SizeOf<Win32.INPUT>());
            Log(sent == 1 ? "INJECTED" : "REJECTED", sent);
            if (sent != 1) throw new InvalidOperationException($"SendInput rejected; Win32 error {Marshal.GetLastWin32Error()}, possible UIPI. No PASS.");
        }
    }
}
