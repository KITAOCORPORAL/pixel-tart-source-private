using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Export;

public static class AtomicTiffWriter
{
    public static async Task<TiffExportResult> WriteRgb48Async(string destinationPath, HighBitDepthImageBuffer pixels, TiffExportOptions? options = null, CancellationToken token = default, bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath); ArgumentNullException.ThrowIfNull(pixels);
        var fullPath = Path.GetFullPath(destinationPath); var directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Destination directory is required.", nameof(destinationPath));
        Directory.CreateDirectory(directory); var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            TiffExportResult result;
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.SequentialScan))
            {
                result = TiffExport.WriteRgb48(stream, pixels, options, token); await stream.FlushAsync(token).ConfigureAwait(false); stream.Flush(true); stream.Position = 0; _ = TiffReadBack.Read(stream);
            }
            return await CommitAsync(result, temporary, fullPath, token, overwrite).ConfigureAwait(false);
        }
        catch { TryDelete(temporary); throw; }
    }

    private static Task<TiffExportResult> CommitAsync(TiffExportResult result, string temporary, string destination, CancellationToken token, bool overwrite)
    {
        token.ThrowIfCancellationRequested(); File.Move(temporary, destination, overwrite); return Task.FromResult(result);
    }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
