namespace MemoryLifecycle.Gc;

/// <summary>
/// Снимок состояния сборщика мусора на момент вызова.
/// Соответствует эталону из методички КТ №40 (раздел 3.2.1).
/// </summary>
public readonly record struct GcSnapshot
{
    public int Gen0 { get; init; }
    public int Gen1 { get; init; }
    public int Gen2 { get; init; }
    public long AllocatedBytes { get; init; }
    public long HeapSizeBytes { get; init; }

    public static GcSnapshot Capture() => new()
    {
        Gen0 = GC.CollectionCount(0),
        Gen1 = GC.CollectionCount(1),
        Gen2 = GC.CollectionCount(2),
        AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true),
        HeapSizeBytes = GC.GetGCMemoryInfo().HeapSizeBytes,
    };

    public GcDelta Diff(GcSnapshot previous) => new(
        Gen0 - previous.Gen0,
        Gen1 - previous.Gen1,
        Gen2 - previous.Gen2,
        AllocatedBytes - previous.AllocatedBytes,
        HeapSizeBytes - previous.HeapSizeBytes);
}

/// <summary>
/// Разница между двумя снимками состояния GC.
/// </summary>
public readonly record struct GcDelta(
    int Gen0,
    int Gen1,
    int Gen2,
    long AllocatedBytes,
    long HeapSizeDelta)
{
    public double AllocatedMegabytes => AllocatedBytes / 1024d / 1024d;
    public double HeapDeltaMegabytes => HeapSizeDelta / 1024d / 1024d;
}
