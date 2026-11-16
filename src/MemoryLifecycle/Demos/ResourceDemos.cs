using MemoryLifecycle.Resources;

namespace MemoryLifecycle.Demos;

public static class ResourceDemos
{
    public static void RunAll()
    {
        string path = Path.Combine(Path.GetTempPath(), "MemoryLifecycle-log.txt");
        RunFinalizerDemo();
        RunUsingDemo();
        RunForgottenDisposeDemo();
        RunLogFileDemo(path);
    }

    public static void RunFinalizerDemo()
    {
        Console.WriteLine("1. Финализатор: объект создаётся без ссылки");
        CreateFakeResources();
        Console.WriteLine("Принудительный GC, финализатор должен сработать");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После GC.Collect + WaitForPendingFinalizers");
    }

    public static void RunUsingDemo()
    {
        Console.WriteLine();
        Console.WriteLine("2. using: детерминированное Dispose");
        using (var resource = new ManagedResource())
        {
            Console.WriteLine("Внутри using-блока");
        }

        Console.WriteLine("После выхода из using-блока");
    }

    public static void RunForgottenDisposeDemo()
    {
        Console.WriteLine();
        Console.WriteLine("3. Забытый Dispose: объект не освобождается");
        CreateManagedResourceWithoutDispose();
        Console.WriteLine("Принудительный GC, Dispose не был вызван");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После Dispose (FINALIZER не вызван, т.к. нет финализатора)");
    }

    public static void RunLogFileDemo(string path)
    {
        Console.WriteLine();
        Console.WriteLine("4. LogFile: запись в файл через using");

        using (var log = new LogFile(path))
        {
            log.Write("Первая запись из using");
        }

        Console.WriteLine("После выхода из using-блока");

        CreateLogFileWithoutDispose(path);
        Console.WriteLine("Создан LogFile без Dispose");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После Dispose: финализатор сработал");
    }

    private static void CreateFakeResources()
    {
        _ = new FakeResource();
        _ = new FakeResource();
    }

    private static void CreateManagedResourceWithoutDispose()
    {
        var resource = new ManagedResource();
        Console.WriteLine($"Объект создан, IsDisposed = {resource.IsDisposed}");
        GC.KeepAlive(resource);
    }

    private static void CreateLogFileWithoutDispose(string path)
    {
        var log = new LogFile(path);
        log.Write("Запись без using: Dispose не вызван");
        GC.KeepAlive(log);
    }
}
