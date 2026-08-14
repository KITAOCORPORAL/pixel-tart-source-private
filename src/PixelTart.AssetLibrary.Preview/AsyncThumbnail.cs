using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PixelTart.AssetLibrary.Preview;

public static class AsyncThumbnail
{
    private const long MaxCacheBytes = 64L * 1024 * 1024;
    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.Ordinal);
    private static readonly LinkedList<string> Lru = new();
    private static readonly ConcurrentDictionary<Image, CancellationTokenSource> Requests = new();
    private static long _cacheBytes;
    private static int _failureCount;

    public static int PendingRequestCount => Requests.Count;
    public static int FailureCount => Volatile.Read(ref _failureCount);

    public static readonly DependencyProperty SourcePathProperty = DependencyProperty.RegisterAttached("SourcePath", typeof(string), typeof(AsyncThumbnail), new PropertyMetadata(null, OnSourceChanged));
    public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.RegisterAttached("DecodeWidth", typeof(double), typeof(AsyncThumbnail), new PropertyMetadata(180d, OnSourceChanged));
    public static void SetSourcePath(DependencyObject target, string? value) => target.SetValue(SourcePathProperty, value); public static string? GetSourcePath(DependencyObject target) => (string?)target.GetValue(SourcePathProperty);
    public static void SetDecodeWidth(DependencyObject target, double value) => target.SetValue(DecodeWidthProperty, value); public static int GetDecodeWidth(DependencyObject target) => (int)Math.Round((double)target.GetValue(DecodeWidthProperty));

    private static async void OnSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Image image) return;
        image.Unloaded -= OnImageUnloaded; image.Unloaded += OnImageUnloaded;
        if (Requests.TryRemove(image, out var previous)) { previous.Cancel(); previous.Dispose(); }
        image.Source = null; var path = GetSourcePath(image); if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { if (!string.IsNullOrWhiteSpace(path)) Interlocked.Increment(ref _failureCount); return; }
        var cancellation = new CancellationTokenSource(); Requests[image] = cancellation; var requestedPath = Path.GetFullPath(path); var width = Math.Clamp(GetDecodeWidth(image), 96, 512);
        try
        {
            var key = await Task.Run(() => Fingerprint(requestedPath, width, cancellation.Token), cancellation.Token);
            if (!Cache.TryGetValue(key, out var cached))
            {
                var bitmap = await Task.Run(() => Decode(requestedPath, width, cancellation.Token), cancellation.Token);
                cached = AddCache(key, bitmap);
            }
            if (!cancellation.IsCancellationRequested && string.Equals(GetSourcePath(image), path, StringComparison.OrdinalIgnoreCase)) image.Source = cached.Bitmap;
        }
        catch (OperationCanceledException) { }
        catch (IOException) { Interlocked.Increment(ref _failureCount); }
        catch (NotSupportedException) { Interlocked.Increment(ref _failureCount); }
        catch (ArgumentException) { Interlocked.Increment(ref _failureCount); }
        finally { if (Requests.TryGetValue(image, out var current) && ReferenceEquals(current, cancellation)) Requests.TryRemove(image, out _); cancellation.Dispose(); }
    }

    private static void OnImageUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && Requests.TryRemove(image, out var cancellation)) { cancellation.Cancel(); cancellation.Dispose(); }
    }

    private static string Fingerprint(string path, int width, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); var info = new FileInfo(path); var text = $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{width}|thumb-v1"; return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }
    private static BitmapImage Decode(string path, int width, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = width; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); cancellationToken.ThrowIfCancellationRequested(); return image;
    }
    private static CacheEntry AddCache(string key, BitmapImage bitmap)
    {
        var bytes = Math.Max(1L, bitmap.PixelWidth * (long)bitmap.PixelHeight * 4); lock (Gate)
        {
            if (Cache.TryGetValue(key, out var existing)) { Lru.Remove(key); Lru.AddLast(key); return existing; }
            while (_cacheBytes + bytes > MaxCacheBytes && Lru.First is { } oldest) { Lru.RemoveFirst(); if (Cache.TryRemove(oldest.Value, out var removed)) _cacheBytes -= removed.Bytes; }
            var entry = new CacheEntry(bitmap, bytes); Cache[key] = entry; Lru.AddLast(key); _cacheBytes += bytes; return entry;
        }
    }
    private sealed record CacheEntry(BitmapImage Bitmap, long Bytes);
}
