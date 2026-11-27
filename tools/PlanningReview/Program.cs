using System.Net;
using System.Text.Json;
using PlanningReview;

var builder = WebApplication.CreateBuilder(args);
string setting(string name) => builder.Configuration[name] ?? throw new InvalidOperationException("Missing " + name);
var root = Path.GetFullPath(setting("data"));
RAWSelectionAssistant.Core.Services.Projects.PlanningImageStorage.RejectLinks(root, Path.Combine(root, "host.lock"));
Directory.CreateDirectory(root);
using var lease = new FileStream(Path.Combine(root,"host.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
var address = IPAddress.Parse(setting("lan"));
var listenPort = int.Parse(setting("port"));
var candidate = builder.Configuration["share-destination-id"] is not null;
if (candidate && (builder.Configuration["project"] is not null || builder.Configuration["copy"] is not null))
    throw new InvalidOperationException("Share candidate does not load the legacy single-project instance.");
if (!candidate && File.Exists(Path.Combine(root, ShareRegistry.MarkerFile)))
    throw new InvalidOperationException("Legacy review cannot use share candidate data.");
var share = candidate ? new ShareRegistry(root, Guid.Parse(setting("share-destination-id")),
    setting("share-public-base"), setting("share-control-secret-file"), listenPort) : null;
var project = candidate ? Guid.Empty : Guid.Parse(setting("project"));
if(!candidate && builder.Configuration["copy"] is string source && !File.Exists(Path.Combine(root,"notebook.json"))) NativePlan.StageCopy(source,Path.Combine(root,"isolated"),project);
var tls = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(setting("cert"),null);
builder.WebHost.ConfigureKestrel(options=> { options.Limits.MaxRequestBodySize=7400000; options.AddServerHeader=false; options.Listen(IPAddress.Loopback,listenPort,o=>o.UseHttps(tls)); if(!IPAddress.IsLoopback(address)) options.Listen(address,listenPort,o=>o.UseHttps(tls)); });
builder.Logging.ClearProviders();builder.Logging.AddSimpleConsole();builder.Logging.SetMinimumLevel(LogLevel.Warning);
var workbench = candidate || string.Equals(builder.Configuration["workbench"],"true",StringComparison.OrdinalIgnoreCase);
var legacyRoom = candidate ? null : await Room.Load(root,project,workbench);
var app=builder.Build();
app.Use(async(ctx,next)=>
{
    ctx.Response.Headers.CacheControl="no-store";ctx.Response.Headers.XContentTypeOptions="nosniff";ctx.Response.Headers["Referrer-Policy"]="no-referrer";
    ctx.Response.Headers.ContentSecurityPolicy="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    try { if((ctx.Request.Method=="POST" || ctx.Request.Method=="PUT") && ctx.Request.Headers.Origin.Count>0 && ctx.Request.Headers.Origin.ToString()!=$"https://{ctx.Request.Host}") throw new Problem(403,"不同来源的请求已拒绝"); await next(); }
    catch(Problem error){ctx.Response.StatusCode=error.Status;await ctx.Response.WriteAsJsonAsync(new{error=error.Message});}
    catch(BadHttpRequestException error) when(candidate){ctx.Response.StatusCode=error.StatusCode;await ctx.Response.WriteAsJsonAsync(new{error="请求格式或大小不正确"});}
    catch(Exception error) when(error is JsonException or BadHttpRequestException or ArgumentException){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{error="请求格式不正确"});}
});
string? token(HttpContext ctx) => ctx.Request.Headers.Authorization.ToString() is string header && header.StartsWith("Bearer ",StringComparison.Ordinal) ? header[7..] : null;
if (share != null) app.Use((ctx, next) => share.Scope(ctx, next, token(ctx)));
if(workbench) app.Use(async(ctx,next)=>{if(ctx.Request.Path=="/"){ctx.Response.Redirect(ctx.Request.PathBase.Add("/workbench.html"));return;}await next();});
app.UseDefaultFiles();app.UseStaticFiles();
app.UseRouting();
Room room(HttpContext ctx) => legacyRoom ?? ShareRegistry.RequestRoom(ctx) ?? throw new Problem(404,"策划入口不存在。");
app.MapPost("/api/join",async(HttpContext ctx, Join request)=>await room(ctx).Join(request.Code, candidate, ShareRegistry.IsLoopback(ctx)));
app.MapGet("/api/state",async(HttpContext ctx)=>await ctx.Response.WriteAsJsonAsync(await room(ctx).View(token(ctx), ShareRegistry.RequestMemberUrl(ctx))));
app.MapPost("/api/presence",async(HttpContext ctx,Pulse request)=>await room(ctx).Pulse(token(ctx),request.Device,request.Target));
app.MapPost("/api/op",async(HttpContext ctx)=>await ctx.Response.WriteAsJsonAsync(await room(ctx).Apply(token(ctx),await JsonSerializer.DeserializeAsync<Edit>(ctx.Request.Body,Rules.Json)??throw new Problem(400,"操作为空"))));
app.MapGet("/api/image/{id:guid}",async(HttpContext ctx,Guid id)=> {var image=await room(ctx).Image(token(ctx),id);return Results.Stream(image.stream,image.mime);});
if(workbench) app.MapGet("/api/workbench/docx",async(HttpContext ctx)=>await Results.File(await room(ctx).Office(token(ctx)),WorkbenchOffice.Mime,"planning-review.docx").ExecuteAsync(ctx));
app.MapPost("/api/export",async(HttpContext ctx)=>await ctx.Response.WriteAsJsonAsync(new {folder=await room(ctx).Export(token(ctx))}));
if (share == null)
{
    app.MapGet("/health",()=>new {app="Pixel Tart · 策划审稿", version=1});
    NativePlan.Atomic(Path.Combine(root,"owner.local"),System.Text.Encoding.UTF8.GetBytes(legacyRoom!.OwnerKey));
}
else
{
    app.MapGet("/health",()=>new {app="Guangjing · 策划协作候选", version=1, mode="planning-share-candidate", destinationId=share.DestinationId});
    share.Map(app);
}
await app.RunAsync();
record Join(string? Code);
record Pulse(Guid Device, Guid? Target);
