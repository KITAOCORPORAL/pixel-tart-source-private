using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private StudioExportFormat _quickExportFormat;
    private StudioExportFormat _lastExportFormat;
    public StudioExportFormat QuickExportFormat
    {
        get=>_quickExportFormat;
        set { if(Enum.IsDefined(value)&&SetProperty(ref _quickExportFormat,value))OnPropertyChanged(nameof(QuickExportFormatSummary)); }
    }
    public string QuickExportFormatSummary=>QuickExportFormat switch
    {
        StudioExportFormat.Jpeg=>StudioLocalizationService.Current["ExportJpegHelp"],
        StudioExportFormat.Png=>StudioLocalizationService.Current["ExportPngHelp"],
        StudioExportFormat.Tiff=>StudioLocalizationService.Current["ExportTiffHelp"],
        _=>StudioLocalizationService.Current["ExportSourceHelp"]
    };
}
