namespace RAWSelectionAssistant.Core.Services.Color;

public enum PhotoBoundaryStage { SensorUnpacked, RenderedWorking, AdjustedWorking, DisplayDevicePreview, FileExport }
public enum PhotoTransfer { SensorCodes, Linear, EncodedSrgb, DeviceProfile }
public enum PhotoWhitePoint { CameraUnspecified, D65, PcsD50, DeviceProfile }

/// <summary>Evidence contract only. It never changes historical PV4/PV5/PV6 pixels or infers sensor data from rendered RGB.</summary>
public sealed record PhotoBoundaryContract(string Version, PhotoBoundaryStage Stage, string Space,
    PhotoTransfer Transfer, PhotoWhitePoint WhitePoint, int Bits, bool Quantized, bool Clipped,
    bool WhiteBalanceBaked, bool CameraMatrixBaked, int ChromaticAdaptations, string Identity)
{
    public PhotoBoundaryContract Validate()
    {
        if (string.IsNullOrWhiteSpace(Version) || string.IsNullOrWhiteSpace(Space) || string.IsNullOrWhiteSpace(Identity) ||
            !Enum.IsDefined(Stage) || !Enum.IsDefined(Transfer) || !Enum.IsDefined(WhitePoint) ||
            Bits is not (8 or 14 or 16 or 32) || ChromaticAdaptations is < 0 or > 1)
            throw new InvalidDataException("Invalid or unverified photo boundary contract.");
        if (Stage == PhotoBoundaryStage.SensorUnpacked && (Transfer != PhotoTransfer.SensorCodes || WhiteBalanceBaked || CameraMatrixBaked))
            throw new InvalidDataException("Rendered/WB/matrix output cannot be labelled sensor data.");
        if (Stage == PhotoBoundaryStage.DisplayDevicePreview && (Transfer != PhotoTransfer.DeviceProfile || WhitePoint != PhotoWhitePoint.DeviceProfile))
            throw new InvalidDataException("Monitor device pixels require a device-profile preview contract.");
        if (Space == "LibRaw Linear ProPhoto D65" && (Transfer != PhotoTransfer.Linear || WhitePoint != PhotoWhitePoint.D65))
            throw new InvalidDataException("LibRaw output convention is not standard ProPhoto ICC D50.");
        return this;
    }
    public void RequireWhiteBalanceNotAlreadyBaked()
    {
        Validate(); if (WhiteBalanceBaked) throw new InvalidDataException("Duplicate white balance is forbidden.");
    }
    public void RequireNoPriorChromaticAdaptation()
    {
        Validate(); if (ChromaticAdaptations != 0) throw new InvalidDataException("Duplicate chromatic adaptation is forbidden.");
    }
    public void RequireExportSource()
    {
        Validate(); if (Stage == PhotoBoundaryStage.DisplayDevicePreview || Transfer == PhotoTransfer.DeviceProfile)
            throw new InvalidDataException("Display device pixels cannot enter file export or editing.");
    }
}
