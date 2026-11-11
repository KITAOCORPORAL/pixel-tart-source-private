using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Utilities;

internal static class Program
{
    private static string Root="";private static HttpClient Client=null!;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    private static readonly List<object> Steps=[];
    [STAThread]public static int Main(string[] args)
    {
        Root=Path.GetFullPath(args[1]);Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME","1");Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT",Root);
        var dispatcher=Dispatcher.CurrentDispatcher;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));int exit=1;
        dispatcher.InvokeAsync(async()=>{try{Client=new HttpClient{BaseAddress=new Uri(args.Length>2?args[2]:"http://127.0.0.1:17661"),Timeout=TimeSpan.FromSeconds(20)};
            switch(args[0]){case "seed":await Seed();break;case "roundtrip":await Roundtrip();break;case "reopen":await Reopen();break;case "phone-device":await PhoneDevice();break;case "pc-adjust":await PcAdjust();break;case "pc-readback":await PcReadback();break;default:throw new ArgumentException("Unknown mode");}
            await File.WriteAllTextAsync(Path.Combine(Root,args[0]+"-receipt.json"),JsonSerializer.Serialize(new{status="PASS",pid=Environment.ProcessId,steps=Steps},Json));Console.WriteLine(args[0]+" PASS: "+Steps.Count+" checks");exit=0;
        }catch(Exception e){await File.WriteAllTextAsync(Path.Combine(Root,args[0]+"-failure.txt"),e.ToString());Console.Error.WriteLine(e.GetType().Name+": "+e.Message);}finally{Client?.Dispose();dispatcher.InvokeShutdown();}});Dispatcher.Run();return exit;
    }
    private static void Check(bool valid,string name){if(!valid)throw new InvalidOperationException(name);Steps.Add(new{check=name,pass=true});}
    private static async Task<(SqliteShootBookingRepository Repo,ShootBookingService Service)> Services()
    {
        var db=new PixelTartDatabase(AppDataPaths.DatabaseFile);var result=await new DatabaseMigrator(db,new DatabaseBackupService(db,Path.Combine(Root,"Backups"))).MigrateAsync();Check(result.Success,"existing calendar database migrated");var repo=new SqliteShootBookingRepository(db);return(repo,new(repo,new BookingConflictDetector(repo)));
    }
    private static async Task Seed()
    {
        var (repo,service)=await Services();var id=Guid.NewGuid();var zone=TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");var today=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone).Date;
        var start=new DateTimeOffset(DateTime.SpecifyKind(today.AddHours(10),DateTimeKind.Unspecified),zone.GetUtcOffset(today));
        var draft=new ShootBookingDraft{Id=id,CreateIfMissing=true,ExpectedRevision=0,MutationId=Guid.NewGuid().ToString(),Title="合成预约 · 手机档期测试",ClientDisplayName="合成客户",StartAt=start,EndAt=start.AddHours(1),TimeZoneId=zone.Id,Status=ShootBookingStatus.Tentative,ShootingType="Portrait",Location="合成摄影棚",Notes="仅测试预约，不含真实客户信息。"};
        var first=await service.SaveAsync(draft);Check(first.Booking?.Revision==1,"PC created synthetic booking through existing service");
        var again=await service.SaveAsync(draft);Check(again.Booking?.Revision==1,"create retry did not create or execute twice");
        var conflict=await service.SaveAsync(draft with{Id=Guid.NewGuid(),MutationId=Guid.NewGuid().ToString(),Title="合成冲突预约",StartAt=start.AddHours(4),EndAt=start.AddHours(5)});Check(conflict.Booking is not null,"synthetic conflict fixture prepared");
        var upcoming=await service.SaveAsync(draft with{Id=Guid.NewGuid(),MutationId=Guid.NewGuid().ToString(),Title="合成近期预约",StartAt=start.AddDays(2),EndAt=start.AddDays(2).AddHours(1)});Check(upcoming.Booking is not null,"upcoming fixture prepared");
        var keyPath=Path.Combine(Root,"schedule-owner.key");if(!File.Exists(keyPath))await File.WriteAllTextAsync(keyPath,Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        await File.WriteAllTextAsync(Path.Combine(Root,"identity.json"),JsonSerializer.Serialize(new{bookingId=id,conflictId=conflict.Booking!.Id,upcomingId=upcoming.Booking!.Id,startAtUtc=start.ToUniversalTime(),endAtUtc=start.AddHours(1).ToUniversalTime()},Json));
    }
    private static async Task<JsonElement> Http(HttpMethod method,string path,string? token=null,object? body=null,int expected=200,bool owner=false,string? operation=null)
    {
        using var request=new HttpRequestMessage(method,path);if(token is not null)request.Headers.Authorization=new("Bearer",token);if(owner)request.Headers.Add("X-Owner-Key",(await File.ReadAllTextAsync(Path.Combine(Root,"schedule-owner.key"))).Trim());if(operation is not null)request.Headers.Add("Idempotency-Key",operation);if(body is not null)request.Content=JsonContent.Create(body,options:Json);
        using var response=await Client.SendAsync(request);Steps.Add(new{method=method.Method,path,status=(int)response.StatusCode});var text=await response.Content.ReadAsStringAsync();if((int)response.StatusCode!=expected)throw new InvalidOperationException(method+" "+path+" expected "+expected+" received "+(int)response.StatusCode+" "+text);return text.StartsWith('{')?JsonDocument.Parse(text).RootElement.Clone():default;
    }
    private static async Task<string> Device(string file,string name)
    {
        var operation=Guid.NewGuid().ToString();var result=await Http(HttpMethod.Post,"/v1/schedule/devices",body:new{name,validHours=48},owner:true,operation:operation);var replay=await Http(HttpMethod.Post,"/v1/schedule/devices",body:new{name,validHours=48},owner:true,operation:operation);Check(result.GetProperty("url").GetString()==replay.GetProperty("url").GetString(),"device authorization retry returns same protected link");
        var url=result.GetProperty("url").GetString()!;await File.WriteAllTextAsync(Path.Combine(Root,file),url);return Uri.UnescapeDataString(new Uri(url).Fragment[7..]);
    }
    private static async Task Roundtrip()
    {
        var identity=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root,"identity.json"))).RootElement;var id=identity.GetProperty("bookingId").GetGuid();var path="/v1/schedule/bookings/"+id;
        var (repo,service)=await Services();var credential=await Device("AUTOMATION_URL.txt","HTTP 自动验收");
        await Http(HttpMethod.Get,path,expected:401);await Http(HttpMethod.Get,path,"bad-token",expected:401);
        var view=await Http(HttpMethod.Get,path,credential);var first=await service.GetAsync(id);Check(view.GetProperty("revision").GetInt64()==first!.Revision,"mobile reads exact existing PC calendar version");
        var stale=new ShootBookingEditorViewModel(service,new SqliteProjectRepository(new PixelTartDatabase(AppDataPaths.DatabaseFile)),id);await stale.InitializeAsync();
        var body=new{expectedRevision=first.Revision,operationId=Guid.NewGuid().ToString(),startAtUtc=first.StartAtUtc.AddHours(1),endAtUtc=first.EndAtUtc.AddHours(1),status="Confirmed"};
        var saved=await Http(HttpMethod.Put,path,credential,body);var replay=await Http(HttpMethod.Put,path,credential,body);Check(saved.GetProperty("receipt").GetProperty("revision").GetInt64()==replay.GetProperty("receipt").GetProperty("revision").GetInt64(),"mobile save retry returns same receipt without extra revision");
        var afterPhone=await service.GetAsync(id);Check(afterPhone!.StartAtUtc==body.startAtUtc&&afterPhone.Status==ShootBookingStatus.Confirmed,"PC service reads phone time and status from same calendar");
        await ((AsyncRelayCommand)stale.SaveCommand).ExecuteAsync(null);Check(stale.ValidationText.Contains("刷新"),"stale PC editor prompts refresh instead of overwriting phone update");Check((await service.GetAsync(id))!.Revision==afterPhone.Revision,"stale PC save preserved phone revision");
        await Http(HttpMethod.Put,path,credential,body with{operationId=Guid.NewGuid().ToString()},expected:409);
        await Http(HttpMethod.Put,path,credential,body with{status="Cancelled"},expected:409);
        var editor=new ShootBookingEditorViewModel(service,new SqliteProjectRepository(new PixelTartDatabase(AppDataPaths.DatabaseFile)),id);await editor.InitializeAsync();editor.StartTimeText="12:00";editor.EndTimeText="13:00";editor.SelectedStatus=editor.StatusOptions.First(s=>s.Value==ShootBookingStatus.Preparing);await ((AsyncRelayCommand)editor.SaveCommand).ExecuteAsync(null);Check(editor.ValidationText=="","actual PC editor save command succeeded");
        var afterPc=await service.GetAsync(id);Check(afterPc!.Revision==afterPhone.Revision+1&&afterPc.Status==ShootBookingStatus.Preparing,"PC edit incremented calendar version once");
        var mobile=await Http(HttpMethod.Get,path,credential);Check(mobile.GetProperty("revision").GetInt64()==afterPc.Revision&&mobile.GetProperty("status").GetString()=="Preparing","mobile refresh sees PC adjustment");
        var range=await Http(HttpMethod.Get,"/v1/schedule/bookings?start="+Uri.EscapeDataString(first.StartAtUtc.Date.ToString("O"))+"&end="+Uri.EscapeDataString(first.StartAtUtc.AddDays(14).ToString("O")),credential);Check(range.GetProperty("items").GetArrayLength()==3,"nearby calendar reads all three synthetic appointments");
        await Http(HttpMethod.Put,path,credential,new{expectedRevision=afterPc.Revision,operationId=Guid.NewGuid().ToString(),startAtUtc=afterPc.EndAtUtc,endAtUtc=afterPc.StartAtUtc,status="Confirmed"},expected:400);
        var conflictBooking=await service.GetAsync(identity.GetProperty("conflictId").GetGuid());var conflict=await Http(HttpMethod.Put,path,credential,new{expectedRevision=afterPc.Revision,operationId=Guid.NewGuid().ToString(),startAtUtc=conflictBooking!.StartAtUtc,endAtUtc=conflictBooking.EndAtUtc,status="Confirmed"},expected:409);Check(conflict.GetProperty("code").GetString()=="TimeConflict","existing conflict detector and prompt reused");
        Check((await service.GetAsync(id))!.Revision==afterPc.Revision,"failed and conflicting requests did not mutate calendar");
        await File.WriteAllTextAsync(Path.Combine(Root,"expected.json"),JsonSerializer.Serialize(afterPc,Json));
    }
    private static async Task Reopen()
    {
        var expected=JsonSerializer.Deserialize<ShootBooking>(await File.ReadAllTextAsync(Path.Combine(Root,"expected.json")),Json)!;var (repo,service)=await Services();var current=await service.GetAsync(expected.Id);Check(current!.Revision==expected.Revision&&current.StartAtUtc==expected.StartAtUtc&&current.Status==expected.Status,"new PC process restored bidirectional changes from existing SQLite");
        var url=await File.ReadAllTextAsync(Path.Combine(Root,"AUTOMATION_URL.txt"));var token=new Uri(url).Fragment[7..];var mobile=await Http(HttpMethod.Get,"/v1/schedule/bookings/"+expected.Id,token);Check(mobile.GetProperty("revision").GetInt64()==expected.Revision,"reopened mobile session reads same persisted revision");
    }
    private static async Task PcAdjust()
    {
        var identity=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root,"identity.json"))).RootElement;
        var (repo,service)=await Services();var id=identity.GetProperty("bookingId").GetGuid();
        var editor=new ShootBookingEditorViewModel(service,new SqliteProjectRepository(new PixelTartDatabase(AppDataPaths.DatabaseFile)),id);await editor.InitializeAsync();
        editor.StartTimeText="13:00";editor.EndTimeText="14:00";editor.SelectedStatus=editor.StatusOptions.First(s=>s.Value==ShootBookingStatus.Confirmed);
        await ((AsyncRelayCommand)editor.SaveCommand).ExecuteAsync(null);Check(editor.ValidationText=="","PC editor adjusted synthetic appointment for mobile reload");
        var current=await service.GetAsync(id);Check(current!.Status==ShootBookingStatus.Confirmed,"PC changed status persisted");
        await File.WriteAllTextAsync(Path.Combine(Root,"pc-adjust-result.json"),JsonSerializer.Serialize(new{current.Id,current.StartAtUtc,current.EndAtUtc,current.Status,current.Revision},Json));
    }
    private static async Task PcReadback()
    {
        var identity=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root,"identity.json"))).RootElement;
        var (repo,service)=await Services();var id=identity.GetProperty("bookingId").GetGuid();
        var calendar=new WorkCalendarViewModel(service,new SqliteProjectRepository(new PixelTartDatabase(AppDataPaths.DatabaseFile)));
        await calendar.InitializeAsync();await calendar.OpenBookingDetailsAsync(id);await ((AsyncRelayCommand)calendar.RefreshCommand).ExecuteAsync(null);
        var b=calendar.Details.Booking;Check(b is not null,"existing PC calendar refreshed selected appointment details");
        await File.WriteAllTextAsync(Path.Combine(Root,"pc-readback.json"),JsonSerializer.Serialize(new{b!.Id,b.Title,b.StartAtUtc,b.EndAtUtc,b.Status,b.Revision},Json));
    }
    private static async Task PhoneDevice(){await Services();await Device("PHONE_URL.txt","iPhone 13 Safari 合成档期测试");}
}
