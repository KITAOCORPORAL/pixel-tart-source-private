using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Bookings;

namespace Guangjing.Schedule.Server;
public partial class Program
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web) { Converters={new JsonStringEnumConverter()} };
    public static async Task Main(string[] args)
    {
        var builder=WebApplication.CreateBuilder(args);
        var root=builder.Configuration["Schedule:Root"] ?? throw new InvalidOperationException("Explicit Schedule:Root required.");
        root=Path.GetFullPath(root); Directory.CreateDirectory(root);
        var dbPath=builder.Configuration["Schedule:Database"] ?? Path.Combine(root,"Data","pixel-tart.db");
        if (!Path.GetFullPath(dbPath).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Calendar database must stay within the explicit runtime root.");
        var ownerFile=builder.Configuration["Schedule:OwnerKeyFile"] ?? throw new InvalidOperationException("Owner key file required.");
        var ownerKey=(await File.ReadAllTextAsync(ownerFile)).Trim();if(ownerKey.Length<48)throw new InvalidOperationException("Invalid owner key.");
        var publicBase=(builder.Configuration["Schedule:PublicBaseUrl"]??"http://127.0.0.1:17661").TrimEnd('/');
        if(!Uri.TryCreate(publicBase,UriKind.Absolute,out var address)||address.AbsolutePath!="/"||(address.Scheme!="https"&&!(address.Scheme=="http"&&address.IsLoopback))||address.UserInfo!=""||address.Query!=""||address.Fragment!="")throw new InvalidOperationException("HTTPS or loopback test origin required.");
        builder.WebHost.ConfigureKestrel(k=>{k.Listen(IPAddress.Loopback,int.Parse(builder.Configuration["Schedule:Port"]??"17661"));k.Limits.MaxRequestBodySize=16*1024;});
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        var database=new PixelTartDatabase(dbPath);
        var migration=await new DatabaseMigrator(database,new DatabaseBackupService(database,Path.Combine(root,"Backups"))).MigrateAsync();
        if(!migration.Success)throw new InvalidOperationException("Calendar database migration failed.");
        var repository=new SqliteShootBookingRepository(database);var service=new ShootBookingService(repository,new BookingConflictDetector(repository));
        var auth=new DeviceAuthorization(database,ownerKey);
        builder.Services.AddSingleton(database);builder.Services.AddSingleton(repository);builder.Services.AddSingleton(service);builder.Services.AddSingleton(auth);
        var app=builder.Build();var auditGate=new SemaphoreSlim(1,1);var mutationGate=new SemaphoreSlim(1,1);
        app.Use(async(c,next)=>
        {
            c.Response.Headers.CacheControl="no-store";c.Response.Headers["Referrer-Policy"]="no-referrer";
            c.Response.Headers["X-Content-Type-Options"]="nosniff";
            c.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
            try{await next(c);}
            catch(ScheduleProblem e){c.Response.StatusCode=e.Status;await c.Response.WriteAsJsonAsync(new {code=e.Code,message=e.Message});}
            catch(BadHttpRequestException){c.Response.StatusCode=400;await c.Response.WriteAsJsonAsync(new{code="InvalidRequest",message="请求格式无效。"});}
            catch(OperationCanceledException){if(!c.Response.HasStarted)c.Response.StatusCode=499;}
            catch(Exception){if(!c.Response.HasStarted){c.Response.StatusCode=500;await c.Response.WriteAsJsonAsync(new{code="ServiceUnavailable",message="保存未得到服务确认，请保留输入并使用同一请求重试。"});}}
            finally{await auditGate.WaitAsync();try{await File.AppendAllTextAsync(Path.Combine(root,"schedule-requests.jsonl"),JsonSerializer.Serialize(new{timeUtc=DateTimeOffset.UtcNow,method=c.Request.Method,path=c.Request.Path.Value,status=c.Response.StatusCode,traceId=c.TraceIdentifier})+"\n");}finally{auditGate.Release();}}
        });
        app.MapGet("/health/ready",()=>Results.Ok(new{ready=true,mode="MobileScheduleV1",source="existing calendar SQLite"}));
        app.MapPost("/v1/schedule/devices",async(DeviceRequest body,HttpRequest req,CancellationToken ct)=>{auth.Owner(req);return Results.Ok(await auth.Create(body,req.Headers["Idempotency-Key"].ToString(),publicBase,ct));});
        app.MapDelete("/v1/schedule/devices/{id}",async(string id,HttpRequest req,CancellationToken ct)=>{auth.Owner(req);await auth.Revoke(id,ct);return Results.Ok(new{revoked=true});});
        app.MapGet("/v1/schedule/session",async(HttpRequest req,CancellationToken ct)=>Results.Ok(new{device=await auth.Require(req,ct),timeZone=TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id,out var localIana)?localIana:TimeZoneInfo.Local.Id}));
        app.MapGet("/v1/schedule/bookings",async(DateTimeOffset start,DateTimeOffset end,HttpRequest req,CancellationToken ct)=>
        {
            await auth.Require(req,ct);if(end<=start||end-start>TimeSpan.FromDays(32))throw new ScheduleProblem(400,"InvalidRange","请选择32天以内的日期范围。");
            var summaries=await service.QueryCurrentViewAsync(new(start,end),ct);var items=new List<object>();
            foreach(var b in summaries) {var booking=await service.GetAsync(b.Id,cancellationToken:ct);if(booking is not null)items.Add(View(booking));}
            return Results.Ok(new{items,start,end});
        });
        app.MapGet("/v1/schedule/bookings/{id:guid}",async(Guid id,HttpRequest req,CancellationToken ct)=>
        {await auth.Require(req,ct);var b=await service.GetAsync(id,cancellationToken:ct)??throw new ScheduleProblem(404,"BookingNotFound","预约不存在或已归档。");return Results.Ok(View(b));});
        app.MapPut("/v1/schedule/bookings/{id:guid}",async(Guid id,ScheduleUpdate body,HttpRequest req,CancellationToken ct)=>
        {
            var device=await auth.Require(req,ct);
            if(body.ExpectedRevision<1||!Guid.TryParse(body.OperationId,out var operation)||!Enum.IsDefined(body.Status)||!Enum.IsDefined(body.ConflictResolution))throw new ScheduleProblem(400,"InvalidMutation","需要预约版本、状态和稳定修改 Id。");
            var hash=DeviceAuthorization.Hash(JsonSerializer.Serialize(new{id,request=body},Json));
            var operationId="schedule:"+device.Id+":"+operation.ToString("N");
            await mutationGate.WaitAsync(ct);
            try
            {
                ShootBooking? replay;
                try{replay=await repository.GetMutationAsync(operationId,id,hash,ct);}catch(BookingOperationConflictException e){throw new ScheduleProblem(409,"OperationIdentityConflict",e.Message);}
                if(replay is not null)return Results.Ok(new{booking=View(replay),receipt=new ScheduleReceipt(id,replay.Revision,replay.UpdatedAtUtc,body.OperationId),replayed=true});
                var current=await service.GetAsync(id,cancellationToken:ct)??throw new ScheduleProblem(404,"BookingNotFound","预约不存在或已归档。");
                var draft=BookingDraftFactory.From(current,await service.GetRequirementsAsync(id,ct)) with {StartAt=body.StartAtUtc,EndAt=body.EndAtUtc,Status=body.Status,ExpectedRevision=body.ExpectedRevision,MutationId=operationId,MutationRequestHash=hash,DeviceId="mobile:"+device.Id};
                var result=await service.SaveAsync(draft,body.ConflictResolution,ct);
                if(result.Status==BookingSaveStatus.VersionConflict)throw new ScheduleProblem(409,"RevisionConflict","电脑或另一端已经更新，请刷新后重新编辑。当前输入尚未覆盖新版本。");
                if(result.Status==BookingSaveStatus.NeedsAttention)return Results.Json(new{code="TimeConflict",message="检测到时间冲突，请刷新或明确选择冲突处理方式。",conflicts=result.Conflicts},statusCode:409);
                if(result.Booking is null||result.Status!=BookingSaveStatus.Saved)throw new ScheduleProblem(400,"ValidationFailed",string.Join("；",result.ValidationErrors));
                return Results.Ok(new{booking=View(result.Booking),receipt=new ScheduleReceipt(id,result.Booking.Revision,result.Booking.UpdatedAtUtc,body.OperationId),replayed=false});
            }finally{mutationGate.Release();}
        });
        app.MapGet("/calendar",()=>Results.File(Path.Combine(AppContext.BaseDirectory,"wwwroot","calendar.html"),"text/html; charset=utf-8"));
        app.MapGet("/calendar-client.js",()=>Results.File(Path.Combine(AppContext.BaseDirectory,"wwwroot","calendar-client.js"),"text/javascript"));
        app.MapGet("/calendar-client.css",()=>Results.File(Path.Combine(AppContext.BaseDirectory,"wwwroot","calendar-client.css"),"text/css"));
        await app.RunAsync();
    }
    private static object View(ShootBooking b)=>new {b.Id,b.Title,b.ClientDisplayName,b.StartAtUtc,b.EndAtUtc,b.TimeZoneId,b.IsAllDay,b.Status,b.Location,b.ShootingType,b.ShootingRequirements,b.PreparationNotes,b.Notes,b.Revision,b.UpdatedAtUtc,timeZoneIana=TimeZoneInfo.TryConvertWindowsIdToIanaId(b.TimeZoneId,out var iana)?iana:b.TimeZoneId};
}
