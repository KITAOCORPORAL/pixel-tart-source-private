using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using PixelTart.BrowserCapture.Contracts;
namespace RAWSelectionAssistant.Services.BrowserCapture;
public sealed class CapturePipeServer : IDisposable
{
    private readonly CancellationTokenSource _stop=new();
    public CapturePipeServer(string name, Func<CaptureRequest,CancellationToken,Task<CaptureReply>> handler)
    {
        Completion=Task.Run(async()=>{
            while(!_stop.IsCancellationRequested)
            {
                try{
                    await using var pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(85));
                    var data=await CaptureWire.ReadAsync(pipe,CaptureWire.MaximumRequestBytes,timeout.Token);
                    var request=data is null?null:JsonSerializer.Deserialize<CaptureRequest>(data,CaptureWire.Json);
                    var reply=request is null?new CaptureReply(false,"InvalidRequest","采集请求无效。"):await handler(request,timeout.Token);
                    await CaptureWire.WriteAsync(pipe,reply,CaptureWire.MaximumResponseBytes,timeout.Token);
                }catch(Exception e)when(e is IOException or JsonException or OperationCanceledException){if(_stop.IsCancellationRequested)break;}
            }
        });
    }
    public Task Completion {get;}
    public void Dispose()=>_stop.Cancel();
}
