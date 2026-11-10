using RAWSelectionAssistant.Services.Diagnostics;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private readonly bool diagnosticRawEnabled;
 private string? loadedDiagnosticDecoderIdentity;
 public string DecoderDiagnosticStatus=>diagnosticRawEnabled?"隔离诊断：X-Trans stable-input V1；非 X-Trans 使用默认解码器":loadedDiagnosticDecoderIdentity is not null?"候选已关停：使用默认解码器；候选缓存不复用":"";
 private void RestoreDiagnosticDecoderRequest(string? identity){loadedDiagnosticDecoderIdentity=identity;OnPropertyChanged(nameof(DecoderDiagnosticStatus));}
}
