namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record StudioProcessingFailure(string Code,string Stage,string UserMessage,bool PreviousVersionPreserved=true,
    int? NativeCode=null,string? NativeName=null,long? RequiredBytes=null,long? AvailableBytes=null)
{
    public static StudioProcessingFailure From(Exception error,string stage)=>error switch
    {
        _ when error.Data["StudioFailure"] is StudioProcessingFailure failure=>failure,
        StudioBudgetException budget=>new("STUDIO_BUDGET_REJECTED",stage,"处理内存预算不足；保留上一有效结果，未降低尺寸或位深。",RequiredBytes:budget.RequiredBytes,AvailableBytes:budget.AvailableBytes),
        RAWSelectionAssistant.Core.Services.RawToJpeg.RawDecodeException raw=>new(raw.ErrorCode,raw.Stage,"RAW解码失败；保留上一有效结果。",NativeCode:raw.NativeCode,NativeName:raw.NativeName),
        OperationCanceledException=>new("STUDIO_CANCELLED",stage,"处理已取消；保留上一有效结果。"),

        InvalidDataException=>new("STUDIO_DOMAIN_OR_CONTRACT_MISMATCH",stage,"输入数值域或处理合同不匹配；未应用结果。"),
        IOException=>new("STUDIO_SOURCE_UNAVAILABLE_OR_CHANGED",stage,"输入源不可用或已改变；保留上一有效结果。"),
        NotSupportedException=>new("STUDIO_UNSUPPORTED_OPERATION",stage,"当前输入或处理工具不受支持；保留上一有效结果。"),
        _=>new("STUDIO_PROCESSING_FAILED",stage,"处理未完成；保留上一有效结果。")
    };
}
public static class StudioFailureException
{
    public static IOException SourceChanged(string message)
    {var error=new IOException(message);error.Data["StudioFailure"]=new StudioProcessingFailure("STUDIO_SOURCE_CHANGED","SourceIdentity","输入源已改变；保留上一有效结果。");return error;}
    public static InvalidDataException Domain(StudioProcessingFailure failure,Exception inner)
    {var error=new InvalidDataException(failure.UserMessage,inner);error.Data["StudioFailure"]=failure;return error;}
}

public sealed class StudioBudgetException(long required,long available) : InvalidOperationException("处理请求超过全应用资源预算；未降低尺寸或位深。")
{
    public long RequiredBytes {get;}=required;
    public long AvailableBytes {get;}=available;
}
