namespace RAWSelectionAssistant.Core.Services.Bookings;

internal static class BookingDocumentFileSafety
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".msi", ".msp", ".dll", ".scr", ".lnk"
    };

    public static bool IsSafeExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) && !BlockedExtensions.Contains(extension);
}
