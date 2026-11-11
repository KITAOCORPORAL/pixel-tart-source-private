using System.Buffers.Binary;
using System.Text.Json;
namespace PixelTart.BrowserCapture.Contracts;

public sealed record CaptureDestination(Guid LibraryId, Guid? ProjectId = null, Guid? BoardId = null);
public sealed record CaptureSubmission(Guid CaptureId, CaptureDestination Destination, string PageUrl, string ImageUrl,
    string PageTitle, DateTimeOffset CapturedAtUtc, string FileName, string ContentSha256, string ImageBase64,
    string? RenderedImageUrl = null, string SourceSelection = "direct");
public sealed record CaptureRequest(string Operation, CaptureSubmission? Capture = null, string? ExtensionOrigin = null)
{
    public Guid? CaptureId { get; init; }
}
public sealed record CaptureOption(Guid Id, string Name);
public sealed record CaptureLibraryOption(Guid Id, string Name);
public sealed record CaptureDestinations(IReadOnlyList<CaptureLibraryOption> Libraries, IReadOnlyList<CaptureOption> Projects, IReadOnlyList<CaptureOption> Boards);
public sealed record CaptureReceipt(Guid CaptureId, Guid LibraryId, Guid? ProjectId, Guid? BoardId, Guid AssetId,
    string ContentSha256, string Outcome, string PageUrl, DateTimeOffset CapturedAtUtc, DateTimeOffset ImportedAtUtc);
public sealed record CaptureReply(bool Ok, string Code, string Message, CaptureDestinations? Destinations = null, CaptureReceipt? Receipt = null, bool Retryable = false);
public static class CaptureWire
{
    public const int MaximumImageBytes = 16 * 1024 * 1024;
    public const int MaximumRequestBytes = 24 * 1024 * 1024;
    public const int MaximumResponseBytes = 1024 * 1024;
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
    public static async Task<byte[]?> ReadAsync(Stream stream, int maximum, CancellationToken token = default)
    {
        var prefix = new byte[4]; var first = await stream.ReadAsync(prefix.AsMemory(0, 1), token);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(prefix.AsMemory(1, 3), token);
        var count = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (count <= 0 || count > maximum) throw new InvalidDataException("Capture message exceeds its byte limit.");
        var bytes = new byte[count]; await stream.ReadExactlyAsync(bytes, token); return bytes;
    }
    public static async Task WriteAsync<T>(Stream stream, T value, int maximum, CancellationToken token = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > maximum) throw new InvalidDataException("Capture response exceeds its byte limit.");
        var prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, bytes.Length);
        await stream.WriteAsync(prefix, token); await stream.WriteAsync(bytes, token); await stream.FlushAsync(token);
    }
}
