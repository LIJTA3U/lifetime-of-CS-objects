namespace MemoryLifecycle.Gc;

/// <summary>
/// Наблюдение за состоянием GC из кода.
/// Соответствует эталону из методички КТ №40 (раздел 3.2.3).
/// </summary>
public static class GcObserver
{
    public static void PrintMemoryInfo(string label)
    {
        var info = GC.GetGCMemoryInfo();
        Console.WriteLine($"{label}:");
        Console.WriteLine($"  HeapSizeBytes      = {info.HeapSizeBytes / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"  TotalCommittedBytes= {info.TotalCommittedBytes / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"  MemoryLoadBytes    = {info.MemoryLoadBytes / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine();
    }
}
