using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

public enum RawLoadLevel
{
    Thumbnail,
    EmbeddedPreview,
    FastPreview,
    ProfessionalDecode
}

public static class RawLoadPolicy
{
    public static RawDecodeMode DecodeModeFor(RawLoadLevel level) =>
        level == RawLoadLevel.ProfessionalDecode ? RawDecodeMode.ProfessionalDecode : RawDecodeMode.FastPreview;
}

public sealed record RawDecodeCacheKey(string FullPath, long Length, DateTime LastWriteTimeUtc,
    RawDecodeMode Mode, bool AutoRotate, string DecoderVersion);

/// <summary>Bounded in-process cache. Display callers must not use a preview as an export source.</summary>
public sealed class RawDecodeCache
{
    private readonly object _gate = new();
    private readonly Dictionary<RawDecodeCacheKey, (RawDecodedImage Image, long Size)> _items = [];
    private readonly LinkedList<RawDecodeCacheKey> _lru = [];
    private readonly long _maximumBytes;
    private long _currentBytes;

    public RawDecodeCache(long maximumBytes = 512L * 1024 * 1024)
    {
        if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _maximumBytes = maximumBytes;
    }

    public int Count { get { lock (_gate) return _items.Count; } }
    public long CurrentBytes { get { lock (_gate) return _currentBytes; } }

    public bool TryGet(RawDecodeCacheKey key, out RawDecodedImage image)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(key, out var entry)) { image = null!; return false; }
            _lru.Remove(key); _lru.AddFirst(key); image = entry.Image; return true;
        }
    }

    public void Put(RawDecodeCacheKey key, RawDecodedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var size = image.Rgb48Pixels is { } high ? high.LongLength * sizeof(ushort) : image.Rgb24Pixels.LongLength;
        lock (_gate)
        {
            if (_items.Remove(key, out var old)) { _currentBytes -= old.Size; _lru.Remove(key); }
            if (size > _maximumBytes) return;
            _items[key] = (image, size); _lru.AddFirst(key); _currentBytes += size;
            while (_currentBytes > _maximumBytes && _lru.Last is { } tail)
            {
                _lru.RemoveLast(); if (_items.Remove(tail.Value, out var removed)) _currentBytes -= removed.Size;
            }
        }
    }
}
