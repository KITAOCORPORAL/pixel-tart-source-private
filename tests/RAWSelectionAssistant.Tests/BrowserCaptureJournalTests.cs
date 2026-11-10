using RAWSelectionAssistant.Core.Services.AssetLibrary;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BrowserCaptureJournalTests
{
    static BrowserCaptureRecord Record()=>new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),null,"https://example.test/page","https://example.test/image.jpg","Synthetic",DateTimeOffset.UtcNow,new string('A',64));
    [TestMethod]public async Task SameCaptureReservationSurvivesRestartWithoutChangingIdentity()
    {
        using var temp=new TempDirectory();var path=temp.Combine("captures.sqlite");var row=Record();
        Assert.AreEqual(row,await new BrowserCaptureJournal(path).ReserveAsync(row,"fingerprint"));
        Assert.AreEqual(row,await new BrowserCaptureJournal(path).ReserveAsync(row,"fingerprint"));
    }
    [TestMethod]public async Task ChangedPayloadOrDestinationCannotReuseCaptureIdentity()
    {
        using var temp=new TempDirectory();var store=new BrowserCaptureJournal(temp.Combine("captures.sqlite"));var row=Record();await store.ReserveAsync(row,"original");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>store.ReserveAsync(row with{ProjectId=Guid.NewGuid()},"different"));
        Assert.AreEqual(row,await store.ReserveAsync(row,"original"));
    }
    [TestMethod]public async Task OnlyCommittedAssetHasDurableReceiptAndSourceMetadata()
    {
        using var temp=new TempDirectory();var path=temp.Combine("captures.sqlite");var store=new BrowserCaptureJournal(path);var row=Record();await store.ReserveAsync(row,"same");
        await Assert.ThrowsExactlyAsync<ArgumentException>(()=>store.CompleteAsync(row));
        var done=row with{AssetId=Guid.NewGuid(),Outcome="Imported",ImportedAtUtc=DateTimeOffset.UtcNow};await store.CompleteAsync(done);
        Assert.AreEqual(done,await new BrowserCaptureJournal(path).ReserveAsync(row,"same"));Assert.AreEqual(done,await new BrowserCaptureJournal(path).LatestSourceAsync(done.LibraryId,done.AssetId!.Value));
    }
}
