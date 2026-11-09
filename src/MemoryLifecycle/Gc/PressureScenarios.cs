using System.Diagnostics;

namespace MemoryLifecycle.Gc;

/// <summary>
/// Сценаррии измерения давления на кучу (КТ №40, вариант 2).
/// Каждый сценарий оформлен отдельным методом, чтобы измерения
/// не смешивали эффекты предыдущих аллокаций.
/// Соответствует эталону из методички (раздел 3.2.2).
/// </summary>
public static class PressureScenarios
{
    // Долгоживущие объекты удерживаются в статическом поле, чтобы
    // пережить сборки Gen0/Gen1 и в итоге переместиться в Gen2.
    private static readonly List<byte[]> _longLived = new();

    public static void RunAll()
    {
        Console.WriteLine("=== Измерение давления на кучу (Вариант 2) ===");
        GcObserver.PrintMemoryInfo("До начала тестов");
        Console.WriteLine(new string('-', 92));
        Console.WriteLine(
            "{0,-34}{1,10}{2,6}{3,6}{4,6}{5,11}{6,12}",
            "Сценарий", "Время, мс", "Gen0", "Gen1", "Gen2",
            "Alloc, МБ", "Heap Δ, МБ");
        Console.WriteLine(new string('-', 92));

        var results = new[]
        {
            RunSmallObjects(),
            RunLargeObjects(),
            RunLongLived(),
            RunForcedGen2(),
        };

        foreach (var result in results)
        {
            Console.WriteLine(
                "{0,-34}{1,10:F2}{2,6}{3,6}{4,6}{5,11:F2}{6,12:F2}",
                result.Name,
                result.Elapsed.TotalMilliseconds,
                result.Delta.Gen0,
                result.Delta.Gen1,
                result.Delta.Gen2,
                result.Delta.AllocatedMegabytes,
                result.Delta.HeapDeltaMegabytes);
        }

        Console.WriteLine(new string('-', 92));
        GcObserver.PrintMemoryInfo("После всех тестов");
        Console.WriteLine(
            "Разница показывает прирост аллокаций и размер кучи. " +
            "Счётчики поколений растут только после фактической сборки corresponding поколения.");
    }

    private static ScenarioResult RunSmallObjects()
    {
        const string name = "Много мелких объектов (byte[100])";
        var before = GcSnapshot.Capture();
        var stopwatch = Stopwatch.StartNew();

        // 100 000 массивов по 100 байт — все попадают в Gen0.
        // Чтобы JIT не удалил "мёртвые" выделения, результат удерживается
        // через GC.KeepAlive до конца метода.
        var small = new byte[100_000][];
        for (int i = 0; i < small.Length; i++)
            small[i] = new byte[100];
        GC.KeepAlive(small);

        stopwatch.Stop();
        var after = GcSnapshot.Capture();
        return new ScenarioResult(name, stopwatch.Elapsed, after.Diff(before));
    }

    private static ScenarioResult RunLargeObjects()
    {
        const string name = "Несколько крупных объектов (byte[100_000])";
        var before = GcSnapshot.Capture();
        var stopwatch = Stopwatch.StartNew();

        // 10 массивов по 100 000 байт. Каждый > 85 000 байт, поэтому
        // попадает в LOH (Generation 2). Аллокация в LOH не увеличивает
        // счётчик Gen0 — сборка Gen0 не вызывается при каждом выделении
        // в LOH. Счётчик Gen2 увеличится только при фактической сборке
        // поколения 2, которая может не произойти сразу.
        var large = new byte[10][];
        for (int i = 0; i < large.Length; i++)
            large[i] = new byte[100_000];
        GC.KeepAlive(large);

        stopwatch.Stop();
        var after = GcSnapshot.Capture();
        return new ScenarioResult(name, stopwatch.Elapsed, after.Diff(before));
    }

    private static ScenarioResult RunLongLived()
    {
        const string name = "Долгоживущие объекты в статическом списке";
        var before = GcSnapshot.Capture();
        var stopwatch = Stopwatch.StartNew();

        // Объекты добавляются в статический список, поэтому после выхода
        // из метода они остаются живыми. Сначала они аллоцируются в Gen0,
        // переживают сборки и перемещаются в Gen1, затем в Gen2. Со временем
        // Gen2 и общий размер кучи растут при накоплении больших массивов.
        for (int i = 0; i < 10_000; i++)
            _longLived.Add(new byte[1000]);
        GC.KeepAlive(_longLived);

        stopwatch.Stop();
        var after = GcSnapshot.Capture();
        return new ScenarioResult(name, stopwatch.Elapsed, after.Diff(before));
    }

    private static ScenarioResult RunForcedGen2()
    {
        const string name = "Принудительная сборка Gen2 (blocking)";
        var before = GcSnapshot.Capture();
        var stopwatch = Stopwatch.StartNew();

        // Принудительная блокирующая сборка поколения 2.
        // Счётчик Gen2 увеличится на 1, независимо от того, было что
        // собирать или нет. Это демонстрирует, что Gen2-счётчик растёт
        // только при реальной сборке поколения 2.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        stopwatch.Stop();
        var after = GcSnapshot.Capture();
        return new ScenarioResult(name, stopwatch.Elapsed, after.Diff(before));
    }

    public readonly record struct ScenarioResult(
        string Name, TimeSpan Elapsed, GcDelta Delta);
}
