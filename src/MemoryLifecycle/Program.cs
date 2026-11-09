// See https://aka.ms/new-console-template for more information

using MemoryLifecycle.Gc;

if (args.Length > 0 && args[0] == "gc-pressure")
{
    PressureScenarios.RunAll();
}
else
{
    Console.WriteLine("Hello, World!");
    Console.WriteLine("Запустите `dotnet run -- gc-pressure` для измерения давления на кучу.");
}
