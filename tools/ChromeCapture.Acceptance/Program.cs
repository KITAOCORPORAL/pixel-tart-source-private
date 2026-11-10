using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Automation;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Utilities;
if(args[0]=="seed")
{
    var runtime=Path.GetFullPath(args[1]);if(Directory.Exists(runtime))throw new IOException("Preserve previous isolated root");
    Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME","1");Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT",runtime);
    var database=new PixelTartDatabase(AppDataPaths.DatabaseFile);var migration=await new DatabaseMigrator(database,new DatabaseBackupService(database,Path.Combine(runtime,"Backups"))).MigrateAsync();if(!migration.Success)throw new IOException(migration.ErrorMessage);
    var library=await new AssetLibraryContainerService().CreateAsync(Path.Combine(runtime,"Capture-Test.ptlibrary"),"浏览器采集测试库");
    var second=await new AssetLibraryContainerService().CreateAsync(Path.Combine(runtime,"Capture-Second.ptlibrary"),"第二测试库");
    var a=new PhotoProjectRecord{Name="采集项目 A"};var b=new PhotoProjectRecord{Name="采集项目 B"};var invalid=new PhotoProjectRecord{Name="待失效测试项目"};var repository=new SqliteProjectRepository(database);
    foreach(var item in new[]{a,b,invalid})await repository.UpsertAsync(item);
    await using var tray=new SqliteInspirationTrayService(Path.Combine(runtime,"InspirationTray","tray.sqlite"));var board=await tray.CreateCollectionAsync("采集灵感板 A",a.Id);
    var settings=new AppSettings{LastPrimaryPage="AssetLibrary"};settings.AssetLibraryPortable.RecordOpened(second.LibraryId,second.DisplayName,second.ContainerPath,DateTimeOffset.UtcNow);settings.AssetLibraryPortable.RecordOpened(library.LibraryId,library.DisplayName,library.ContainerPath,DateTimeOffset.UtcNow);settings.AssetLibraryPortable.CurrentContainerPath=library.ContainerPath;
    Directory.CreateDirectory(runtime);await File.WriteAllTextAsync(Path.Combine(runtime,"settings.json"),JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
    await File.WriteAllTextAsync(args[2],JsonSerializer.Serialize(new{runtime,library,second,projectA=a.Id,projectB=b.Id,invalidProject=invalid.Id,board=board.CollectionId},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Isolated synthetic capture destinations prepared");return;
}
if(args[0]=="ui")
{
    var pid=int.Parse(args[1]);using var app=Process.GetProcessById(pid);var until=DateTime.UtcNow.AddSeconds(35);AutomationElement? root=null;
    while(DateTime.UtcNow<until){app.Refresh();if(app.MainWindowHandle!=IntPtr.Zero){root=AutomationElement.FromHandle(app.MainWindowHandle);if(root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"AssetLibraryNavigationButton"))!=null)break;}await Task.Delay(100);}
    if(root==null)throw new IOException("Desktop window unavailable");var source=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"OpenCapturedSourcePage"));
    if(args.Length>3&&args[3]=="open-source"){if(source==null)throw new IOException("Visible original-page action not found");((InvokePattern)source.GetCurrentPattern(InvokePattern.Pattern)).Invoke();await Task.Delay(300);}
    var info=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"AssetInspectorSourcePath"));
    await File.WriteAllTextAsync(args[2],JsonSerializer.Serialize(new{pid,title=root.Current.Name,sourceActionExists=source!=null,selectedSourcePath=info?.Current.Name,operation=args.Length>3?args[3]:"inspect",scope="Native UIA published full desktop; not Chrome context-menu evidence"},new JsonSerializerOptions{WriteIndented=true}));return;
}
if(args[0]=="close")
{
    using var app=Process.GetProcessById(int.Parse(args[1]));var root=AutomationElement.FromHandle(app.MainWindowHandle);((WindowPattern)root.GetCurrentPattern(WindowPattern.Pattern)).Close();if(!app.WaitForExit(30000))throw new TimeoutException("Desktop close");Console.WriteLine("Native desktop window closed normally");return;
}
throw new ArgumentException("seed | ui | close");
