using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryExifMetadataTests
{
    [TestMethod]
    public void TiffAndDngContainerDimensionsUseStandardExifFallbackWithoutFabrication()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-exif", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tiff = Path.Combine(root, "metadata.tiff");
        var dng = Path.Combine(root, "metadata.dng");
        try
        {
            var pixels = Enumerable.Repeat((byte)127, 4 * 3 * 4).ToArray();
            var bitmap = BitmapSource.Create(4, 3, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
            var encoder = new TiffBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(tiff)) encoder.Save(stream);
            File.Copy(tiff, dng);

            var service = new JpegMetadataService();
            foreach (var path in new[] { tiff, dng })
            {
                var metadata = service.Read(path);
                Assert.AreEqual(4, metadata.PixelWidth, path);
                Assert.AreEqual(3, metadata.PixelHeight, path);
                Assert.AreEqual(string.Empty, metadata.MetadataReadError, path);
                Assert.AreEqual(string.Empty, metadata.CameraMake, "A missing EXIF camera tag must remain empty.");
                Assert.AreEqual(string.Empty, metadata.Lens, "A missing EXIF lens tag must remain empty.");
            }
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [TestMethod]
    public void UnsupportedRawBytesReturnFriendlyErrorInsteadOfThrowingOrInventingMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-exif", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var raw = Path.Combine(root, "corrupt.nef");
        try
        {
            File.WriteAllBytes(raw, [1, 2, 3, 4]);
            var metadata = new JpegMetadataService().Read(raw);
            Assert.IsFalse(string.IsNullOrWhiteSpace(metadata.MetadataReadError));
            Assert.IsNull(metadata.PixelWidth);
            Assert.AreEqual(string.Empty, metadata.CameraModel);
            Assert.AreEqual(string.Empty, metadata.Iso);
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }
}
