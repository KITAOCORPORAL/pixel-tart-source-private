using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PixelTart.Modules.AssetLibrary;

public static class AsyncThumbnail
{
    private sealed class RequestState
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public Task? Completion { get; set; }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Image, RequestState> Requests = new();
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
    public static readonly DependencyProperty ScopedProviderProperty = DependencyProperty.RegisterAttached(
        "ScopedProvider",
        typeof(IAssetThumbnailProvider),
        typeof(AsyncThumbnail),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnSourceChanged));
    public static readonly DependencyProperty HasFailureProperty = DependencyProperty.RegisterAttached("HasFailure", typeof(bool), typeof(AsyncThumbnail), new PropertyMetadata(false));
    public static readonly DependencyProperty FailureMessageProperty = DependencyProperty.RegisterAttached("FailureMessage", typeof(string), typeof(AsyncThumbnail), new PropertyMetadata(null));
    public static void SetSourcePath(DependencyObject target, string? value) => target.SetValue(SourcePathProperty, value); public static string? GetSourcePath(DependencyObject target) => (string?)target.GetValue(SourcePathProperty);
    public static void SetDecodeWidth(DependencyObject target, double value) => target.SetValue(DecodeWidthProperty, value); public static int GetDecodeWidth(DependencyObject target) => (int)Math.Round((double)target.GetValue(DecodeWidthProperty));
    public static void SetScopedProvider(DependencyObject target, IAssetThumbnailProvider? value) => target.SetValue(ScopedProviderProperty, value); public static IAssetThumbnailProvider? GetScopedProvider(DependencyObject target) => (IAssetThumbnailProvider?)target.GetValue(ScopedProviderProperty);
    public static void SetHasFailure(DependencyObject target, bool value) => target.SetValue(HasFailureProperty, value); public static bool GetHasFailure(DependencyObject target) => (bool)target.GetValue(HasFailureProperty);
    public static void SetFailureMessage(DependencyObject target, string? value) => target.SetValue(FailureMessageProperty, value); public static string? GetFailureMessage(DependencyObject target) => (string?)target.GetValue(FailureMessageProperty);

    private static void OnSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Image image) return;
        image.Unloaded -= OnImageUnloaded; image.Unloaded += OnImageUnloaded;
        if (Requests.TryRemove(image, out var previous)) TryCancel(previous.Cancellation);
        image.Source = null;
        SetFailureState(image, false, null);
        var path = GetSourcePath(image);
        if (string.IsNullOrWhiteSpace(path)) return;
        var request = new RequestState();
        Requests[image] = request;
        var dispatcher = image.Dispatcher;
        string requestedPath;
        int width;
        try
        {
            requestedPath = Path.GetFullPath(path);
            width = Math.Clamp(GetDecodeWidth(image), 96, 512);
        }
        catch (ArgumentException) { RemoveRequest(image, request); request.Cancellation.Dispose(); RecordFailure(image, "缩略图加载失败。"); return; }
        catch (NotSupportedException) { RemoveRequest(image, request); request.Cancellation.Dispose(); RecordFailure(image, "缩略图加载失败。"); return; }

        // Publish the common offline/missing-source state immediately. Besides avoiding
        // needless decoder work, this keeps a virtualized image from retaining a pending
        // request when it unloads before the provider gets scheduled.
        if (!File.Exists(requestedPath))
        {
            RemoveRequest(image, request);
            request.Cancellation.Dispose();
            RecordFailure(image, "文件不存在：源文件离线或已移动。");
            return;
        }

        var provider = GetScopedProvider(image) ?? Provider;
        request.Completion = LoadAsync(image, dispatcher, requestedPath, width, provider, request);
    }

    public static async Task CancelAndDrainAsync(DependencyObject scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var requests = Requests
            .Where(pair => IsWithinScope(pair.Key, scope))
            .Select(pair => pair.Value)
            .Distinct()
            .ToArray();
        foreach (var request in requests) TryCancel(request.Cancellation);
        var completions = requests.Select(request => request.Completion).Where(task => task is not null).Cast<Task>().ToArray();
        if (completions.Length == 0) return;
        try { await Task.WhenAll(completions); }
        catch (OperationCanceledException) { }
    }

    private static bool IsWithinScope(DependencyObject candidate, DependencyObject scope)
    {
        for (DependencyObject? current = candidate; current is not null; current = LogicalTreeHelper.GetParent(current) ??
             (current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(current) : null))
        {
            if (ReferenceEquals(current, scope)) return true;
        }
        return false;
    }

    private static async Task LoadAsync(Image image, Dispatcher dispatcher, string requestedPath, int width, IAssetThumbnailProvider provider, RequestState request)
    {
        var cancellationToken = request.Cancellation.Token;
        try
        {
            var result = await provider.GetAsync(new(requestedPath, width), cancellationToken).ConfigureAwait(false);
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            var publication = dispatcher.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested
                    && Requests.TryGetValue(image, out var current)
                    && ReferenceEquals(current, request))
                {
                    image.Source = result.Bitmap;
                    if (result.IsAvailable)
                        SetFailureState(image, false, null, request);
                    else
                        RecordFailure(image, result.PlaceholderMessage ?? "缩略图不可用。", request);
                }
            }, DispatcherPriority.DataBind);
            await publication.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { RecordFailure(image, "缩略图加载失败。", request); }
        catch (NotSupportedException) { RecordFailure(image, "缩略图加载失败。", request); }
        catch (ArgumentException) { RecordFailure(image, "缩略图加载失败。", request); }
        catch (InvalidOperationException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) { }
        catch (Exception) when (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
        {
            // Decoder/URI/security failures are all rendered as a visible failure state;
            // no async-void/UI-thread exception is allowed to disappear silently.
            RecordFailure(image, "缩略图加载失败。", request);
        }
        finally { RemoveRequest(image, request); request.Cancellation.Dispose(); }
    }

    private static void OnImageUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && Requests.TryRemove(image, out var request)) TryCancel(request.Cancellation);
    }

    private static void TryCancel(CancellationTokenSource cancellation)
    {
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private static bool RemoveRequest(Image image, RequestState request) =>
        ((ICollection<KeyValuePair<Image, RequestState>>)Requests).Remove(new(image, request));

    private static void RecordFailure(Image image, string message, RequestState? request = null)
    {
        if (request is not null && (!Requests.TryGetValue(image, out var current) || !ReferenceEquals(current, request))) return;
        Interlocked.Increment(ref _failureCount);
        SetFailureState(image, true, message, request);
    }

    private static void SetFailureState(Image image, bool hasFailure, string? message, RequestState? request = null)
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
