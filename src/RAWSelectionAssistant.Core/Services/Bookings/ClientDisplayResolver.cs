namespace RAWSelectionAssistant.Core.Services.Bookings;

/// <summary>Resolves the display names already owned by Project and Booking records without introducing a parallel client store.</summary>
public sealed class ClientDisplayResolver
{
    public string Resolve(IEnumerable<string?> bookingNames, IEnumerable<string?> projectNames)
    {
        var names = bookingNames.Concat(projectNames)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names.Length switch
        {
            0 => "未关联",
            1 => names[0],
            _ => $"多个客户（{string.Join("、", names)}）"
        };
    }

}
