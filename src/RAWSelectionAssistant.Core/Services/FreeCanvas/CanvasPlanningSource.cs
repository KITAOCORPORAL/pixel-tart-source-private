using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Core.Services.FreeCanvas;

/// <summary>Local durable identity, not a thumbnail or external-original fallback.
/// Free-canvas workspaces are deliberately outside .ptplan v1 portability.</summary>
public sealed record CanvasPlanningSource(string PlanningRoot, PlanningProjectCopy Copy)
{
    public static CanvasPlanningSource From(PlanningProjectCopy copy)
    {
        var source=new CanvasPlanningSource(copy.StorageRoot??throw new InvalidDataException("自有参考未绑定策划根。"),copy);
        source.Validate();using var verified=source.OpenVerified();return source;
    }
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(PlanningRoot)||!Path.IsPathFullyQualified(PlanningRoot)||Copy is null||Copy.Kind!="reference")
            throw new InvalidDataException("画布自有参考来源身份无效。");
        Copy.Validate();
        if(PlanningRoot.StartsWith(@"\\?\",StringComparison.Ordinal)||PlanningRoot.StartsWith(@"\\.\",StringComparison.Ordinal)||PlanningRoot.AsSpan(Path.GetPathRoot(PlanningRoot)!.Length).Contains(':'))
            throw new InvalidDataException("画布自有参考不支持设备或备用数据流。");
    }
    public PlanningProjectCopy Bind()
    {
        Validate();return Copy with{StorageRoot=Path.GetFullPath(PlanningRoot)};
    }
    public FileStream OpenVerified(CancellationToken token=default)=>PlanningProjectCopyStorage.OpenVerifiedRead(Bind(),token);
}
