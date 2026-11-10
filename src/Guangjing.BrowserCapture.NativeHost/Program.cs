using System.IO.Pipes;
using System.Text.Json;
using PixelTart.BrowserCapture.Contracts;

if(args.Length>0 && args[0]=="--serve-fixtures")
{
    await CaptureFixtureServer.RunAsync(Path.GetFullPath(args[1]),args.Length>2?int.Parse(args[2]):51292);return;
}

if(args.Length>0 && args[0]=="--prepare")
{
    var package=Path.GetFullPath(args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,".."));
    var identity=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(package,"extension","DEVELOPMENT_ID.json"))).RootElement;
    var extensionId=identity.GetProperty("extensionId").GetString()!;
    string? previousRoot=null;var previousConfig=Path.Combine(package,"native-host","capture-host-config.json");
    if(File.Exists(previousConfig))try{using var previous=JsonDocument.Parse(await File.ReadAllTextAsync(previousConfig));var value=previous.RootElement.GetProperty("dataRoot").GetString();if(value is not null&&Path.IsPathFullyQualified(value)&&Directory.Exists(value))previousRoot=value;}catch(Exception e)when(e is JsonException or IOException or KeyNotFoundException){}
    var runtime=args.Length>2?Path.GetFullPath(args[2]):previousRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KitaoDiagnostic","ChromeCaptureV1-"+extensionId);
    var sid=System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
    var pipe="guangjing.capture."+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sid+"|"+runtime)))[..24].ToLowerInvariant();
    var native=Path.Combine(package,"native-host");Directory.CreateDirectory(native);
    await File.WriteAllTextAsync(Path.Combine(native,"capture-host-config.json"),JsonSerializer.Serialize(new{extensionId,pipeName=pipe,dataRoot=runtime},CaptureWire.Json));
    var manifest=Path.Combine(native,"com.guangjing.capture.json");
    await File.WriteAllTextAsync(manifest,JsonSerializer.Serialize(new{name="com.guangjing.capture",description="Guangjing explicit image capture",path=Path.Combine(native,"Guangjing.Capture.NativeHost.exe"),type="stdio",allowed_origins=new[]{"chrome-extension://"+extensionId+"/"}},CaptureWire.Json));
    var slash=(char)92;var quote=(char)34;
    var key="HKEY_CURRENT_USER"+slash+string.Join(slash,new[]{"Software","Google","Chrome","NativeMessagingHosts","com.guangjing.capture"});
    var escaped=quote+manifest.Replace(slash.ToString(),new string(slash,2)).Replace(quote.ToString(),slash.ToString()+quote)+quote;
    var registration=Path.Combine(package,"Register-Guangjing-NativeHost.reg");
    await File.WriteAllTextAsync(registration,"Windows Registry Editor Version 5.00\r\n\r\n["+key+"]\r\n@="+escaped+"\r\n",System.Text.Encoding.Unicode);
    Console.WriteLine("Prepared local files only; no registry settings changed.");Console.WriteLine(registration);Console.WriteLine("Extension ID: "+extensionId);return;
}

// stdout is exclusively the Chrome Native Messaging binary protocol.
using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
try
{
    var config = JsonSerializer.Deserialize<HostConfig>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "capture-host-config.json")), CaptureWire.Json)
        ?? throw new InvalidDataException("Missing host configuration.");
    if (args.Length == 0 || args[0] != "chrome-extension://" + config.ExtensionId + "/")
        throw new UnauthorizedAccessException("Caller origin is not registered for this host.");
    var bytes = await CaptureWire.ReadAsync(input, CaptureWire.MaximumRequestBytes);
    if (bytes is null) return;
    var request = JsonSerializer.Deserialize<CaptureRequest>(bytes, CaptureWire.Json) ?? throw new InvalidDataException("Missing request.");
    request = request with { ExtensionOrigin = args[0] };
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    using var pipe = new NamedPipeClientStream(".", config.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    try { await pipe.ConnectAsync(2500, timeout.Token); }
    catch (Exception e) when (e is TimeoutException or IOException) {
        await CaptureWire.WriteAsync(output, new CaptureReply(false, "DesktopUnavailable", "光镜桌面暂不可用，采集已保留。请打开配套桌面入口后重试。", Retryable:true), CaptureWire.MaximumResponseBytes); return;
    }
    await CaptureWire.WriteAsync(pipe, request, CaptureWire.MaximumRequestBytes, timeout.Token);
    var response = await CaptureWire.ReadAsync(pipe, CaptureWire.MaximumResponseBytes, timeout.Token) ?? throw new IOException("Desktop disconnected.");
    var reply = JsonSerializer.Deserialize<CaptureReply>(response, CaptureWire.Json) ?? throw new IOException("Missing desktop receipt.");
    await CaptureWire.WriteAsync(output, reply, CaptureWire.MaximumResponseBytes);
}
catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or OperationCanceledException or ArgumentException)
{
    await CaptureWire.WriteAsync(output, new CaptureReply(false, "BridgeUnavailable", "本机桥接未完成；待处理采集已保留，请重试或检查配套桌面入口。", Retryable:true), CaptureWire.MaximumResponseBytes);
}
internal sealed record HostConfig(string ExtensionId, string PipeName);
