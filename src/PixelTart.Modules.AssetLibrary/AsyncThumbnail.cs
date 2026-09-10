using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PixelTart.Modules.AssetLibrary;

public static class AsyncThumbnail
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Image, CancellationTokenSource> Requests = new();
    private static IAssetThumbnailProvider _provider = new WpfAssetThumbnailProvider();
    private static int _failureCount;

    public static int PendingRequestCount => Requests.Count;
    public static int FailureCount => Volatile.Read(ref _failureCount);
    public static IAssetThumbnailProvider Provider
    {
        get => Volatile.Read(ref _provider);
        set => Volatile.Write(ref _provider, value ?? throw new ArgumentNullException(nameof(value)));
    }

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
            var result = await Provider.GetAsync(new(requestedPath, width), cancellationToken).ConfigureAwait(false);
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            var publication = dispatcher.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested
                    && Requests.TryGetValue(image, out var current)
                    && ReferenceEquals(current, cancellation))
                {
                    image.Source = result.Bitmap;
                    if (result.IsAvailable)
                        SetFailureState(image, false, null, cancellation);
                    else
                        RecordFailure(image, result.PlaceholderMessage ?? "缩略图不可用。", cancellation);
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

}
