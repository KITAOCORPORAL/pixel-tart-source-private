using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
/// <summary>Explicit development-only static fixture mode; never used for desktop capture IPC.</summary>
internal static class CaptureFixtureServer
{
    public static async Task RunAsync(string root,int port)
    {
        if(!File.Exists(Path.Combine(root,"FIXTURE_IDENTITY.json")))throw new InvalidDataException("Synthetic fixture identity is missing.");
        var listener=new TcpListener(IPAddress.Loopback,port);listener.Start();
        await File.WriteAllTextAsync(Path.Combine(root,"SERVER_READY.runtime.json"),JsonSerializer.Serialize(new{port,pid=Environment.ProcessId,mode="SyntheticStaticFixturesOnly"}));
        while(true){var client=await listener.AcceptTcpClientAsync();_=Task.Run(()=>Handle(client,root));}
    }
    private static async Task Handle(TcpClient client,string root)
    {
        using(client)try{
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));var stream=client.GetStream();
            using var reader=new StreamReader(stream,Encoding.ASCII,false,1024,leaveOpen:true);var line=await reader.ReadLineAsync(timeout.Token);if(line is null)return;
            var parts=line.Split(' ');var length=line.Length;string? header;
            while(!string.IsNullOrEmpty(header=await reader.ReadLineAsync(timeout.Token))){length+=header.Length;if(length>16384)return;}
            var method=parts[0];var path=parts.Length>1?parts[1].Split('?')[0]:"";var name=path.TrimStart('/');
            var allowed=new[]{"ordinary.html","srcset.html","unavailable.html","ordinary-640.jpg","srcset-original-1600.jpg","srcset-thumb-320.jpg","unavailable-thumb.jpg","FIXTURE_IDENTITY.json"};
            var status=method is not("GET"or"HEAD")?405:path=="/denied-original-2400.jpg"?403:allowed.Contains(name)&&File.Exists(Path.Combine(root,name))?200:404;
            var body=status==200?await File.ReadAllBytesAsync(Path.Combine(root,name),timeout.Token):Encoding.UTF8.GetBytes("Synthetic fixture: original unavailable ("+status+")");
            var type=name.EndsWith(".jpg")?"image/jpeg":name.EndsWith(".json")?"application/json":"text/html; charset=utf-8";
            var prefix=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Fixture\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");await stream.WriteAsync(prefix,timeout.Token);if(method!="HEAD")await stream.WriteAsync(body,timeout.Token);
        }catch(Exception e)when(e is IOException or OperationCanceledException or ArgumentException){}
    }
}
