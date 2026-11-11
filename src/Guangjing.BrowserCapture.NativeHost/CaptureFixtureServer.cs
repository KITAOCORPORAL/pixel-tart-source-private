using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
/// <summary>Explicit development-only static fixture mode; never used for desktop capture IPC.</summary>
internal static class CaptureFixtureServer
{
    private static readonly SemaphoreSlim AuditGate=new(1,1);
    private sealed record Route(string Path,string File,string? ContentType=null,int Status=200,int DelayMs=0,string? AllowFlag=null);
    public static async Task RunAsync(string root,int port)
    {
        if(!File.Exists(System.IO.Path.Combine(root,"FIXTURE_IDENTITY.json")))throw new InvalidDataException("Synthetic fixture identity is missing.");
        var listener=new TcpListener(IPAddress.Loopback,port);listener.Start();
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root,"SERVER_READY.runtime.json"),JsonSerializer.Serialize(new{port,pid=Environment.ProcessId,mode="SyntheticStaticFixturesOnly"}));
        while(true){var client=await listener.AcceptTcpClientAsync();_=Task.Run(()=>Handle(client,root));}
    }
    private static async Task Handle(TcpClient client,string root)
    {
        using(client)try{
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));var stream=client.GetStream();
            using var reader=new StreamReader(stream,Encoding.ASCII,false,1024,leaveOpen:true);var line=await reader.ReadLineAsync(timeout.Token);if(line is null)return;
            var parts=line.Split(' ');var length=line.Length;string? header;
            while(!string.IsNullOrEmpty(header=await reader.ReadLineAsync(timeout.Token))){length+=header.Length;if(length>16384)return;}
            var method=parts[0];var path=parts.Length>1?parts[1].Split('?')[0]:"";var name=path.TrimStart('/');
            var allowed=new[]{"ordinary.html","srcset.html","unavailable.html","ordinary-640.jpg","srcset-original-1600.jpg","srcset-thumb-320.jpg","unavailable-thumb.jpg","FIXTURE_IDENTITY.json"};
            Route? route=null;var config=System.IO.Path.Combine(root,"FIXTURE_ROUTES.json");
            if(System.IO.File.Exists(config)){
                using var doc=JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(config,timeout.Token));
                if(doc.RootElement.TryGetProperty("routes",out var routes))foreach(var entry in routes.EnumerateArray()){
                    var candidate=entry.Deserialize<Route>(new JsonSerializerOptions(JsonSerializerDefaults.Web));if(candidate?.Path==path){route=candidate;break;}
                }
            }
            var status=method is not("GET"or"HEAD")?405:route?.Status??(path=="/denied-original-2400.jpg"?403:allowed.Contains(name)&&System.IO.File.Exists(System.IO.Path.Combine(root,name))?200:404);
            if(route is not null){
                var target=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,route.File));var prefixRoot=System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root))+System.IO.Path.DirectorySeparatorChar;
                if(!target.StartsWith(prefixRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Fixture path escaped its root.");name=route.File;
                if(route.AllowFlag is string flag){
                    var control=System.IO.Path.Combine(root,"FIXTURE_CONTROL.runtime.json");var permitted=false;
                    if(System.IO.File.Exists(control)){using var doc=JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(control,timeout.Token));permitted=doc.RootElement.TryGetProperty(flag,out var value)&&value.ValueKind==JsonValueKind.True;}
                    if(!permitted)status=403;
                }
                if(route.DelayMs>0)await Task.Delay(Math.Min(route.DelayMs,8000),timeout.Token);
                if(status==200&&!System.IO.File.Exists(target))status=404;
            }
            var body=status==200?await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(root,name),timeout.Token):Encoding.UTF8.GetBytes("Synthetic fixture original unavailable ("+status+")");
            var type=route?.ContentType??(name.EndsWith(".jpg")?"image/jpeg":name.EndsWith(".png")?"image/png":name.EndsWith(".json")?"application/json":"text/html; charset=utf-8");
            var prefix=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Fixture\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");await stream.WriteAsync(prefix,timeout.Token);if(method!="HEAD")await stream.WriteAsync(body,timeout.Token);
            await AuditGate.WaitAsync();try{await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(root,"requests.runtime.jsonl"),JsonSerializer.Serialize(new{timeUtc=DateTimeOffset.UtcNow,method,path,status,bytes=body.Length,delayMs=route?.DelayMs??0})+"\n");}finally{AuditGate.Release();}
        }catch(Exception e)when(e is IOException or OperationCanceledException or ArgumentException or JsonException){}
    }
}
