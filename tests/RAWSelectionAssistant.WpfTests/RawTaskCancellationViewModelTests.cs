using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class RawTaskCancellationViewModelTests
{
    [TestMethod]
    public async Task CancelCommand_CancelsActiveTaskAndWaitsForEngineCompletion()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart.RawVmCancel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "input.ARW");
            var output = Path.Combine(root, "output");
            await File.WriteAllBytesAsync(source, [1, 2, 3]);
            Directory.CreateDirectory(output);
            var coordinator = new BlockingCoordinator();
            var viewModel = new RawToJpegViewModel(coordinator, new NoopDialogs());
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sawBusy = false;
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(RawToJpegViewModel.IsBusy)) return;
                if (viewModel.IsBusy) sawBusy = true;
                else if (sawBusy) completed.TrySetResult(true);
            };
            viewModel.AddFiles([source]);
            viewModel.DestinationDirectory = output;

            viewModel.StartCommand.Execute(null);
            await coordinator.FirstWaitEntered.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.IsTrue(viewModel.IsBusy);
            Assert.IsTrue(viewModel.CancelCommand.CanExecute(null));
            viewModel.CancelCommand.Execute(null);
            await coordinator.CancelCalled.WaitAsync(TimeSpan.FromSeconds(10));
            await coordinator.SecondWaitEntered.WaitAsync(TimeSpan.FromSeconds(10));
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(1, coordinator.CancelCallCount);
            Assert.IsGreaterThanOrEqualTo(coordinator.WaitCallCount, 2);
            Assert.IsFalse(viewModel.IsBusy);
            StringAssert.Contains(viewModel.StatusText, "安全取消");
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(source));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private sealed class BlockingCoordinator : IRawToJpegTaskCoordinator
    {
        private readonly Guid _taskId = Guid.NewGuid();
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstWaitEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _secondWaitEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _cancelCalled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _cancelCallCount;
        private int _waitCallCount;

        public Task FirstWaitEntered => _firstWaitEntered.Task;
        public Task SecondWaitEntered => _secondWaitEntered.Task;
        public Task CancelCalled => _cancelCalled.Task;
        public int CancelCallCount => Volatile.Read(ref _cancelCallCount);
        public int WaitCallCount => Volatile.Read(ref _waitCallCount);

        public RawDecoderCapability GetCapability() => new(true, "test", "1", [".ARW"], [".ARW"]);

        public Task<Guid> StartAsync(RawToJpegBatchRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(_taskId);

        public Task CancelAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(_taskId, taskId);
            Interlocked.Increment(ref _cancelCallCount);
            _cancelCalled.TrySetResult(true);
            _completion.TrySetResult(true);
            return Task.CompletedTask;
        }

        public async Task WaitForCompletionAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(_taskId, taskId);
            var count = Interlocked.Increment(ref _waitCallCount);
            if (count == 1) _firstWaitEntered.TrySetResult(true);
            if (count == 2) _secondWaitEntered.TrySetResult(true);
            await _completion.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class NoopDialogs : IDialogService
    {
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [];
        public string? ChooseSaveFile(string title, string filter, string defaultExtension,
            string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public void ShowInfo(string message) { }
        public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false;
        public HelpAction ShowHelp() => HelpAction.None;
        public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
