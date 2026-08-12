using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ModalInteractionContractTests
{
    [TestMethod]
    public async Task Cancel_CancelsToken_InvokesHandlerOnce_AndReleasesHost()
    {
        var calls = 0;
        using var session = new ModalSession(cancelAsync: () =>
        {
            calls++;
            return Task.CompletedTask;
        });
        using var host = new ModalHost();
        host.Show(session);

        var accepted = await host.RequestCancelAsync();

        Assert.IsTrue(accepted);
        Assert.AreEqual(1, calls);
        Assert.IsTrue(session.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(host.IsOpen);
        Assert.IsTrue(session.IsClosed);
    }

    [TestMethod]
    public async Task FailedClose_LeavesSessionHosted_AndAllowsRetry()
    {
        var attempts = 0;
        using var session = new ModalSession(closeAsync: () =>
        {
            attempts++;
            if (attempts == 1)
                throw new IOException("temporary close failure");
            return Task.CompletedTask;
        });
        using var host = new ModalHost();
        host.Show(session);

        await Assert.ThrowsExactlyAsync<IOException>(() => host.RequestCloseAsync());
        Assert.IsTrue(host.IsOpen);
        Assert.IsTrue(session.CanClose);

        Assert.IsTrue(await host.RequestCloseAsync());
        Assert.AreEqual(2, attempts);
        Assert.IsFalse(host.IsOpen);
    }

    [TestMethod]
    public async Task NonCancelableSession_DoesNotInvokeHandlerOrClose()
    {
        var calls = 0;
        using var session = new ModalSession(canCancel: false, cancelAsync: () =>
        {
            calls++;
            return Task.CompletedTask;
        });
        using var host = new ModalHost();
        host.Show(session);

        Assert.IsFalse(await host.RequestCancelAsync());
        Assert.AreEqual(0, calls);
        Assert.IsTrue(host.IsOpen);
        Assert.IsFalse(session.CancellationToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ConcurrentCancelRequests_AreIdempotent()
    {
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new ModalSession(cancelAsync: async () =>
        {
            Interlocked.Increment(ref calls);
            await release.Task;
        });
        using var host = new ModalHost();
        host.Show(session);

        var first = host.RequestCancelAsync();
        var second = host.RequestCancelAsync();
        release.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.IsTrue(results.All(result => result));
        Assert.AreEqual(1, calls);
        Assert.IsFalse(host.IsOpen);
    }
}
