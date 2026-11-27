using System.Net;
using System.Text.Json;
using PlanningReview;

var builder = WebApplication.CreateBuilder(args);
string setting(string name) => builder.Configuration[name] ?? throw new InvalidOperationException("Missing " + name);
var root = Path.GetFullPath(setting("data")); Directory.CreateDirectory(root);
using var lease = new FileStream(Path.Combine(root,"host.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
var address = IPAddress.Parse(setting("lan")); var listenPort = int.Parse(setting("port")); var project = Guid.Parse(setting("project"));
if(builder.Configuration["copy"] is string source && !File.Exists(Path.Combine(root,"notebook.json"))) NativePlan.StageCopy(source,Path.Combine(root,"isolated"),project);
var tls = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(setting("cert"),null);
builder.WebHost.ConfigureKestrel(options=> { options.Limits.MaxRequestBodySize=7400000; options.AddServerHeader=false; options.Listen(IPAddress.Loopback,listenPort,o=>o.UseHttps(tls)); if(!IPAddress.IsLoopback(address)) options.Listen(address,listenPort,o=>o.UseHttps(tls)); });
builder.Logging.ClearProviders();builder.Logging.AddSimpleConsole();builder.Logging.SetMinimumLevel(LogLevel.Warning);
var workbench = string.Equals(builder.Configuration["workbench"],"true",StringComparison.OrdinalIgnoreCase);
var room=await Room.Load(root,project,workbench); var app=builder.Build();
app.Use(async(ctx,next)=>
{
    ctx.Response.Headers.CacheControl="no-store";ctx.Response.Headers.XContentTypeOptions="nosniff";ctx.Response.Headers["Referrer-Policy"]="no-referrer";
    ctx.Response.Headers.ContentSecurityPolicy="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    try { if(ctx.Request.Method=="POST" && ctx.Request.Headers.Origin.Count>0 && ctx.Request.Headers.Origin.ToString()!=$"https://{ctx.Request.Host}") throw new Problem(403,"不同来源的请求已拒绝"); await next(); }
    catch(Problem error){ctx.Response.StatusCode=error.Status;await ctx.Response.WriteAsJsonAsync(new{error=error.Message});}
    catch(Exception error) when(error is JsonException or BadHttpRequestException or ArgumentException){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{error="请求格式不正确"});}
});
if(workbench) app.Use(async(ctx,next)=>{if(ctx.Request.Path=="/"){ctx.Response.Redirect("/workbench.html");return;}await next();});
app.UseDefaultFiles();app.UseStaticFiles();
string? token(HttpContext ctx) => ctx.Request.Headers.Authorization.ToString() is string header && header.StartsWith("Bearer ",StringComparison.Ordinal) ? header[7..] : null;
app.MapPost("/api/join",async(Join request)=>await room.Join(request.Code));
app.MapGet("/api/state",async(HttpContext ctx)=>await ctx.Response.WriteAsJsonAsync(await room.View(token(ctx))));
app.MapPost("/api/presence",async(HttpContext ctx,Pulse request)=>await room.Pulse(token(ctx),request.Device,request.Target));
app.MapPost("/api/op",async(HttpContext ctx)=>await ctx.Response.WriteAsJsonAsync(await room.Apply(token(ctx),await JsonSerializer.DeserializeAsync<Edit>(ctx.Request.Body,Rules.Json)??throw new Problem(400,"操作为空"))));
app.MapGet("/api/image/{id:guid}",async(HttpContext ctx,Guid id)=> {var image=await room.Image(token(ctx),id);return Results.Stream(image.stream,image.mime);});
if(workbench) app.MapGet("/api/workbench/docx",async(HttpContext ctx)=>await Results.File(await room.Office(token(ctx)),WorkbenchOffice.Mime,"planning-review.docx").ExecuteAsync(ctx));
app.MapPost("/api/export",async(HttpContext ctx)=>await ctx.Response.WriteAsJsonAsync(new {folder=await room.Export(token(ctx))}));
app.MapGet("/health",()=>new {app="Pixel Tart · 策划审稿", version=1});
NativePlan.Atomic(Path.Combine(root,"owner.local"),System.Text.Encoding.UTF8.GetBytes(room.OwnerKey));
await app.RunAsync();
record Join(string Code);
record Pulse(Guid Device, Guid? Target);
