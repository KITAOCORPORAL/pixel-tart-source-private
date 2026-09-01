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
    public static readonly DependencyProperty HasFailureProperty = DependencyProperty.RegisterAttached("HasFailure", typeof(bool), typeof(AsyncThumbnail), new PropertyMetadata(false));
    public static readonly DependencyProperty FailureMessageProperty = DependencyProperty.RegisterAttached("FailureMessage", typeof(string), typeof(AsyncThumbnail), new PropertyMetadata(null));
    public static void SetSourcePath(DependencyObject target, string? value) => target.SetValue(SourcePathProperty, value); public static string? GetSourcePath(DependencyObject target) => (string?)target.GetValue(SourcePathProperty);
    public static void SetDecodeWidth(DependencyObject target, double value) => target.SetValue(DecodeWidthProperty, value); public static int GetDecodeWidth(DependencyObject target) => (int)Math.Round((double)target.GetValue(DecodeWidthProperty));
    public static void SetHasFailure(DependencyObject target, bool value) => target.SetValue(HasFailureProperty, value); public static bool GetHasFailure(DependencyObject target) => (bool)target.GetValue(HasFailureProperty);
    public static void SetFailureMessage(DependencyObject target, string? value) => target.SetValue(FailureMessageProperty, value); public static string? GetFailureMessage(DependencyObject target) => (string?)target.GetValue(FailureMessageProperty);

    private static void OnSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Image image) return;
        image.Unloaded -= OnImageUnloaded; image.Unloaded += OnImageUnloaded;
        if (Requests.TryRemove(image, out var previous)) TryCancel(previous);
        image.Source = null;
        SetFailureState(image, false, null);
        var path = GetSourcePath(image);
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path))
        {
            RecordFailure(image, "缩略图不可用：文件不存在。");
            return;
        }
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
        catch (ArgumentException) { RemoveRequest(image, cancellation); cancellation.Dispose(); RecordFailure(image, "缩略图加载失败。"); return; }
        catch (NotSupportedException) { RemoveRequest(image, cancellation); cancellation.Dispose(); RecordFailure(image, "缩略图加载失败。"); return; }

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
                {
                    image.Source = cached.Bitmap;
                    SetFailureState(image, false, null, cancellation);
                }
            }, DispatcherPriority.DataBind);
            await publication.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { RecordFailure(image, "缩略图加载失败。", cancellation); }
        catch (NotSupportedException) { RecordFailure(image, "缩略图加载失败。", cancellation); }
        catch (ArgumentException) { RecordFailure(image, "缩略图加载失败。", cancellation); }
        catch (InvalidOperationException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) { }
        catch (Exception) when (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
        {
            // Decoder/URI/security failures are all rendered as a visible failure state;
            // no async-void/UI-thread exception is allowed to disappear silently.
            RecordFailure(image, "缩略图加载失败。", cancellation);
        }
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

    private static void RecordFailure(Image image, string message, CancellationTokenSource? request = null)
    {
        if (request is not null && (!Requests.TryGetValue(image, out var current) || !ReferenceEquals(current, request))) return;
        Interlocked.Increment(ref _failureCount);
        SetFailureState(image, true, message, request);
    }

    private static void SetFailureState(Image image, bool hasFailure, string? message, CancellationTokenSource? request = null)
    {
        if (image.Dispatcher.HasShutdownStarted || image.Dispatcher.HasShutdownFinished) return;
        void Apply()
        {
            if (image.Dispatcher.HasShutdownStarted || image.Dispatcher.HasShutdownFinished) return;
            if (request is not null && (!Requests.TryGetValue(image, out var current) || !ReferenceEquals(current, request))) return;
            image.SetValue(HasFailureProperty, hasFailure);
            image.SetValue(FailureMessageProperty, hasFailure ? message : null);
        }
        if (image.Dispatcher.CheckAccess())
        {
            Apply();
            return;
        }

        // Await the UI publication before LoadAsync removes its request entry.  A fire-and-
        // forget callback here would otherwise be discarded by the identity check in Apply,
        // leaving decode failures silently blank.
        try { image.Dispatcher.Invoke(Apply, DispatcherPriority.DataBind); }
        catch (InvalidOperationException) when (image.Dispatcher.HasShutdownStarted || image.Dispatcher.HasShutdownFinished) { }
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
