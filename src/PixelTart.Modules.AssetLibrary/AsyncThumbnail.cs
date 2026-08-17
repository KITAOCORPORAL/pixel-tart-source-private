using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PixelTart.Modules.AssetLibrary;

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

    private static void OnSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Image image) return;
        image.Unloaded -= OnImageUnloaded; image.Unloaded += OnImageUnloaded;
        if (Requests.TryRemove(image, out var previous)) TryCancel(previous);
        image.Source = null; var path = GetSourcePath(image); if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { if (!string.IsNullOrWhiteSpace(path)) Interlocked.Increment(ref _failureCount); return; }
        var cancellation = new CancellationTokenSource();
        Requests[image] = cancellation;
        var dispatcher = image.Dispatcher;
        string requestedPath;
        int width;
        try
        {
            requestedPath = Path.GetFullPath(path);
            width = Math.Clamp(GetDecodeWidth(image), 96, 512);
        }
        catch (ArgumentException) { RemoveRequest(image, cancellation); cancellation.Dispose(); Interlocked.Increment(ref _failureCount); return; }
        catch (NotSupportedException) { RemoveRequest(image, cancellation); cancellation.Dispose(); Interlocked.Increment(ref _failureCount); return; }

        _ = LoadAsync(image, dispatcher, requestedPath, width, cancellation);
    }

    private static async Task LoadAsync(Image image, Dispatcher dispatcher, string requestedPath, int width, CancellationTokenSource cancellation)
    {
        var cancellationToken = cancellation.Token;
        try
        {
            var key = await Task.Run(() => Fingerprint(requestedPath, width, cancellationToken), cancellationToken).ConfigureAwait(false);
            if (!Cache.TryGetValue(key, out var cached))
            {
                var bitmap = await Task.Run(() => Decode(requestedPath, width, cancellationToken), cancellationToken).ConfigureAwait(false);
                cached = AddCache(key, bitmap);
            }
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            var publication = dispatcher.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested
                    && Requests.TryGetValue(image, out var current)
                    && ReferenceEquals(current, cancellation))
                    image.Source = cached.Bitmap;
            }, DispatcherPriority.DataBind);
            await publication.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { Interlocked.Increment(ref _failureCount); }
        catch (NotSupportedException) { Interlocked.Increment(ref _failureCount); }
        catch (ArgumentException) { Interlocked.Increment(ref _failureCount); }
        catch (InvalidOperationException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) { }
        finally { RemoveRequest(image, cancellation); cancellation.Dispose(); }
    }

    private static void OnImageUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && Requests.TryRemove(image, out var cancellation)) TryCancel(cancellation);
    }

    private static void TryCancel(CancellationTokenSource cancellation)
    {
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private static bool RemoveRequest(Image image, CancellationTokenSource cancellation) =>
        ((ICollection<KeyValuePair<Image, CancellationTokenSource>>)Requests).Remove(new(image, cancellation));

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
