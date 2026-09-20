using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>Shared, presentation-only delay. It never delays or cancels the operation.</summary>
public static class StudioBusyFeedback
{
    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.RegisterAttached(
        "IsBusy", typeof(bool), typeof(StudioBusyFeedback), new PropertyMetadata(false, Changed));
    private static readonly DependencyPropertyKey IsVisiblePropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsVisible", typeof(bool), typeof(StudioBusyFeedback), new PropertyMetadata(false));
    public static readonly DependencyProperty IsVisibleProperty = IsVisiblePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsLongRunningPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsLongRunning", typeof(bool), typeof(StudioBusyFeedback), new PropertyMetadata(false));
    public static readonly DependencyProperty IsLongRunningProperty = IsLongRunningPropertyKey.DependencyProperty;
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(FeedbackState), typeof(StudioBusyFeedback));

    public static bool GetIsBusy(DependencyObject target) => (bool)target.GetValue(IsBusyProperty);
    public static void SetIsBusy(DependencyObject target, bool value) => target.SetValue(IsBusyProperty, value);
    public static bool GetIsVisible(DependencyObject target) => (bool)target.GetValue(IsVisibleProperty);
    public static bool GetIsLongRunning(DependencyObject target) => (bool)target.GetValue(IsLongRunningProperty);
    public static int StageForElapsed(TimeSpan elapsed) => elapsed.TotalMilliseconds < 300 ? 0 : elapsed.TotalMilliseconds < 1500 ? 1 : 2;

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement element) return;
        if (element.GetValue(StateProperty) is not FeedbackState state)
        {
            state = new FeedbackState(element);
            element.SetValue(StateProperty, state);
        }
        state.Update();
    }

    private sealed class FeedbackState
    {
        private readonly FrameworkElement _element;
        private readonly Stopwatch _elapsed = new();
        private readonly DispatcherTimer _timer;

        internal FeedbackState(FrameworkElement element)
        {
            _element = element;
            _timer = new DispatcherTimer(DispatcherPriority.Background, element.Dispatcher) { Interval = TimeSpan.FromMilliseconds(30) };
            _timer.Tick += (_, _) => Tick();
            element.Loaded += (_, _) => Update();
            element.Unloaded += (_, _) => Stop();
        }

        internal void Update()
        {
            if (!GetIsBusy(_element) || !_element.IsLoaded) { Stop(); return; }
            if (_elapsed.IsRunning) return;
            _elapsed.Restart();
            _timer.Start();
        }

        private void Tick()
        {
            var stage = StageForElapsed(_elapsed.Elapsed);
            _element.SetValue(IsVisiblePropertyKey, stage >= 1);
            _element.SetValue(IsLongRunningPropertyKey, stage >= 2);
            if (stage == 2) _timer.Stop();
        }

        private void Stop()
        {
            _timer.Stop(); _elapsed.Reset();
            _element.SetValue(IsVisiblePropertyKey, false);
            _element.SetValue(IsLongRunningPropertyKey, false);
        }
    }
}
