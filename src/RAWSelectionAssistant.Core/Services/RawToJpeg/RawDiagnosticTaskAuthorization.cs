namespace RAWSelectionAssistant.Core.Services.RawToJpeg;
/// <summary>Diagnostic task admission only; it does not grant a RAW decode reservation.</summary>
public static class RawDiagnosticTaskAuthorization
{
    public const string LegacyTask="RAW-XTRANS-STABLE-V1-PRODUCT-REGRESSION-20261110";
    public const string MemoryFixTask="RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111";
    public static void Validate(int version,string task,bool authorizedForHeavyDecode)
    {
        if(version==1&&task==LegacyTask)return;
        if(version!=2||task!=MemoryFixTask)throw new InvalidDataException("Unknown diagnostic task/version; no RAW decode is admitted.");
        if(!authorizedForHeavyDecode)throw new InvalidDataException("Memory-fix regression requires explicit new-batch authorization; no RAW decode is admitted.");
    }
}
