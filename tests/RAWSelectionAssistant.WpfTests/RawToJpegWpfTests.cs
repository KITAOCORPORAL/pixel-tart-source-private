using System.IO;
using System.Threading;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services.RawToJpeg;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class RawToJpegWpfTests
{
    [TestMethod]
    public Task Encoder_WritesReadableJpegAtRequestedLongestEdge() => RunSta(async () =>
    {
        const int width = 640;
        const int height = 320;
        var image = new RawDecodedImage(width, height, width * 3,
            new byte[width * height * 3], new("Test", "Test", null, 1, "sRGB"));
        using var output = new MemoryStream();
        await new WpfJpegEncoder().EncodeAsync(image, output, new RawToJpegOptions(90, 320));
        output.Position = 0;
        var frame = BitmapFrame.Create(output, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.AreEqual(320, frame.PixelWidth);
        Assert.AreEqual(160, frame.PixelHeight);
    });

    [TestMethod]
    public Task Encoder_WritesReadableJpegAtRealRawDimensionsWithoutFullSizeThumbnail() => RunSta(async () =>
    {
        const int width = 7968;
        const int height = 5320;
        var image = new RawDecodedImage(width, height, width * 3,
            GC.AllocateUninitializedArray<byte>(checked(width * height * 3)),
            new("Sony", "ILCE-7RM3", new DateTimeOffset(2026, 8, 11, 9, 30, 0, TimeSpan.Zero), 1, "sRGB"));
        using var output = new MemoryStream();
        await new WpfJpegEncoder().EncodeAsync(image, output, new RawToJpegOptions());
        output.Position = 0;
        var frame = BitmapFrame.Create(output, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.AreEqual(width, frame.PixelWidth);
        Assert.AreEqual(height, frame.PixelHeight);
        Assert.IsNull(frame.Thumbnail);
    });

    [TestMethod]
    public Task Encoder_AppliesNinetyDegreeOrientation() => RunSta(async () =>
    {
        var image = new RawDecodedImage(2, 1, 6, [255, 0, 0, 0, 255, 0], new(null, null, null, 6, "sRGB"));
        using var output = new MemoryStream();
        await new WpfJpegEncoder().EncodeAsync(image, output, new RawToJpegOptions());
        output.Position = 0;
        var frame = BitmapFrame.Create(output, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.AreEqual(1, frame.PixelWidth);
        Assert.AreEqual(2, frame.PixelHeight);
    });

    [TestMethod]
    public Task Encoder_PreservesExifAndNormalizesOrientationAfterRotation() => RunSta(async () =>
    {
        var image = new RawDecodedImage(2, 1, 6, [255, 0, 0, 0, 255, 0],
            new("Camera Make", "Camera Model", new DateTimeOffset(2026, 8, 11, 9, 30, 0, TimeSpan.Zero), 6, "sRGB"));
        using var output = new MemoryStream();
        await new WpfJpegEncoder().EncodeAsync(image, output, new RawToJpegOptions(PreserveExif: true, AutoRotate: true));
        output.Position = 0;
        var directories = ImageMetadataReader.ReadMetadata(output);
        var ifd0 = directories.OfType<ExifIfd0Directory>().Single();
        Assert.AreEqual("Camera Make", ifd0.GetString(ExifDirectoryBase.TagMake));
        Assert.AreEqual("Camera Model", ifd0.GetString(ExifDirectoryBase.TagModel));
        Assert.AreEqual(1, ifd0.GetInt32(ExifDirectoryBase.TagOrientation));
    });

    [TestMethod]
    public Task Encoder_WhenAutoRotateIsDisabled_KeepsPixelsAndOrientationTag() => RunSta(async () =>
    {
        var image = new RawDecodedImage(2, 1, 6, [255, 0, 0, 0, 255, 0], new(null, null, null, 6, "sRGB"));
        using var output = new MemoryStream();
        await new WpfJpegEncoder().EncodeAsync(image, output, new RawToJpegOptions(PreserveExif: true, AutoRotate: false));
        output.Position = 0;
        var frame = BitmapFrame.Create(output, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.AreEqual(2, frame.PixelWidth);
        Assert.AreEqual(1, frame.PixelHeight);
        output.Position = 0;
        var ifd0 = ImageMetadataReader.ReadMetadata(output).OfType<ExifIfd0Directory>().Single();
        Assert.AreEqual(6, ifd0.GetInt32(ExifDirectoryBase.TagOrientation));
    });

    [TestMethod]
    public void Modal_DeclaresDropAndSourceSafetyContract()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/RawToJpegModal.xaml");
        StringAssert.Contains(xaml, "AllowDrop=\"True\"");
        StringAssert.Contains(xaml, "Drop=\"OnDrop\"");
        StringAssert.Contains(xaml, "源文件不会被移动、删除或覆盖");
        StringAssert.Contains(xaml, "使用相机白平衡");
        StringAssert.Contains(xaml, "保留 EXIF");
        StringAssert.Contains(xaml, "自动旋转");
        StringAssert.Contains(xaml, "sRGB（固定输出）");
        StringAssert.Contains(xaml, "Width=\"320\"");
        StringAssert.Contains(xaml, "Av2PrimaryButton");
    }

    private static string Text(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln")))
                return File.ReadAllText(Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar)));
        throw new DirectoryNotFoundException();
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(async () =>
        {
            try { await action(); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
