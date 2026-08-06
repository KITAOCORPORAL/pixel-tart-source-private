using System.Text.Json;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

public interface ICalendarAvailabilityStore
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    bool IsClosed(DateTime date);
    Task SetClosedAsync(DateTime date, bool isClosed, CancellationToken cancellationToken = default);
}

public sealed class JsonCalendarAvailabilityStore : ICalendarAvailabilityStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<DateOnly> _closedDates = [];
    private bool _loaded;

    public JsonCalendarAvailabilityStore(string? path = null) =>
        _path = path ?? Path.Combine(AppDataPaths.DataDirectory, "calendar-availability.json");

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded) return;
            _closedDates.Clear();
            if (File.Exists(_path))
            {
                await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, true);
                var values = await JsonSerializer.DeserializeAsync<List<string>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
                foreach (var value in values)
                    if (DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date)) _closedDates.Add(date);
            }
            _loaded = true;
        }
        finally { _gate.Release(); }
    }

    public bool IsClosed(DateTime date) => _closedDates.Contains(DateOnly.FromDateTime(date));

    public async Task SetClosedAsync(DateTime date, bool isClosed, CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var day = DateOnly.FromDateTime(date);
            if (isClosed) _closedDates.Add(day); else _closedDates.Remove(day);
            var directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            var temporary = _path + ".tmp";
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                await JsonSerializer.SerializeAsync(stream, _closedDates.OrderBy(x => x).Select(x => x.ToString("yyyy-MM-dd")).ToArray(), cancellationToken: cancellationToken).ConfigureAwait(false);
            File.Move(temporary, _path, true);
        }
        finally { _gate.Release(); }
    }
}
