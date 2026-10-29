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
                result = TiffExport.WriteRgb48(stream, pixels, options, token); await stream.FlushAsync(token).ConfigureAwait(false); stream.Flush(true); stream.Position = 0; _ = TiffStreamValidator.Validate(stream,token);
            }
            return await CommitAsync(result, temporary, fullPath, token, overwrite).ConfigureAwait(false);
        }
        catch { TryDelete(temporary); throw; }
    }

    public static async Task<TiffExportResult> WriteRowsAsync(string destinationPath,int width,int height,Action<int,Memory<byte>> fillRow,TiffExportOptions? options=null,CancellationToken token=default,bool overwrite=true,Action<string>? stage=null)
    {
        var full=Path.GetFullPath(destinationPath);Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary=Path.Combine(Path.GetDirectoryName(full)!,"."+Path.GetFileName(full)+"."+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            using var expected=System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            TiffExportResult result;await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,65536,FileOptions.SequentialScan))
            {result=TiffExport.WriteRgb48Rows(stream,width,height,(y,row)=>{fillRow(y,row);expected.AppendData(row.Span);},options,token);await stream.FlushAsync(token);stream.Flush(true);stage?.Invoke("Encoded");stream.Position=0;var verifyClock=System.Diagnostics.Stopwatch.StartNew();var verified=TiffStreamValidator.Validate(stream,token);stage?.Invoke("ValidationMs="+verifyClock.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));if(verified.PixelSha256!=Convert.ToHexString(expected.GetHashAndReset()))throw new InvalidDataException("TIFF streamed pixel hash mismatch.");stage?.Invoke("Validated");}
            return await CommitAsync(result,temporary,full,token,overwrite);
        }
        catch{TryDelete(temporary);throw;}
    }

    private static Task<TiffExportResult> CommitAsync(TiffExportResult result, string temporary, string destination, CancellationToken token, bool overwrite)
    {
        token.ThrowIfCancellationRequested(); File.Move(temporary, destination, overwrite); return Task.FromResult(result);
    }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
