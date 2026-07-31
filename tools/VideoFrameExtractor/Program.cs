using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: VideoFrameExtractor <video> <output-directory> [interval-seconds]");
    return 2;
}

var videoPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
var intervalSeconds = args.Length > 2 && double.TryParse(args[2], out var parsedInterval)
    ? Math.Max(0.25, parsedInterval)
    : 1d;

Directory.CreateDirectory(outputPath);
var videoFile = await StorageFile.GetFileFromPathAsync(videoPath);
var outputFolder = await StorageFolder.GetFolderFromPathAsync(outputPath);
var clip = await MediaClip.CreateFromFileAsync(videoFile);
var composition = new MediaComposition();
composition.Clips.Add(clip);

var durationSeconds = composition.Duration.TotalSeconds;
Console.WriteLine($"DurationSeconds={durationSeconds:F3}");

for (var second = 0d; second <= durationSeconds; second += intervalSeconds)
{
    using var thumbnail = await composition.GetThumbnailAsync(
        TimeSpan.FromSeconds(second),
        1280,
        720,
        VideoFramePrecision.NearestFrame);
    var decoder = await BitmapDecoder.CreateAsync(thumbnail);
    using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    var frameName = $"frame_{second:000.00}s.png";
    var outputFile = await outputFolder.CreateFileAsync(frameName, CreationCollisionOption.ReplaceExisting);
    using IRandomAccessStream outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
    encoder.SetSoftwareBitmap(bitmap);
    await encoder.FlushAsync();
    Console.WriteLine(frameName);
}

return 0;
