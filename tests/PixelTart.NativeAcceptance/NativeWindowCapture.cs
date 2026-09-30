using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelTart.NativeAcceptance;

internal sealed record ScreenshotValidation(int Width, int Height, long NonZeroBytes, bool NotAllBlack,
    bool NotAllWhite, bool NotTransparent, double PixelVariance, string PerceptualHash, bool Valid);
internal sealed record NativeCaptureResult(string Path, string Method, ScreenshotValidation Validation);

internal static class NativeScreenshotValidator
{
    public static ScreenshotValidation Validate(string path, int expectedWidth, int expectedHeight)
    {
        using var file = File.OpenRead(path);
        var frame = BitmapFrame.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var image = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[checked(image.PixelWidth * image.PixelHeight * 4)]; image.CopyPixels(pixels, image.PixelWidth * 4, 0);
        double sum = 0, sum2 = 0; var min = 255d; var max = 0d; long nonzero = 0, alpha = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var l = (pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3d; sum += l; sum2 += l * l; min = Math.Min(min, l); max = Math.Max(max, l);
            for (var c = 0; c < 4; c++) if (pixels[i + c] != 0) nonzero++;
            alpha += pixels[i + 3];
        }
        var count = image.PixelWidth * image.PixelHeight; var variance = Math.Max(0, sum2 / count - Math.Pow(sum / count, 2));
        ulong hash = 0;
        double L(int x, int y) { var i = (y * image.PixelWidth + x) * 4; return pixels[i] + pixels[i + 1] + pixels[i + 2]; }
        for (var y = 0; y < 8; y++) for (var x = 0; x < 8; x++)
            if (L(x * (image.PixelWidth - 1) / 8, y * (image.PixelHeight - 1) / 7) > L((x + 1) * (image.PixelWidth - 1) / 8, y * (image.PixelHeight - 1) / 7)) hash |= 1UL << (y * 8 + x);
        return new(image.PixelWidth, image.PixelHeight, nonzero, max > 2, min < 253, alpha > 0, variance, hash.ToString("X16"),
            image.PixelWidth == expectedWidth && image.PixelHeight == expectedHeight && nonzero > 0 && max > 2 && min < 253 && alpha > 0 && variance > 1);
    }
}

internal sealed class NativeWindowCapture(PixelTartProcessHost host)
{
    public NativeCaptureResult Capture(string name, nint? ownedWindow = null)
    {
        if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Evidence filename only.");
        using var dpi = new PhysicalDpiScope();
        var target = host.Validate(captureOnly: true);
        var hwnd = ownedWindow ?? host.Hwnd;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != host.Pid || !Win32.IsWindow(hwnd) || !Win32.GetWindowRect(hwnd, out var bounds))
            throw new InvalidOperationException("Capture requires an observed window of the owned process.");
        var w = checked((int)bounds.Bounds.Width); var h = checked((int)bounds.Bounds.Height);
        if (w > 8192 || h > 8192) throw new InvalidOperationException("Bounded screenshot dimensions exceeded.");
        foreach (var method in new[] { "PrintWindow", "BitBlt-WindowDC" })
        {
            host.Validate(captureOnly: true);
            var path = Path.Combine(host.RuntimeRoot, $"{name}-{Guid.NewGuid():N}-{method}.png");
            if (!TryCapture(hwnd, method, path, w, h)) continue;
            var validation = NativeScreenshotValidator.Validate(path, w, h);
            File.AppendAllText(Path.Combine(host.RuntimeRoot, "native-capture.jsonl"), System.Text.Json.JsonSerializer.Serialize(new
                { Timestamp = DateTimeOffset.UtcNow, target.TargetPid, TargetHwnd = hwnd.ToInt64(), Method = method, Path = path, Validation = validation }) + Environment.NewLine);
            if (validation.Valid) return new(path, method, validation);
            // Invalid images remain local failure evidence. Never replace/label as PASS.
        }
        throw new InvalidOperationException("Both native screenshot methods failed validation.");
    }
    private bool TryCapture(nint hwnd, string method, string path, int width, int height)
    {
        var source = Win32.GetWindowDC(hwnd); if (source == 0) return false;
        var dc = Win32.CreateCompatibleDC(source); var bitmap = Win32.CreateCompatibleBitmap(source, width, height); nint old = 0;
        try
        {
            if (dc == 0 || bitmap == 0) return false;
            old = Win32.SelectObject(dc, bitmap);
            host.Validate(captureOnly: true);
            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != host.Pid) throw new InvalidOperationException("Capture window ownership changed.");
            var ok = method == "PrintWindow" ? Win32.PrintWindow(hwnd, dc, 2) : Win32.BitBlt(dc, 0, 0, width, height, source, 0, 0, 0x00CC0020);
            if (!ok) return false;
            Win32.SelectObject(dc, old); old = 0;
            var bytes = new byte[checked(width * height * 4)];
            var info = new Win32.BITMAPINFO { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            if (Win32.GetDIBits(dc, bitmap, 0, (uint)height, bytes, ref info, 0) != height) return false;
            // BI_RGB desktop captures have an unused alpha byte, not meaningful transparency.
            for (var i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bytes, width * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new FileStream(path, FileMode.CreateNew); encoder.Save(stream); return true;
        }
        finally
        {
            if (old != 0) Win32.SelectObject(dc, old);
            if (bitmap != 0) Win32.DeleteObject(bitmap);
            if (dc != 0) Win32.DeleteDC(dc);
            Win32.ReleaseDC(hwnd, source);
        }
    }
}
