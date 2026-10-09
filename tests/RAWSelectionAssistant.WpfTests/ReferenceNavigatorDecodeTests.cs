using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ReferenceNavigatorDecodeTests
{
    [TestMethod]
    public Task ValidMissingCorruptAndReaddedReferencesShowTruthfulState() => RunSta(async () =>
    {
        EnsureTestApplication();
        var root = Path.Combine(Path.GetTempPath(), "Studio-reference-decode-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var source = BitmapSource.Create(12, 8, 96, 96, PixelFormats.Rgb24, null, new byte[12 * 8 * 3], 36);
            source.Freeze(); var path = Path.Combine(root, "reference.png");
            StudioQuickExport.Encode(source, path, path);
            var navigator = new ReferenceNavigator { SourcePath = path };
            await navigator.LoadingTask;
            Assert.IsNotNull(navigator.Image); Assert.AreEqual(12, navigator.Image.PixelWidth);
            navigator.SourcePath = Path.Combine(root, "missing.png"); await navigator.LoadingTask;
            Assert.IsNull(navigator.Image);
            var corrupt = Path.Combine(root, "corrupt.png"); File.WriteAllText(corrupt, "not a bitmap");
            navigator.SourcePath = corrupt; await navigator.LoadingTask;
            Assert.IsNull(navigator.Image);
            navigator.SourcePath = path; await navigator.LoadingTask;
            Assert.IsNotNull(navigator.Image);
            // Shared loading applies EXIF orientation / ICC and detaches the
            // decoder before cross-thread preview or inspection conversion.
            var loaded = navigator.Image;
            await Task.Run(() => { var converted = new FormatConvertedBitmap(loaded, PixelFormats.Bgra32, null, 0); converted.Freeze(); });
            navigator.SourcePath = string.Empty; await navigator.LoadingTask;
            Assert.IsNull(navigator.Image);
            var large = BitmapSource.Create(1000, 1600, 96, 96, PixelFormats.Rgb24, null, new byte[1000 * 1600 * 3], 3000);
            large.Freeze(); var portrait = Path.Combine(root, "portrait.png");
            StudioQuickExport.Encode(large, portrait, portrait);
            navigator.IsThumbnail = true; navigator.SourcePath = portrait; await navigator.LoadingTask;
            Assert.IsNotNull(navigator.Image);
            Assert.IsLessThanOrEqualTo(384, navigator.Image.PixelHeight);
            Assert.AreEqual(1000d / 1600, navigator.Image.PixelWidth / (double)navigator.Image.PixelHeight, .005);
        }
        finally { Directory.Delete(root, true); }
    });
}
