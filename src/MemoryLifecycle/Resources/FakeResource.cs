namespace MemoryLifecycle.Resources;

public class FakeResource
{
    private static int counter;
    private readonly int id = ++counter;

    public FakeResource()
    {
        Console.WriteLine($"[FakeResource {id}] создан");
    }

    ~FakeResource()
    {
        Console.WriteLine($"[FakeResource {id}] финализирован");
    }
}
