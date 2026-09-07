using System.Windows.Threading;

namespace RAWSelectionAssistant.WpfTests;

/// <summary>Await real async UI work while servicing the same STA dispatcher as the product.</summary>
internal static class DispatcherTaskTestExtensions
{
    internal static void CompleteOnDispatcher(this Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            var timedOut = false;
            var timeout = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
            timeout.Tick += (_, _) => { timedOut = true; frame.Continue = false; };
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => frame.Continue = false)), TaskScheduler.Default);
            timeout.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timeout.Stop(); }
            Assert.IsFalse(timedOut, "The real UI operation did not finish within the bounded dispatcher wait.");
        }
        task.GetAwaiter().GetResult();
    }

    internal static T CompleteOnDispatcher<T>(this Task<T> task)
    {
        ((Task)task).CompleteOnDispatcher();
        return task.GetAwaiter().GetResult();
    }
}
