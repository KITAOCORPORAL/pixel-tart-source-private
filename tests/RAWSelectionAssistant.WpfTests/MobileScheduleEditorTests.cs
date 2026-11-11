using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class MobileScheduleEditorTests
{
    [TestMethod]
    public async Task EditorRetryWithPeopleAndChecklistDoesNotExecuteAgain()
    {
        var root=Path.Combine(Path.GetTempPath(),"Guangjing-ScheduleEditor",Guid.NewGuid().ToString("N"));
        try
        {
            var db=new PixelTartDatabase(Path.Combine(root,"calendar.db"));Assert.IsTrue((await new DatabaseMigrator(db,new DatabaseBackupService(db,Path.Combine(root,"backup"))).MigrateAsync()).Success);
            var repo=new SqliteShootBookingRepository(db);var service=new ShootBookingService(repo,new BookingConflictDetector(repo));var editor=new ShootBookingEditorViewModel(service,new SqliteProjectRepository(db));await editor.InitializeAsync();
            editor.Title="合成编辑重试";editor.ClientDisplayName="测试";editor.Contacts.Add(new(){DisplayName="合成联系人",IsPrimary=true});editor.Requirements.AddCommand.Execute(null);editor.Requirements.Items[0].ItemText="合成准备";
            var count=0;ShootBooking? first=null;editor.Saved+=(_,b)=>{count++;first??=b;};
            await ((AsyncRelayCommand)editor.SaveCommand).ExecuteAsync(null);Assert.IsNotNull(first,editor.ValidationText);
            await ((AsyncRelayCommand)editor.SaveCommand).ExecuteAsync(null);Assert.AreEqual(2,count,editor.ValidationText);
            Assert.AreEqual(1,(await repo.GetAsync(first.Id))!.Revision);
            Assert.AreEqual(1,(await repo.GetRequirementsAsync(first.Id)).Count);
            Assert.AreEqual(1,(await new SqliteBookingPeopleRepository(db).ListContactsAsync(first.Id)).Count);
        }
        finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();try{Directory.Delete(root,true);}catch{}}
    }
}
