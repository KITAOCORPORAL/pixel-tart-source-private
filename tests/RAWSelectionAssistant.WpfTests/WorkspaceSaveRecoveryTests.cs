using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using AssetLibraryPage = PixelTart.Modules.AssetLibrary.AssetLibraryPage;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tasks;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class WorkspaceSaveRecoveryTests
{
    private static string Root=>Environment.GetEnvironmentVariable("KITAO_WORKSPACE_RECOVERY_ROOT")??throw new InvalidOperationException("Explicit isolated recovery root required");
    private static async Task Record(string name,object value)
    {
        Directory.CreateDirectory(Root);await File.WriteAllTextAsync(Path.Combine(Root,name+".json"),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
    }
    private static IEnumerable<Target> Find<Target>(DependencyObject parent) where Target:DependencyObject
    {
        if(parent is Target match)yield return match;
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)foreach(var child in Find<Target>(VisualTreeHelper.GetChild(parent,index)))yield return child;
    }
    private static async Task SaveButton(FreeCanvasView canvas)
    {
        canvas.HeaderPanel.Children.OfType<Button>().Single(button=>Equals(button.Content,"保存")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.IsTrue(await canvas.FlushAsync());
    }
    private static async Task AssertLeaseAvailable(AssetLibraryContainerDescriptor descriptor,bool checkDatabaseHandle=true)
    {
        await using var lease=await new AssetLibraryContainerService().AcquireWriteLeaseAsync(descriptor);
        if(checkDatabaseHandle){using var database=new FileStream(descriptor.DatabasePath,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}
    }
    private static async Task AssertLeaseHeld(AssetLibraryContainerDescriptor descriptor)=>await Assert.ThrowsExactlyAsync<InvalidOperationException>(async()=>
    {
        await using var lease=await new AssetLibraryContainerService().AcquireWriteLeaseAsync(descriptor);
    });
    private static async Task OpenRecent(Fixture fixture,AssetLibraryContainerDescriptor descriptor,bool expectSuccess)
    {
        var recent=fixture.Host.ProductHarnessMenu.Items.OfType<MenuItem>().Single(item=>Equals(item.Header,descriptor.DisplayName));
        var command=recent.Items.OfType<MenuItem>().Single(item=>Equals(item.Header,"打开 / 切换"));
        var created=fixture.Pages.Count;command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var deadline=DateTime.UtcNow.AddSeconds(15);
        while(DateTime.UtcNow<deadline)
        {
            var state=(TextBlock)typeof(AssetLibraryWorkspaceHost).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fixture.Host)!;
            if(fixture.Pages.Count>created&&(expectSuccess?fixture.Host.CurrentDescriptor?.LibraryId==descriptor.LibraryId:state.Text.StartsWith("切换失败")))return;
            await Task.Delay(20);
        }
        Assert.Fail("Recent-library product command did not reach expected switch result");
    }
    [TestMethod]
    public Task FailedSwitchRepairsSavesThenSwitchesBackWithoutPoisonedDispose()=>RunSta(async()=>
    {
        EnsureTestApplication();await using var fixture=await Fixture.Create("switch");var original=fixture.Host.CurrentPage!;var canvas=original.ActiveCanvas!;
        canvas.Editor.Rename("A库尚未保存的恢复标题");var document=canvas.Editor.Document;var savedBytes=await File.ReadAllBytesAsync(fixture.CanvasPath);
        using(var blocked=new FileStream(fixture.CanvasPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            await OpenRecent(fixture,fixture.Second,false);
            Assert.AreSame(original,fixture.Host.CurrentPage);Assert.AreSame(canvas,original.ActiveCanvas);Assert.AreSame(document,canvas.Editor.Document);Assert.IsTrue(original.ViewModel.IsReady);
            await AssertLeaseHeld(fixture.First);await AssertLeaseAvailable(fixture.Second);
            CollectionAssert.AreEqual(savedBytes,await File.ReadAllBytesAsync(fixture.CanvasPath));
            Assert.IsEmpty(Directory.GetFiles(fixture.Store.DirectoryPath,"*.tmp"));
            Assert.AreEqual(2,fixture.Pages.Count);Assert.IsTrue(fixture.Pages[1].ViewModel.P3RepositoryDisposeStarted);
            await Record("SWITCH_SAVE_FAILURE",new{pid=Environment.ProcessId,currentPageRetained=true,documentRetained=true,originalReady=original.ViewModel.IsReady,secondCandidateDisposed=fixture.Pages[1].ViewModel.P3RepositoryDisposeStarted,cachedTaskFaulted=PageDisposeTask(original)?.IsFaulted});
        }
        await SaveButton(canvas);Assert.AreEqual(document.Name,(await fixture.Store.LoadAsync(document.CanvasId))!.Name);
        await OpenRecent(fixture,fixture.Second,true);var switched=fixture.Host.CurrentDescriptor?.LibraryId==fixture.Second.LibraryId;
        await Record("SWITCH_AFTER_REPAIR",new{pid=Environment.ProcessId,switchedToB=switched,currentLibrary=fixture.Host.CurrentDescriptor?.LibraryId,expected=fixture.Second.LibraryId,originalIsReady=original.ViewModel.IsReady,cachedTaskFaulted=PageDisposeTask(original)?.IsFaulted});
        Assert.IsTrue(switched,"Repair + actual Save must permit the next A-to-B switch");
        Assert.AreEqual(0,original.ViewModel.P3PendingOperationCount);Assert.IsTrue(original.ViewModel.P3RepositoryDisposeStarted);await AssertLeaseAvailable(fixture.First);
        await fixture.Host.SwitchToContainerAsync(fixture.First.ContainerPath);await fixture.Host.CurrentPage!.ShowCanvasAsync((await fixture.Store.LoadAsync(document.CanvasId))!);
        Assert.AreEqual(document.Name,fixture.Host.CurrentPage.ActiveCanvas!.Editor.Document.Name);var retained=fixture.Host.CurrentPage;
        await fixture.Host.SwitchToContainerAsync(fixture.First.ContainerPath);Assert.AreSame(retained,fixture.Host.CurrentPage);Assert.AreEqual(0,fixture.Pages[2].ViewModel.P3PendingOperationCount);await AssertLeaseAvailable(fixture.Second);
        await fixture.Host.DisposeAsync();await fixture.Host.DisposeAsync();await AssertLeaseAvailable(fixture.First);await AssertLeaseAvailable(fixture.Second);
        foreach(var retired in fixture.Pages){Assert.IsTrue(retired.ViewModel.P3RepositoryDisposeStarted);Assert.AreEqual(0,retired.ViewModel.P3PendingOperationCount);}
        await Record("RECOVERY_RESOURCE_RELEASE",new{pid=Environment.ProcessId,createdPages=fixture.Pages.Count,allPageRepositoriesDisposed=true,allTrackedOperationsDrained=true,firstAndSecondWriterLeasesReleased=true,firstAndSecondDatabasesExclusiveOpen=true,noTemporarySaveFiles=Directory.GetFiles(fixture.Store.DirectoryPath,"*.tmp").Length==0});
        await Record("INDEPENDENT_REOPEN",new{pid=Environment.ProcessId,root=fixture.Directory,canvasRoot=fixture.Store.DirectoryPath,canvasId=document.CanvasId,name=document.Name,first=fixture.First.ContainerPath,second=fixture.Second.ContainerPath});
    });
    [TestMethod]
    public Task FailedHostCloseNeverReturnsFalseSuccessAndCanRetryAfterSave()=>RunSta(async()=>
    {
        EnsureTestApplication();await using var fixture=await Fixture.Create("close");var page=fixture.Host.CurrentPage!;var canvas=page.ActiveCanvas!;canvas.Editor.Rename("退出失败后继续编辑");
        using(var blocked=new FileStream(fixture.CanvasPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            var first=fixture.Host.DisposeAsync().AsTask();await Assert.ThrowsExactlyAsync<IOException>(()=>first);
            var second=fixture.Host.DisposeAsync().AsTask();Exception? failure=null;try{await second;}catch(Exception error){failure=error;}
            await Record("CLOSE_RETRY_BLOCKED",new{pid=Environment.ProcessId,firstFaulted=first.IsFaulted,secondStatus=second.Status.ToString(),secondFailure=failure?.GetType().Name,pageReady=page.ViewModel.IsReady,currentRetained=ReferenceEquals(page,fixture.Host.CurrentPage)});
            Assert.IsInstanceOfType<IOException>(failure,"A second close while writes remain blocked must not report success");
            Assert.AreSame(page,fixture.Host.CurrentPage);Assert.IsTrue(page.ViewModel.IsReady);await AssertLeaseHeld(fixture.First);canvas.Editor.Rename("失败后仍可修改");
        }
        await SaveButton(canvas);await fixture.Host.DisposeAsync();await fixture.Host.DisposeAsync();Assert.IsNull(fixture.Host.CurrentPage);
        await AssertLeaseAvailable(fixture.First);await AssertLeaseAvailable(fixture.Second);Assert.AreEqual("失败后仍可修改",(await fixture.Store.LoadAsync(canvas.Editor.Document.CanvasId))!.Name);
    });
    [TestMethod]
    public Task ExistingSaveConfirmationRejectsCloseUntilWriteRecovery()=>RunSta(async()=>
    {
        EnsureTestApplication();await using var fixture=await Fixture.Create("confirmation");var page=fixture.Host.CurrentPage!;var canvas=page.ActiveCanvas!;
        canvas.Editor.Rename("保留并等待保存的退出修改");
        using(var blocked=new FileStream(fixture.CanvasPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Assert.IsFalse(await canvas.PrepareDocumentChangeAsync(()=>MessageBoxResult.Yes));
            Assert.IsFalse(page.HasStartedDisposal);Assert.IsTrue(page.ViewModel.IsReady);await AssertLeaseHeld(fixture.First);
        }
        Assert.IsTrue(await canvas.PrepareDocumentChangeAsync(()=>MessageBoxResult.Yes));
        Assert.AreEqual(canvas.Editor.Document.Name,(await fixture.Store.LoadAsync(canvas.Editor.Document.CanvasId))!.Name);
        await fixture.Host.DisposeAsync();await AssertLeaseAvailable(fixture.First);
    });
    private static Task? PageDisposeTask(AssetLibraryPage page)=>(Task?)typeof(AssetLibraryPage).GetField("_disposeTask",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page);
    [TestMethod]
    public Task ConcurrentPageAndHostDisposalShareOneExecutionAndWaitForBackgroundDrain()=>RunSta(async()=>
    {
        EnsureTestApplication();await using var fixture=await Fixture.Create("concurrent");var page=fixture.Host.CurrentPage!;
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var started=0;
        var background=page.ViewModel.RunTrackedP3OperationAsync(async()=>{started++;await release.Task;});
        var first=fixture.Host.DisposeAsync().AsTask();var second=fixture.Host.DisposeAsync().AsTask();
        var pageFirst=page.DisposeAsync().AsTask();var pageSecond=page.DisposeAsync().AsTask();
        var switchWhileClosing=fixture.Host.SwitchToContainerAsync(fixture.Second.ContainerPath);
        try
        {
            Assert.AreSame(first,second);Assert.AreSame(pageFirst,pageSecond);Assert.IsFalse(first.IsCompleted);Assert.IsFalse(second.IsCompleted);
            Assert.IsTrue(page.HasStartedDisposal);Assert.IsTrue(page.ViewModel.P3ShutdownStarted);Assert.IsFalse(page.ViewModel.P3RepositoryDisposeStarted);
            Assert.IsFalse(switchWhileClosing.IsCompleted);
            await AssertLeaseHeld(fixture.First);Assert.AreEqual(1,started);
        }
        finally{release.TrySetResult();}
        await background;await Task.WhenAll(first,second,pageFirst,pageSecond);await switchWhileClosing;
        Assert.IsNull(fixture.Host.CurrentPage);Assert.AreEqual(1,fixture.Pages.Count,"Closing must not initialize a competing candidate page");
        Assert.AreEqual(0,page.ViewModel.P3PendingOperationCount);Assert.IsTrue(page.ViewModel.P3RepositoryDisposeStarted);
        Assert.AreSame(first,fixture.Host.DisposeAsync().AsTask());Assert.AreSame(pageFirst,page.DisposeAsync().AsTask());
        await AssertLeaseAvailable(fixture.First);await AssertLeaseAvailable(fixture.Second);
        await Record("CONCURRENT_DISPOSAL",new{pid=Environment.ProcessId,hostSharedTask=ReferenceEquals(first,second),pageSharedTask=ReferenceEquals(pageFirst,pageSecond),trackedExecutionCount=started,drainedOperations=page.ViewModel.P3PendingOperationCount});
    });
    [TestMethod]
    public Task ConcurrentSaveFailureSharesAttemptAndPreservesLiveLeaseUntilRetry()=>RunSta(async()=>
    {
        EnsureTestApplication();await using var fixture=await Fixture.Create("concurrent-save-failure");var page=fixture.Host.CurrentPage!;var canvas=page.ActiveCanvas!;
        canvas.Editor.Rename("并发保存失败后保留");
        var saveGate=(SemaphoreSlim)typeof(FreeCanvasView).GetField("_saveGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(canvas)!;
        using(var blocked=new FileStream(fixture.CanvasPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            await saveGate.WaitAsync();var first=fixture.Host.DisposeAsync().AsTask();var second=fixture.Host.DisposeAsync().AsTask();
            var pageFirst=page.DisposeAsync().AsTask();var pageSecond=page.DisposeAsync().AsTask();
            try{Assert.AreSame(first,second);Assert.AreSame(pageFirst,pageSecond);Assert.IsFalse(first.IsCompleted);Assert.IsFalse(page.HasStartedDisposal);await AssertLeaseHeld(fixture.First);}
            finally{saveGate.Release();}
            await Assert.ThrowsExactlyAsync<IOException>(()=>first);await Assert.ThrowsExactlyAsync<IOException>(()=>second);
            await Assert.ThrowsExactlyAsync<IOException>(()=>pageFirst);await Assert.ThrowsExactlyAsync<IOException>(()=>pageSecond);
            Assert.IsTrue(page.ViewModel.IsReady);Assert.IsFalse(page.HasStartedDisposal);Assert.IsNull(PageDisposeTask(page));
        }
        await SaveButton(canvas);await fixture.Host.DisposeAsync();await AssertLeaseAvailable(fixture.First);
    });
    [TestMethod]
    public Task IrreversibleCleanupFailureKeepsOriginalFaultAndDoesNotRestartCleanup()=>RunSta(async()=>
    {
        EnsureTestApplication();var fixture=await Fixture.Create("terminal-cleanup");var page=fixture.Host.CurrentPage!;
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed=page.ViewModel.RunTrackedP3OperationAsync(()=>release.Task);
        var first=fixture.Host.DisposeAsync().AsTask();var pageTask=PageDisposeTask(page)!;
        try
        {
            Assert.IsTrue(page.HasStartedDisposal);Assert.IsFalse(first.IsCompleted);
            release.SetException(new IOException("Injected failure during irreversible tracked-operation drain"));
            await Assert.ThrowsExactlyAsync<IOException>(()=>first);await Assert.ThrowsExactlyAsync<IOException>(()=>observed);
            var repeated=fixture.Host.DisposeAsync().AsTask();Assert.AreSame(first,repeated);Assert.AreSame(pageTask,page.DisposeAsync().AsTask());
            await Assert.ThrowsExactlyAsync<IOException>(()=>repeated);Assert.IsFalse(page.ViewModel.IsReady);await AssertLeaseHeld(fixture.First);
            await Record("TERMINAL_CLEANUP_FAULT",new{pid=Environment.ProcessId,cleanupStarted=page.HasStartedDisposal,hostFaultRetained=ReferenceEquals(first,repeated),pageFaultRetained=ReferenceEquals(pageTask,PageDisposeTask(page)),leaseRetained=true,scope="Injected non-save cleanup failure; not automatically restarted or labeled a successful close"});
        }
        finally
        {
            release.TrySetResult();
            foreach(var name in new[]{"_repository","_inspirationTray"})
                await ((IAsyncDisposable)typeof(AssetLibraryViewModel).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page.ViewModel)!).DisposeAsync();
            await ((AssetLibraryWriteLease)typeof(AssetLibraryWorkspaceHost).GetField("_lease",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fixture.Host)!).DisposeAsync();
        }
        await AssertLeaseAvailable(fixture.First);await AssertLeaseAvailable(fixture.Second);
    });
    [TestMethod]
    public Task CandidateCleanupFailureStillReleasesCandidateLeaseAndSwitchGate()=>RunSta(async()=>
    {
        EnsureTestApplication();await using var fixture=await Fixture.Create("candidate-cleanup");var page=fixture.Host.CurrentPage!;
        page.ActiveCanvas!.Editor.Rename("旧库保存仍阻断");
        using(var blocked=new FileStream(fixture.CanvasPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            fixture.FailCandidateCleanup=true;
            await fixture.Host.SwitchToContainerAsync(fixture.Second.ContainerPath);
            var candidate=fixture.Pages[1];fixture.Pages.Remove(candidate);
            try
            {
                Assert.AreSame(page,fixture.Host.CurrentPage);Assert.IsTrue(page.ViewModel.IsReady);await AssertLeaseHeld(fixture.First);await AssertLeaseAvailable(fixture.Second,false);
                Assert.IsTrue(candidate.HasStartedDisposal);Assert.IsTrue(PageDisposeTask(candidate)!.IsFaulted);
                await fixture.Host.SwitchToContainerAsync(fixture.First.ContainerPath);Assert.AreSame(page,fixture.Host.CurrentPage);
            }
            finally
            {
                foreach(var name in new[]{"_repository","_inspirationTray"})
                    await ((IAsyncDisposable)typeof(AssetLibraryViewModel).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(candidate.ViewModel)!).DisposeAsync();
            }
            await AssertLeaseAvailable(fixture.Second);
            await Record("CANDIDATE_CLEANUP_FAULT",new{pid=Environment.ProcessId,oldWorkspaceRetained=true,candidateCleanupFaultObserved=true,candidateLeaseReleased=true,switchGateReusable=true,scope="Additional injected irreversible candidate drain failure; test-only repository teardown, not a production forced-discard mechanism"});
        }
        await SaveButton(page.ActiveCanvas!);fixture.FailCandidateCleanup=false;await fixture.Host.SwitchToContainerAsync(fixture.Second.ContainerPath);Assert.AreEqual(fixture.Second.LibraryId,fixture.Host.CurrentDescriptor?.LibraryId);
    });
    [TestMethod]
    public Task IndependentProcessReopensRecoveredCanvasAndBothLibraryLeases()=>RunSta(async()=>
    {
        EnsureTestApplication();using var receipt=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root,"INDEPENDENT_REOPEN.json")));var expected=receipt.RootElement;
        Assert.AreNotEqual(expected.GetProperty("pid").GetInt32(),Environment.ProcessId);
        var store=new CanvasDocumentStore(expected.GetProperty("canvasRoot").GetString()!);var document=(await store.LoadAsync(expected.GetProperty("canvasId").GetGuid()))!;
        Assert.AreEqual(expected.GetProperty("name").GetString(),document.Name);Assert.AreEqual("恢复测试文字",document.Objects.Single().Text);
        var directory=expected.GetProperty("root").GetString()!;var containers=new AssetLibraryContainerService();
        var first=await containers.OpenAsync(expected.GetProperty("first").GetString()!);var second=await containers.OpenAsync(expected.GetProperty("second").GetString()!);
        var settings=new AssetLibraryPortableSettings();settings.RecordOpened(first.LibraryId,first.DisplayName,first.ContainerPath,DateTimeOffset.UtcNow);
        var host=new AssetLibraryWorkspaceHost(Path.Combine(directory,"legacy.db"),path=>new AssetLibraryPage(path,new TaskOperationBridge(),[],productDatabasePath:Path.Combine(directory,"product.db"),onlineSelectionWorkspaceFile:Path.Combine(directory,"online.json"),inspirationTrayDatabasePath:Path.Combine(directory,"tray.db")),settings);
        try
        {
            await host.InitializeForProductHarnessAsync();await host.CurrentPage!.ShowCanvasAsync(document);Assert.AreEqual(document.Name,host.CurrentPage.ActiveCanvas!.Editor.Document.Name);
            await AssertLeaseHeld(first);await host.SwitchToContainerAsync(second.ContainerPath);Assert.AreEqual(second.LibraryId,host.CurrentDescriptor?.LibraryId);
        }
        finally{await host.DisposeAsync();}
        await AssertLeaseAvailable(first);await AssertLeaseAvailable(second);
        await Record("INDEPENDENT_RESULT",new{pid=Environment.ProcessId,priorPid=expected.GetProperty("pid").GetInt32(),exactRecoveredName=true,canvasId=document.CanvasId,bothLeasesReleased=true});
    });
    private sealed class Fixture:IAsyncDisposable
    {
        public required string Directory {get;init;}
        public required AssetLibraryWorkspaceHost Host {get;init;}
        public required AssetLibraryContainerDescriptor First {get;init;}
        public required AssetLibraryContainerDescriptor Second {get;init;}
        public required CanvasDocumentStore Store {get;init;}
        public required string CanvasPath {get;init;}
        public required List<AssetLibraryPage> Pages {get;init;}
        public bool FailCandidateCleanup {get;set;}
        public static async Task<Fixture> Create(string scenario)
        {
            var directory=Path.Combine(Root,scenario+"-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(directory);
            var containers=new AssetLibraryContainerService();var first=await containers.CreateAsync(Path.Combine(directory,"A.ptlibrary"),"恢复A库");var second=await containers.CreateAsync(Path.Combine(directory,"B.ptlibrary"),"恢复B库");
            var pages=new List<AssetLibraryPage>();var settings=new AssetLibraryPortableSettings();settings.RecordOpened(second.LibraryId,second.DisplayName,second.ContainerPath,DateTimeOffset.UtcNow.AddMinutes(-1));settings.RecordOpened(first.LibraryId,first.DisplayName,first.ContainerPath,DateTimeOffset.UtcNow);
            Fixture? fixture=null;
            var host=new AssetLibraryWorkspaceHost(Path.Combine(directory,"legacy.db"),path=>
            {
                var page=new AssetLibraryPage(path,new TaskOperationBridge(),[],productDatabasePath:Path.Combine(directory,"product.db"),onlineSelectionWorkspaceFile:Path.Combine(directory,"online.json"),inspirationTrayDatabasePath:Path.Combine(directory,"tray.db"));pages.Add(page);
                if(fixture?.FailCandidateCleanup==true)
                {
                    var failed=page.ViewModel.RunTrackedP3OperationAsync(()=>Task.FromException(new IOException("Injected candidate cleanup failure")));
                    var pending=(HashSet<Task>)typeof(AssetLibraryViewModel).GetField("_p3Operations",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page.ViewModel)!;
                    pending.Add(failed);
                }
                return page;
            },settings);
            var database=new PixelTartDatabase(Path.Combine(directory,"product.db"));
            Assert.IsTrue((await new DatabaseMigrator(database,new DatabaseBackupService(database,Path.Combine(directory,"backups"))).MigrateAsync()).Success);
            await host.InitializeForProductHarnessAsync();var store=new CanvasDocumentStore(Path.Combine(directory,"canvases"));
            var document=new CanvasDocument{StorageRoot=store.DirectoryPath,PixelWidth=160,PixelHeight=120,Name="已保存初版"};await host.CurrentPage!.ShowCanvasAsync(document);
            host.CurrentPage.ActiveCanvas!.Editor.AddText(10,10,"恢复测试文字");await SaveButton(host.CurrentPage.ActiveCanvas);
            fixture=new(){Directory=directory,Host=host,First=first,Second=second,Store=store,CanvasPath=Path.Combine(store.DirectoryPath,document.CanvasId.ToString("N")+".json"),Pages=pages};return fixture;
        }
        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            foreach(var page in Pages)await page.DisposeAsync();
        }
    }
}
