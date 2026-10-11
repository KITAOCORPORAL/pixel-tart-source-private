using RAWSelectionAssistant.Core.Services.Database;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class IdentityBridgeRecoveryTests
{
    private static async Task<IdentityBridgeRepository> CreateAsync(TempDirectory temp)
    {
        var db = new PixelTartDatabase(temp.Combine("product.db"));
        var result = await new DatabaseMigrator(db, new DatabaseBackupService(db, temp.Combine("backup"))).MigrateAsync();
        Assert.IsTrue(result.Success);
        return new(db);
    }
    [TestMethod]
    public async Task RetryCannotClearConflict()
    {
        using var temp = new TempDirectory(); var repo = await CreateAsync(temp); var id = Guid.NewGuid();
        var op = await repo.BeginOperationAsync("k", "Import", "Tether", id);
        await repo.BeginOperationAsync("k", "Different", "Tether", id);
        var retry = await repo.BeginOperationAsync("k", "Import", "Tether", id);
        Assert.AreEqual(CrossModuleOperationState.Conflict, retry.State);
    }
    [TestMethod]
    public async Task CompletedIdentityCannotBeOverwritten()
    {
        using var temp = new TempDirectory(); var repo = await CreateAsync(temp);
        var op = await repo.BeginOperationAsync("k", "Import", "Tether", Guid.NewGuid());
        var first = Guid.NewGuid(); await repo.CompleteOperationAsync(op.OperationId, first);
        try { await repo.CompleteOperationAsync(op.OperationId, Guid.NewGuid()); Assert.Fail("Completed identity was overwritten"); }
        catch (InvalidOperationException) { }
    }
    [TestMethod]
    public async Task TokensAreNormalizedBeforeComparison()
    {
        using var temp = new TempDirectory(); var repo = await CreateAsync(temp); var id = Guid.NewGuid();
        await repo.BeginOperationAsync(" k ", " Import ", " Tether ", id);
        var op = await repo.BeginOperationAsync("k", " Import ", " Tether ", id);
        Assert.AreNotEqual(CrossModuleOperationState.Conflict, op.State);
    }
    [TestMethod]
    public async Task ConcurrentClaimHasOneOwnerAndRecoveryFencesLateCompletion()
    {
        using var temp = new TempDirectory(); var repo = await CreateAsync(temp);
        var op = await repo.BeginOperationAsync("claim", "Import", "Tether", Guid.NewGuid());
        var claims = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => repo.TryClaimOperationAsync(op.OperationId))));
        Assert.AreEqual(1, claims.Count(c => c is not null));
        var owner = claims.Single(c => c is not null)!;
        var running = await repo.BeginOperationAsync("claim", "Import", "Tether", op.SourceId);
        Assert.AreEqual(CrossModuleOperationState.Running, running.State);
        Assert.IsTrue(await repo.RetryOperationAsync(op.OperationId, owner.Attempt));
        Assert.IsFalse(await repo.CompleteClaimAsync(owner, Guid.NewGuid()));
        var replacement = await repo.TryClaimOperationAsync(op.OperationId);
        Assert.IsNotNull(replacement);
        Assert.IsTrue(await repo.FailClaimAsync(replacement, "LIBRARY_OFFLINE"));
        Assert.AreEqual("LIBRARY_OFFLINE", (await repo.GetOperationAsync(op.OperationId))!.LastErrorCode);
        Assert.AreEqual(1, (await repo.ListIncompleteOperationsAsync()).Count);
        Assert.IsTrue(await repo.RetryOperationAsync(op.OperationId, replacement.Attempt));
        var final = await repo.TryClaimOperationAsync(op.OperationId);
        Assert.IsTrue(await repo.CompleteClaimAsync(final!, Guid.NewGuid()));
        Assert.IsFalse(await repo.CompleteClaimAsync(final!, Guid.NewGuid()));
    }    [TestMethod]
    public async Task HandoffExecutionLockRejectsSecondOwner()
    {
        using var temp=new TempDirectory(); var repo=await CreateAsync(temp);
        using(var first=repo.TryAcquireExecutionLock("same"))
        { Assert.IsNotNull(first); Assert.IsNull(repo.TryAcquireExecutionLock(" same ")); }
        using var recovered=repo.TryAcquireExecutionLock("same"); Assert.IsNotNull(recovered);
    }    [TestMethod]
    public async Task ConcurrentBeginAndClaimProduceOneDurableExecutor()
    {
        using var temp=new TempDirectory(); var repo=await CreateAsync(temp); var source=Guid.NewGuid();
        var results=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>Task.Run(async()=>
        {
            var operation=await repo.BeginOperationAsync("duplicate-event","TetherToLibrary","TetherAsset",source);
            return await repo.TryClaimOperationAsync(operation.OperationId);
        })));
        Assert.AreEqual(1,results.Count(x=>x is not null));
        Assert.HasCount(1,await repo.ListIncompleteOperationsAsync());
    }}
