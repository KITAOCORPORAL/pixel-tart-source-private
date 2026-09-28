namespace RAWSelectionAssistant.Core.Services.Projects;

public enum GpuExecutionAvailability
{
    GpuAvailable,
    GpuUnavailable,
    GpuInsufficientMemory,
    GpuDeviceLost,
    GpuUnsupported
}

/// <summary>Product-facing capability classification. Adapter names are never used as a support test.</summary>
public sealed record GpuCapabilityStatus(
    GpuExecutionAvailability Availability,
    GpuCapabilityInfo Details,
    string Message);

public sealed class GpuCapabilityService
{
    public GpuCapabilityStatus Detect(CancellationToken token = default)
    {
        var details = GpuCapabilityDetector.Detect(token);
        if (!details.DeviceCreated || !details.SmokeTestPassed || !details.BackendAvailable)
        {
            var availability = details.FailureReason?.Contains("memory", StringComparison.OrdinalIgnoreCase) == true
                ? GpuExecutionAvailability.GpuInsufficientMemory
                : OperatingSystem.IsWindows() ? GpuExecutionAvailability.GpuUnavailable : GpuExecutionAvailability.GpuUnsupported;
            return new(availability, details, details.FailureReason ?? "No validated GPU compute device is available.");
        }
        return new(GpuExecutionAvailability.GpuAvailable, details, "Validated DX12 compute device is available.");
    }
}
