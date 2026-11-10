using System.IO.Pipes;
using System.Text.Json;
using PixelTart.BrowserCapture.Contracts;

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
catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or OperationCanceledException or ArgumentException)
{
    await CaptureWire.WriteAsync(output, new CaptureReply(false, "BridgeUnavailable", "本机桥接未完成；待处理采集已保留，请重试或检查配套桌面入口。", Retryable:true), CaptureWire.MaximumResponseBytes);
}
internal sealed record HostConfig(string ExtensionId, string PipeName);
