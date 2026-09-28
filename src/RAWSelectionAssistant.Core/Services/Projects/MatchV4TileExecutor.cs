using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>
/// Deterministic per-pixel tile executor for the shared V4 semantics. It is deliberately backend
/// neutral: a DX12/Metal implementation can replace the mapping callback without changing tile
/// coordinates, generation checks or cancellation behavior.
/// </summary>
public sealed class MatchV4TileExecutor
{
    public async Task<HighBitDepthImageBuffer> ExecuteAsync(
        HighBitDepthImageBuffer source,
        ReferenceMatchV4Settings settings,
        Func<ReadOnlyMemory<float>, CancellationToken, Task<ReadOnlyMemory<float>>> transform,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transform);
        settings.Validate();
        var output = new float[source.Rgb32.Length];
        foreach (var tile in ColorMatchTilePlanner.Plan(source.Width, source.Height, settings))
        {
            token.ThrowIfCancellationRequested();
            var tileValues = new float[checked(tile.Width * tile.Height * 3)];
            for (var y = 0; y < tile.Height; y++)
            {
                var sourceOffset = ((tile.Y + y) * source.Width + tile.X) * 3;
                source.Rgb32.Span.Slice(sourceOffset, tile.Width * 3).CopyTo(tileValues.AsSpan(y * tile.Width * 3));
            }
            var transformed = await transform(tileValues, token).ConfigureAwait(false);
            if (transformed.Length != tileValues.Length) throw new InvalidDataException("V4 tile backend returned an unexpected pixel count.");
            for (var y = 0; y < tile.Height; y++)
            {
                var destinationOffset = ((tile.Y + y) * source.Width + tile.X) * 3;
                transformed.Span.Slice(y * tile.Width * 3, tile.Width * 3).CopyTo(output.AsSpan(destinationOffset));
            }
        }
        return new HighBitDepthImageBuffer(source.Width, source.Height, output, source.SourceBitDepth, source.WorkingColorSpace, source.Orientation, source.Metadata);
    }
}
