using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Threading.Tasks;
using MemoryLifecycle.Gc;
using MemoryLifecycle.Resources;
using MemoryLifecycle.Demos;

namespace MemoryLifecycle;

class Program
{
    const int ThreadCount = 4;
    const int ObjectsPerThread = 300_000;
    const int ObjectSize = 100;

    static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "gc-pressure")
        {
            await RunGcPressureBenchmarkAsync();
        }
        else if (args.Length > 0 && args[0] == "gc-modes")
        {
            GcModeScenarios.RunAll();
        }
        else if (args.Length > 0 && args[0] == "dispose")
        {
            ResourceDemos.RunAll();
        }
        else if (args.Length > 0 && args[0] == "dispose-v1")
        {
            await RunDisposeVariant1Async();
        }
        else if (args.Length > 0 && args[0] == "dispose-v2")
        {
            await RunDisposeVariant2Async();
        }
        else
        {
            Console.WriteLine("Hello, World!");
            Console.WriteLine("Доступные команды:");
            Console.WriteLine("  gc-pressure    —бенчмарк давления на кучу (Workstation vs Server GC)");
            Console.WriteLine("  gc-modes       — сценарии работы с режимами GC");
            Console.WriteLine("  dispose        — примеры IDisposable и финализаторов");
            Console.WriteLine("  dispose-v1     — вариант 1: DatabaseConnection (IDisposable, IAsyncDisposable)");
            Console.WriteLine("  dispose-v2     — вариант 2: FileBuffer (IDisposable, IAsyncDisposable)");
        }
    }

    static async Task RunGcPressureBenchmarkAsync()
    {
        bool serverGc = GCSettings.IsServerGC;

        Console.WriteLine("=== GC Benchmark: Workstation vs Server GC ===");
        Console.WriteLine($"GCSettings.IsServerGC = {serverGc}");
        Console.WriteLine($"Processor count: {Environment.ProcessorCount}");
        Console.WriteLine($"Threads: {ThreadCount}");
        Console.WriteLine($"Objects per thread: {ObjectsPerThread:N0}");
        Console.WriteLine($"Object size: {ObjectSize} bytes");
        Console.WriteLine($"Total allocated (approx): {ThreadCount * ObjectsPerThread * ObjectSize / 1024.0 / 1024:F2} MB");
        Console.WriteLine();

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        long allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);

        var sw = Stopwatch.StartNew();

        var tasks = new Task[ThreadCount];
        for (int i = 0; i < ThreadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < ObjectsPerThread; j++)
                {
                    var obj = new byte[ObjectSize];
                    GC.KeepAlive(obj);
                }
                Console.WriteLine($"  Thread {threadIndex} finished");
            });
        }

        await Task.WhenAll(tasks);

        sw.Stop();

        int gen0After = GC.CollectionCount(0);
        int gen1After = GC.CollectionCount(1);
        int gen2After = GC.CollectionCount(2);
        long allocatedBytesAfter = GC.GetTotalAllocatedBytes(precise: true);

        Console.WriteLine();
        Console.WriteLine("=== Results ===");
        Console.WriteLine($"GCSettings.IsServerGC = {serverGc}");
        Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Gen0 collections: {gen0After - gen0Before}");
        Console.WriteLine($"Gen1 collections: {gen1After - gen1Before}");
        Console.WriteLine($"Gen2 collections: {gen2After - gen2Before}");
        Console.WriteLine($"Total allocated bytes: {(allocatedBytesAfter - allocatedBytesBefore) / 1024 / 1024:N2} MB");
        Console.WriteLine();
    }

    static async Task RunDisposeVariant1Async()
    {
        Console.WriteLine("=== Вариант 1: DatabaseConnection (IDisposable, IAsyncDisposable) ===");

        Console.WriteLine();
        Console.WriteLine("1. Синхронный using");
        using (var connection = new DatabaseConnection())
        {
            connection.Execute("SELECT * FROM Students");
        }

        Console.WriteLine();
        Console.WriteLine("2. Асинхронный await using");
        await using (var connection = new DatabaseConnection())
        {
            connection.Execute("SELECT * FROM Topics");
            await Task.Delay(1);
        }

        Console.WriteLine();
        Console.WriteLine("3. Проверка GC.SuppressFinalize после using");
        using (var connection = new DatabaseConnection())
        {
            connection.Execute("SELECT 1");
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После сборки: сообщение финализатора отсутствует");

        Console.WriteLine();
        Console.WriteLine("4. Финализатор без Dispose");
        CreateDatabaseConnectionWithoutDispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После сборки: финализатор освободил только нативный ресурс");
    }

    static void CreateDatabaseConnectionWithoutDispose()
    {
        var connection = new DatabaseConnection();
        connection.Execute("UPDATE Students SET Name = 'Test'");
    }

    static async Task RunDisposeVariant2Async()
    {
        Console.WriteLine("=== Вариант 2: FileBuffer (IDisposable, IAsyncDisposable) ===");

        Console.WriteLine();
        Console.WriteLine("1. Синхронный using");
        using (var buffer = new FileBuffer())
        {
            buffer.Write(new byte[] { 1, 2, 3, 4 });
        }

        Console.WriteLine();
        Console.WriteLine("2. Асинхронный await using");
        await using (var buffer = new FileBuffer())
        {
            buffer.Write(new byte[] { 5, 6, 7 });
            await Task.Delay(1);
        }

        Console.WriteLine();
        Console.WriteLine("3. Проверка GC.SuppressFinalize после using");
        using (var buffer = new FileBuffer())
        {
            buffer.Write(new byte[] { 8, 9 });
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После сборки: сообщение финализатора отсутствует");

        Console.WriteLine();
        Console.WriteLine("4. Финализатор без Dispose");
        CreateFileBufferWithoutDispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После сборки: финализатор освободил только нативный ресурс");
    }

    static void CreateFileBufferWithoutDispose()
    {
        var buffer = new FileBuffer();
        buffer.Write(new byte[] { 10, 11 });
    }
}
