using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>
/// Immutable high-precision RAW decode owned by one editing session. Preview, Match and export
/// must consume this object rather than silently decoding the source again.
/// </summary>
public sealed class FrozenRawMaster
{
    public FrozenRawMaster(string sourcePath, string sourceSha256, Guid decodeGenerationId, HighBitDepthImageBuffer image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSha256);
        ArgumentNullException.ThrowIfNull(image);
        SourcePath = Path.GetFullPath(sourcePath);
        SourceSha256 = sourceSha256;
        DecodeGenerationId = decodeGenerationId;
        ProcessingGenerationId = Guid.NewGuid();
        Image = image;
    }

    public string SourcePath { get; }
    public string SourceSha256 { get; }
    public Guid DecodeGenerationId { get; }
    /// <summary>Processing-session identity shared by preview, Match and export for this master.</summary>
    public Guid ProcessingGenerationId { get; }
    public HighBitDepthImageBuffer Image { get; }
    public int Width => Image.Width;
    public int Height => Image.Height;
    public ushort Orientation => Image.Orientation;

    public async Task ValidateSourceAsync(CancellationToken token = default)
    {
        await using var stream = new FileStream(SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
        if (!string.Equals(hash, SourceSha256, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The RAW source changed after its frozen master was created.");
    }
}
