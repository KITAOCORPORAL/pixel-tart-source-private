using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelTart.NativeAcceptance;

// PURE CONTRACTS. These tests never start a product, call UIA, SendInput, PrintWindow or BitBlt.
[TestClass]
public sealed class NativeHarnessContractTests
{
    private static TargetIdentity Identity => new(12, 34, @"C:\test\KitaoPhotoSelector.exe", @"C:\test\KitaoPhotoSelector.exe", 12, 56, 56, true, true, 144, new(100, 100, 1600, 920));
    [TestMethod] public void MatchingOwnedIdentityPasses() => TargetGuard.Validate(Identity, new(200, 200));
    [TestMethod] public void ForeignPidIsRejected() => Assert.ThrowsExactly<InvalidOperationException>(() => TargetGuard.Validate(Identity with { WindowPid = 13 }));
    [TestMethod] public void WrongPathIsRejected() => Assert.ThrowsExactly<InvalidOperationException>(() => TargetGuard.Validate(Identity with { ActualProcessPath = @"C:\test\other.exe" }));
    [TestMethod] public void ReusedPidIsRejected() => Assert.ThrowsExactly<InvalidOperationException>(() => TargetGuard.Validate(Identity with { ProcessStartTicks = 57 }));
    [TestMethod] public void ForegroundLossIsRejected() => Assert.ThrowsExactly<InvalidOperationException>(() => TargetGuard.Validate(Identity with { Foreground = false }));
    [TestMethod] public void OutsideWindowIsRejected() => Assert.ThrowsExactly<InvalidOperationException>(() => TargetGuard.Validate(Identity, new(0, 0)));
    [TestMethod] public void InvalidHwndIsRejected() => Assert.ThrowsExactly<InvalidOperationException>(() => TargetGuard.Validate(Identity with { TargetHwnd = 0 }));
    [TestMethod] public void NoGenericExecutableAllowed() => Assert.ThrowsExactly<InvalidOperationException>(() => TargetGuard.Validate(Identity with { ExpectedProcessPath = @"C:\test\other.exe", ActualProcessPath = @"C:\test\other.exe" }));
    [TestMethod] public void DpiMatrixUsesPhysicalCoordinates()
    {
        foreach (var scale in new[] { 1d, 1.25, 1.5, 2 })
            NativeCoordinateMapper.ValidateDpi(new(-100, 20, 800 * scale, 600 * scale), 800, 600, scale, scale, (uint)(96 * scale));
    }
    [TestMethod] public void DpiMismatchIsRejected() => Assert.ThrowsExactly<InvalidDataException>(() => NativeCoordinateMapper.ValidateDpi(new(0, 0, 800, 600), 800, 600, 1, 1, 144));
    [TestMethod] public void VirtualScreenSupportsNegativeOrigin()
    {
        var desk = new ScreenBounds(-1920, 0, 3840, 1080);
        Assert.AreEqual((0, 0), NativeCoordinateMapper.Absolute(new(-1920, 0), desk));
        Assert.AreEqual((65535, 65535), NativeCoordinateMapper.Absolute(new(1919, 1079), desk));
    }
    [TestMethod] public void Win32InputLayoutIsX64() => Assert.AreEqual(40, Marshal.SizeOf<Win32.INPUT>());
    [TestMethod] public void StaleOrUnmatchedObserverNonceIsRejected()
    {
        var nonce = Guid.NewGuid().ToString(); var now = DateTimeOffset.UtcNow;
        using var fresh = JsonDocument.Parse(JsonSerializer.Serialize(new { Nonce = nonce, Timestamp = now }));
        NativeEvidenceReader.ValidateFreshNonce(fresh.RootElement, nonce, now);
        Assert.ThrowsExactly<InvalidOperationException>(() => NativeEvidenceReader.ValidateFreshNonce(fresh.RootElement, Guid.NewGuid().ToString(), now));
        Assert.ThrowsExactly<InvalidOperationException>(() => NativeEvidenceReader.ValidateFreshNonce(fresh.RootElement, nonce, now.AddSeconds(6)));
    }
    [TestMethod] public async Task NativeWaitRequiresPredicate()
    {
        var count = 0;
        var result = await NativeWait.UntilAsync(_ => Task.FromResult(++count), n => n == 3, TimeSpan.FromSeconds(1));
        Assert.AreEqual(3, result);
    }
    [TestMethod] public async Task NativeWaitTimeoutNeverBecomesPass() => await Assert.ThrowsExactlyAsync<TimeoutException>(() => NativeWait.UntilAsync(_ => Task.FromResult(false), s => s, TimeSpan.FromMilliseconds(40)));
    [TestMethod] public void ThreeWayNodeIdsMustAllMatch()
    {
        using var doc = JsonDocument.Parse("""{"ModelRenderSynchronized":true,"Drag":{"Active":false,"Rows":[{"Id":"a"},{"Id":"b"}]},"ModelIds":["a","b"],"RenderedNodeIds":["b","a"]}""");
        Assert.IsFalse(NativeEvidenceReader.NodesSynchronized(doc.RootElement));
    }
    [TestMethod] public void ScreenshotValidatorRejectsBlackWhiteTransparentAndSolid()
    {
        foreach (var (shade, alpha) in new[] { ((byte)0, (byte)255), ((byte)255, (byte)255), ((byte)128, (byte)0), ((byte)128, (byte)255) })
            ImageTest((x, y) => (shade, shade, shade, alpha), path => Assert.IsFalse(NativeScreenshotValidator.Validate(path, 64, 64).Valid));
    }
    [TestMethod] public void ScreenshotValidatorChecksSizeVarianceAndHash() => ImageTest((x, y) => ((byte)(x * 4), (byte)(y * 4), (byte)100, (byte)255), path =>
    {
        var result = NativeScreenshotValidator.Validate(path, 64, 64);
        Assert.IsTrue(result.Valid); Assert.AreEqual(16, result.PerceptualHash.Length); Assert.IsGreaterThan(1d, result.PixelVariance);
        Assert.IsFalse(NativeScreenshotValidator.Validate(path, 65, 64).Valid);
    });
    [TestMethod] public void ProductionPackageDoesNotContainNativeAcceptanceHarness()
    {
        var root = Root();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
            Assert.IsFalse(File.ReadAllText(file).Contains("PixelTart.NativeAcceptance", StringComparison.OrdinalIgnoreCase), file);
        var output = Path.Combine(root, "src/RAWSelectionAssistant/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64");
        Assert.IsTrue(Directory.Exists(output), "Production output must exist for package isolation check.");
        Assert.IsFalse(Directory.EnumerateFiles(output, "*NativeAcceptance*", SearchOption.AllDirectories).Any());
        var deps = File.ReadAllText(Path.Combine(output, "KitaoPhotoSelector.deps.json"));
        Assert.IsFalse(deps.Contains("PixelTart.NativeAcceptance", StringComparison.OrdinalIgnoreCase));
    }
    private static void ImageTest(Func<int, int, (byte R, byte G, byte B, byte A)> pixel, Action<string> check)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            var bytes = new byte[64 * 64 * 4];
            for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++) { var c = pixel(x, y); var i = (y * 64 + x) * 4; bytes[i] = c.B; bytes[i + 1] = c.G; bytes[i + 2] = c.R; bytes[i + 3] = c.A; }
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, bytes, 64 * 4)));
            using (var output = File.Create(path)) encoder.Save(output);
            check(path);
        }
        finally { File.Delete(path); }
    }
    private static string Root()
    {
        for (var root = new DirectoryInfo(AppContext.BaseDirectory); root is not null; root = root.Parent)
            if (File.Exists(Path.Combine(root.FullName, "RAWSelectionAssistant.sln"))) return root.FullName;
        throw new DirectoryNotFoundException();
    }
}
