using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class MobileScheduleConcurrencyTests
{
    [TestMethod]
    public async Task AtomicVersionGuardRejectsConcurrentPcAndPhoneUpdate()
    {
        using var context=new Context();await context.Init();var saved=await context.Service.SaveAsync(context.Draft);
        var first=saved.Booking!;var draft=BookingDraftFactory.From(first,[]) with{MutationId=Guid.NewGuid().ToString(),Title="PC"};
        var results=await Task.WhenAll(context.Service.SaveAsync(draft),context.Other.SaveAsync(draft with{MutationId=Guid.NewGuid().ToString(),Title="phone"}));
        Assert.AreEqual(1,results.Count(r=>r.Status==BookingSaveStatus.Saved));Assert.AreEqual(1,results.Count(r=>r.Status==BookingSaveStatus.VersionConflict));
        Assert.AreEqual(2,(await context.Repo.GetAsync(first.Id))!.Revision);
    }
    [TestMethod]
    public async Task RetryAndStatusChangeRemainVersionedAcrossRepositoryReopen()
    {
        using var c=new Context();await c.Init();var created=(await c.Service.SaveAsync(c.Draft)).Booking!;
        var retry=await c.Other.SaveAsync(c.Draft);Assert.AreEqual(created.Id,retry.Booking!.Id);Assert.AreEqual(created.Revision,retry.Booking.Revision);
        var change=BookingDraftFactory.From(created,[]) with{MutationId=Guid.NewGuid().ToString(),Status=ShootBookingStatus.Confirmed};
        var update=await c.Service.SaveAsync(change);Assert.AreEqual(2,update.Booking!.Revision);
        Assert.IsTrue(await c.Repo.SetStatusAsync(created.Id,ShootBookingStatus.Preparing,DateTimeOffset.UtcNow));
        Assert.AreEqual(3,(await c.Repo.GetAsync(created.Id))!.Revision);
        Assert.AreEqual(BookingSaveStatus.VersionConflict,(await c.Other.SaveAsync(change with{MutationId=Guid.NewGuid().ToString()})).Status);
        var replay=await c.Other.SaveAsync(change);Assert.AreEqual(2,replay.Booking!.Revision);Assert.AreEqual(3,(await c.Repo.GetAsync(created.Id))!.Revision);
    }
    [TestMethod]
    public async Task SameOperationCannotChangeDifferentPayloadOrEraseRelatedPeople()
    {
        using var c=new Context();await c.Init();var first=await c.Service.SaveAsync(c.Draft with{ReplacePeople=true,Contacts=[new(){DisplayName="合成联系人"}]});
        var current=first.Booking!;var people=new SqliteBookingPeopleRepository(c.Database);Assert.AreEqual(1,(await people.ListContactsAsync(current.Id)).Count);
        var patch=BookingDraftFactory.From(current,[]) with{MutationId=Guid.NewGuid().ToString(),StartAt=current.StartAtUtc.AddHours(1),EndAt=current.EndAtUtc.AddHours(1)};
        Assert.AreEqual(BookingSaveStatus.Saved,(await c.Service.SaveAsync(patch)).Status);Assert.AreEqual(1,(await people.ListContactsAsync(current.Id)).Count);
        Assert.AreEqual(BookingSaveStatus.ValidationFailed,(await c.Other.SaveAsync(patch with{Title="changed"})).Status);
    }
    [TestMethod]
    public async Task IdenticalConcurrentRetryReturnsSamePersistedRevision()
    {
        using var c=new Context();await c.Init();var b=(await c.Service.SaveAsync(c.Draft)).Booking!;
        var change=BookingDraftFactory.From(b,[]) with{MutationId=Guid.NewGuid().ToString(),Status=ShootBookingStatus.Confirmed};
        var results=await Task.WhenAll(c.Service.SaveAsync(change),c.Other.SaveAsync(change));
        Assert.IsTrue(results.All(r=>r.Status==BookingSaveStatus.Saved));Assert.IsTrue(results.All(r=>r.Booking!.Revision==2));Assert.AreEqual(2,(await c.Repo.GetAsync(b.Id))!.Revision);
        Assert.IsFalse(await c.Repo.SetStatusVersionedAsync(b.Id,ShootBookingStatus.Preparing,DateTimeOffset.UtcNow,1));
        Assert.AreEqual(2,(await c.Repo.GetAsync(b.Id))!.Revision);
    }
    private sealed class Context:IDisposable
    {
        private readonly string _root=Path.Combine(Path.GetTempPath(),"Guangjing-MobileSchedule",Guid.NewGuid().ToString("N"));
        public PixelTartDatabase Database {get;} public SqliteShootBookingRepository Repo {get;} public ShootBookingService Service {get;} public ShootBookingService Other {get;}
        public ShootBookingDraft Draft {get;}=new(){Id=Guid.NewGuid(),CreateIfMissing=true,ExpectedRevision=0,MutationId=Guid.NewGuid().ToString(),Title="合成并发预约",ClientDisplayName="测试",StartAt=DateTimeOffset.UtcNow.AddDays(1),EndAt=DateTimeOffset.UtcNow.AddDays(1).AddHours(1),TimeZoneId="UTC",ShootingType="Portrait"};
        public Context(){Database=new(Path.Combine(_root,"calendar.db"));Repo=new(Database);Service=new(Repo,new BookingConflictDetector(Repo));var other=new SqliteShootBookingRepository(Database);Other=new(other,new BookingConflictDetector(other));}
        public async Task Init(){Assert.IsTrue((await new DatabaseMigrator(Database,new DatabaseBackupService(Database,Path.Combine(_root,"backups"))).MigrateAsync()).Success);}
        public void Dispose(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();try{Directory.Delete(_root,true);}catch{}}
    }
}
