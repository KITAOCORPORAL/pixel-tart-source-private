namespace RAWSelectionAssistant.Core.Services.Projects;

public enum V4GpuStage { PairwiseOt, OklabTransform, MaskEvaluation, ResidualApplication, GamutMapping, PixelApplication }

public sealed class V4GpuTelemetry
{
    private readonly object _gate = new();
    private readonly Dictionary<V4GpuStage, long> _dispatches = [];
    private readonly Dictionary<V4GpuStage, TimeSpan> _gpuTimes = [];
    public string AdapterName { get; init; } = "Unavailable";
    public long DedicatedMemoryBytes { get; init; }
    public long BudgetBytes { get; init; }
    public long EstimatedAllocationBytes { get; private set; }
    public long PeakTrackedAllocationBytes { get; private set; }
    public int FallbackCount { get; private set; }
    public int OomCount { get; private set; }
    public int TileCount { get; private set; }
    public void RecordDispatch(V4GpuStage stage, TimeSpan elapsed, long allocationBytes = 0, int tiles = 1)
    {
        lock (_gate)
        {
            _dispatches[stage] = _dispatches.GetValueOrDefault(stage) + 1;
            _gpuTimes[stage] = _gpuTimes.GetValueOrDefault(stage) + elapsed;
            EstimatedAllocationBytes = allocationBytes; PeakTrackedAllocationBytes = Math.Max(PeakTrackedAllocationBytes, allocationBytes); TileCount += Math.Max(1, tiles);
        }
    }
    public void RecordFallback(bool outOfMemory = false) { lock (_gate) { FallbackCount++; if (outOfMemory) OomCount++; } }
    public IReadOnlyDictionary<V4GpuStage, long> DispatchCounts { get { lock (_gate) return new Dictionary<V4GpuStage, long>(_dispatches); } }
    public IReadOnlyDictionary<V4GpuStage, TimeSpan> GpuTimes { get { lock (_gate) return new Dictionary<V4GpuStage, TimeSpan>(_gpuTimes); } }
}

public static class V4TilePolicy
{
    public static int SelectTileSize(int width, int height, GpuMemoryBudget budget)
    {
        var pixels = (long)width * height;
        if (pixels >= 80_000_000 || budget.Tier == "LOW") return Math.Min(384, budget.TileSize);
        if (pixels >= 40_000_000) return Math.Min(768, budget.TileSize);
        return budget.TileSize;
    }

    public static int CountTiles(int width, int height, int tileSize, int overlap = 8)
    {
        if (tileSize <= overlap * 2) throw new ArgumentOutOfRangeException(nameof(tileSize));
        var step = tileSize - overlap * 2;
        return checked((int)Math.Ceiling((double)Math.Max(1, width - overlap * 2) / step) * (int)Math.Ceiling((double)Math.Max(1, height - overlap * 2) / step));
    }
}
