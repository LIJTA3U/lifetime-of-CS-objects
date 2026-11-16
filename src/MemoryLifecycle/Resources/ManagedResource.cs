namespace MemoryLifecycle.Resources;

public sealed class ManagedResource : IDisposable
{
    private static int counter;
    private readonly int id = ++counter;
    private bool disposed;

    public ManagedResource()
    {
        Console.WriteLine($"[ManagedResource {id}] создан");
    }

    public bool IsDisposed => disposed;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Console.WriteLine($"[ManagedResource {id}] Dispose вызван");
    }
}
