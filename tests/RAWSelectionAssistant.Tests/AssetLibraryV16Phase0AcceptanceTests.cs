using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryV16Phase0AcceptanceTests
{
    [TestMethod]
    public async Task GeneratedFixtureManifestUsesOnlyExplicitTempRootAndMetadataFreeJpegs()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The foreground WPF acceptance fixture generator is Windows-only.");

        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "PixelTart_AssetLibrary_V16_Acceptance",
            "Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var result = await RunGeneratorAsync(outputRoot, performanceCount: 4);
            Assert.AreEqual(0, result.ExitCode, result.StandardError);

            var manifestPath = Path.Combine(outputRoot, "fixtures.manifest.json");
            Assert.IsTrue(File.Exists(manifestPath));
            var manifestBytes = await File.ReadAllBytesAsync(manifestPath);
            Assert.IsFalse(manifestBytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "Manifest must be UTF-8 without BOM.");

            var manifestText = new UTF8Encoding(false, true).GetString(manifestBytes);
            Assert.DoesNotContain(outputRoot, manifestText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.UserName, manifestText, StringComparison.OrdinalIgnoreCase);

            using var manifest = JsonDocument.Parse(manifestText);
            var root = manifest.RootElement;
            Assert.AreEqual("pixel-tart-asset-library-v16-fixtures/v1", root.GetProperty("schema").GetString());
            Assert.IsFalse(root.GetProperty("contains_customer_media").GetBoolean());
            Assert.IsFalse(root.GetProperty("contains_source_paths").GetBoolean());
            Assert.IsFalse(root.GetProperty("icc_reference_included").GetBoolean());
            Assert.IsFalse(root.GetProperty("raw_embedded_preview_included").GetBoolean());

            var fixtures = root.GetProperty("fixtures").EnumerateArray().ToArray();
            Assert.HasCount(7, fixtures);
            CollectionAssert.AreEquivalent(
                new[] { "phase0_palette", "phase0_histogram", "phase0_tone", "performance" },
                fixtures.Select(item => item.GetProperty("category").GetString()).Distinct().ToArray());

            foreach (var fixture in fixtures)
            {
                var relativePath = fixture.GetProperty("relative_path").GetString()!;
                Assert.IsFalse(Path.IsPathRooted(relativePath));
                Assert.DoesNotContain("..", relativePath, StringComparison.Ordinal);

                var fullPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                Assert.IsTrue(fullPath.StartsWith(Path.GetFullPath(outputRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(File.Exists(fullPath));

                var bytes = await File.ReadAllBytesAsync(fullPath);
                Assert.AreEqual(fixture.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
                var jpeg = InspectJpeg(bytes);
                Assert.AreEqual(fixture.GetProperty("width").GetInt32(), jpeg.Width);
                Assert.AreEqual(fixture.GetProperty("height").GetInt32(), jpeg.Height);
                Assert.IsFalse(jpeg.HasExifOrXmpApp1, $"Sensitive APP1 metadata found in {relativePath}.");

                var ascii = Encoding.ASCII.GetString(bytes);
                Assert.DoesNotContain(Environment.UserName, ascii, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("GPS", ascii, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task GeneratorRejectsRepositoryOutputBeforeCreatingFiles()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The foreground WPF acceptance fixture generator is Windows-only.");

        var forbidden = Path.Combine(Root(), "tools", "AssetLibraryV16Acceptance", "forbidden-output-" + Guid.NewGuid().ToString("N"));
        var result = await RunGeneratorAsync(forbidden, performanceCount: 1);
        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardError, "system temporary directory");
        Assert.IsFalse(Directory.Exists(forbidden));
    }

    private static async Task<ProcessResult> RunGeneratorAsync(string outputRoot, int performanceCount)
    {
        var script = Path.Combine(Root(), "tools", "AssetLibraryV16Acceptance", "New-AssetLibraryV16Fixtures.ps1");
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-OutputRoot");
        start.ArgumentList.Add(outputRoot);
        start.ArgumentList.Add("-PerformanceCount");
        start.ArgumentList.Add(performanceCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("-PerformanceWidth");
        start.ArgumentList.Add("192");
        start.ArgumentList.Add("-PerformanceHeight");
        start.ArgumentList.Add("128");

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start fixture generator.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await standardOutput, await standardError);
    }

    private static JpegInfo InspectJpeg(ReadOnlySpan<byte> bytes)
    {
        Assert.IsGreaterThanOrEqualTo(4, bytes.Length);
        Assert.AreEqual((byte)0xFF, bytes[0]);
        Assert.AreEqual((byte)0xD8, bytes[1]);

        var width = 0;
        var height = 0;
        var hasExifOrXmp = false;
        var offset = 2;
        while (offset + 3 < bytes.Length)
        {
            while (offset < bytes.Length && bytes[offset] != 0xFF) offset++;
            while (offset < bytes.Length && bytes[offset] == 0xFF) offset++;
            if (offset >= bytes.Length) break;

            var marker = bytes[offset++];
            if (marker is 0xD8 or 0xD9 || marker is >= 0xD0 and <= 0xD7) continue;
            if (offset + 1 >= bytes.Length) break;
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            if (length < 2 || offset + length > bytes.Length) break;
            var payload = bytes.Slice(offset + 2, length - 2);

            if (marker == 0xE1)
            {
                var app1 = Encoding.ASCII.GetString(payload);
                hasExifOrXmp |= app1.Contains("Exif", StringComparison.OrdinalIgnoreCase) ||
                                app1.Contains("xap/1.0", StringComparison.OrdinalIgnoreCase) ||
                                app1.Contains("GPS", StringComparison.OrdinalIgnoreCase);
            }

            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                Assert.IsGreaterThanOrEqualTo(5, payload.Length);
                height = (payload[1] << 8) | payload[2];
                width = (payload[3] << 8) | payload[4];
            }

            if (marker == 0xDA) break;
            offset += length;
        }

        Assert.IsGreaterThan(0, width);
        Assert.IsGreaterThan(0, height);
        return new(width, height, hasExifOrXmp);
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
    private sealed record JpegInfo(int Width, int Height, bool HasExifOrXmpApp1);
}
