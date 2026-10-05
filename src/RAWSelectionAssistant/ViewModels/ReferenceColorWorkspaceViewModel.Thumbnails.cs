using System.ComponentModel;
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private sealed class ThumbnailJob
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public bool Finished { get; set; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly ConcurrentDictionary<Guid, ThumbnailJob> _thumbnailJobs = new();
    private readonly SemaphoreSlim _thumbnailSlots = new(2);
    private void OnTargetAdjustmentChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is ReferenceTargetItem item && args.PropertyName is nameof(ReferenceTargetItem.AppliedLookSnapshot)
            or nameof(ReferenceTargetItem.ColorAdjustmentStackSnapshot) or nameof(ReferenceTargetItem.FilmSettingsSnapshot)
            or nameof(ReferenceTargetItem.EngineSnapshot) or nameof(ReferenceTargetItem.ExecutionModeSnapshot)) ScheduleTargetThumbnail(item);
    }
    private void CancelTargetThumbnail(Guid id)
    {
        if (_thumbnailJobs.TryRemove(id, out var job)) lock (job) { if (!job.Finished) job.Cancellation.Cancel(); }
    }
    private void ScheduleTargetThumbnail(ReferenceTargetItem target)
    {
        if (_disposed || target.SourceThumbnail is null || !Targets.Contains(target)) return;
        var source = target.SourceThumbnail;
        var look = target.AppliedLookSnapshot?.Normalize(); var stack = target.ColorAdjustmentStackSnapshot?.DeepClone();
        var film = target.FilmSettingsSnapshot is { } settings ? settings with { } : null;
        var engine = target.EngineSnapshot; var mode = target.ExecutionModeSnapshot; var master = target.RawMaster;
        CancelTargetThumbnail(target.Id);
        if (look is null && stack is null && film?.Enabled != true)
        { target.Thumbnail = source; target.ThumbnailStatus = "原图缩略预览（无调整）"; return; }
        var job = new ThumbnailJob(); _thumbnailJobs[target.Id] = job;
        target.ThumbnailStatus = "正在更新调整后缩略预览";
        _ = RenderAsync();
        async Task RenderAsync()
        {
            var token = job.Cancellation.Token; var entered = false;
            try
            {
                await Task.Delay(250, token); await _thumbnailSlots.WaitAsync(token); entered = true;
                BitmapSource result;
                if (engine == ColorStudioMatchEngine.MatchV4Beta)
                {
                    if (master is null || !TetherReferenceModeViewModel.SupportsExperimentalV4Stack(stack, film)) throw new NotSupportedException();
                    result = await Editor.RenderFrozenV4ThumbnailAsync(master, TetherReferenceModeViewModel.ResolveExperimentalV4Look(look, stack), mode, token);
                }
                else if (master is not null)
                    result = await Task.Run(() => RawDisplayBitmapAdapter.ToBitmap(_rawPipeline.Render(master.Image, look, stack, 320, token, film).Pixels), token);
                else result = await Editor.RenderFrozenThumbnailAsync(source, look, stack, film, token);
                token.ThrowIfCancellationRequested();
                if (!_disposed && _thumbnailJobs.TryGetValue(target.Id, out var current) && ReferenceEquals(current, job) && Targets.Contains(target))
                { target.Thumbnail = result; target.ThumbnailStatus = "当前调整后缩略预览（缩小采样）"; }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
            {
                if (!_disposed && _thumbnailJobs.TryGetValue(target.Id, out var current) && ReferenceEquals(current, job))
                { target.Thumbnail = source; target.ThumbnailStatus = "原图缩略预览；当前调整预览未能生成"; }
            }
            finally
            {
                if (entered) _thumbnailSlots.Release();
                _thumbnailJobs.TryRemove(new KeyValuePair<Guid, ThumbnailJob>(target.Id, job));
                lock (job) { job.Finished = true; job.Cancellation.Dispose(); }
                job.Completion.TrySetResult();
            }
        }
    }
    public async Task FlushThumbnailsAsync()
    {
        while (_thumbnailJobs.Count > 0) await Task.WhenAll(_thumbnailJobs.Values.Select(job => job.Completion.Task).ToArray());
    }
}
