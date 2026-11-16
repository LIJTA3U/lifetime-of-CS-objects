using System;
using System.Runtime;

namespace MemoryLifecycle.Gc;

public static class GcModeScenarios
{
    public static void RunAll()
    {
        Console.WriteLine("=== КТ №41. Режимы GC: Workstation vs Server ===");
        Console.WriteLine();

        // Выводим информацию о текущем режиме GC — доказательство, какой режим активен
        Console.WriteLine($"GCSettings.IsServerGC = {GCSettings.IsServerGC}");
        Console.WriteLine($"Processor count = {Environment.ProcessorCount}");
        Console.WriteLine();

        // Сценарий: массовая аллокация мелких объектов для наблюдения за поведением GC
        Console.WriteLine("Аллокация 1 000 000 мелких объектов (new object())...");
        var objects = new object[1_000_000];
        for (int i = 0; i < objects.Length; i++)
        {
            objects[i] = new object();
        }

        Console.WriteLine($"Аллоцировано объектов: {objects.Length:N0}");
        Console.WriteLine($"Gen0 сборок: {GC.CollectionCount(0)}");
        Console.WriteLine($"Gen1 сборок: {GC.CollectionCount(1)}");
        Console.WriteLine($"Gen2 сборок: {GC.CollectionCount(2)}");
        Console.WriteLine($"Всего аллоцировано байт: {GC.GetTotalAllocatedBytes(precise: true) / 1024 / 1024:N2} МБ");
        Console.WriteLine();

        // Освобождаем ссылки и форсируем сборку
        for (int i = 0; i < objects.Length; i++)
        {
            objects[i] = null!;
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        Console.WriteLine("Принудительная полная сборка (Gen2) выполнена.");
        Console.WriteLine($"Gen0 сборок после forced Gen2: {GC.CollectionCount(0)}");
        Console.WriteLine($"Gen1 сборок после forced Gen2: {GC.CollectionCount(1)}");
        Console.WriteLine($"Gen2 сборок после forced Gen2: {GC.CollectionCount(2)}");
        Console.WriteLine();
    }
}
