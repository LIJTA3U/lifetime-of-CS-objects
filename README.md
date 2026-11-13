# MemoryLifecycle

Примеры работы с памятью, сборщиком мусора (GC) и паттерном Dispose в .NET.

## Структура

- `docs/` — статьи по теме (GC, режимы GC, IDisposable, паттерн Dispose)
- `src/MemoryLifecycle/` — исходный код проекта:
  - `Program.cs` — точка входа (поддерживает команду `gc-pressure` и GC-бенчмарк)
  - `Gc/` — сценарии работы с GC (`GcSnapshot`, `GcObserver`, `PressureScenarios`, `Resources`)
  - `Resources/` — примеры управляемых и неуправляемых ресурсов
  - `Demos/` — демонстрации освобождения ресурсов (вынесены на уровень `src/Demos`)

## КТ №40. Поколения и давление на кучу

Команда: `dotnet run --project src/MemoryLifecycle/MemoryLifecycle.csproj -- gc-pressure`

Условия запуска: .NET 9, конфигурация Release, без подключённого профилировщика.

| # | Сценарий | Аллокаций | Gen0 | Gen2 | Ожидаемое поведение |
|---|----------|-----------|------|------|---------------------|
| 1 | Много мелких объектов (100 000 × byte[100]) | ~12,97 МБ | растёт | не меняется | Объекты аллоцируются в Gen0, вызывая сборку поколения 0. |
| 2 | Несколько крупных объектов (10 × byte[100_000]) | ~0,95 МБ | не меняется | не меняется\* | Массивы > 85 КБ попадают в LOH (Gen2); счётчик Gen2 растёт только при фактической сборке Gen2. |
| 3 | Долгоживущие объекты в статическом списке (10 000 × byte[1000]) | ~10,02 МБ | растёт | не меняется\* | Объекты удерживаются статическим полем, переживают Gen0/Gen1 и со временем перемещаются в Gen2. |
| 4 | Принудительная сборка Gen2 (blocking) | 0 МБ | может расти | растёт | Принудительный вызов GC.Collect(2, Forced) увеличивает Gen2 на 1. |

\* Gen2-счётчик увеличивается только после реальной сборки поколения 2, а не при каждой крупной аллокации.

---

## GC-бенчмарк: Workstation GC vs Server GC

Программа (`Program.cs`) выполняет следующее:

1. Выводит `GCSettings.IsServerGC` — доказательство текущего режима GC.
2. Делает warm-up: forced Gen2 сборка для очистки счётчиков.
3. Фиксирует `GC.CollectionCount(0/1/2)` и `GC.GetTotalAllocatedBytes()` **до** начала.
4. Запускает **4 параллельных потока** через `Task.Run`, каждый в цикле создаёт `300 000 × new byte[100]`.
5. Ждёт завершения всех потоков через `Task.WhenAll`.
6. Фиксирует счётчики **после** завершения, выводит разницу, время выполнения и `GCSettings.IsServerGC`.

Общий объём аллокаций: `4 × 300 000 × 100 байт ≈ 114,44 МБ`.

### Как запустить

Соберите проект в **Release** и запускайте **без отладчика**:

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```

#### Вариант 1 — Workstation GC (по умолчанию)
```powershell
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

#### Вариант 2 — Server GC (через .csproj)
Добавьте в `src/MemoryLifecycle/MemoryLifecycle.csproj`:
```xml
<PropertyGroup>
  ...
  <ServerGarbageCollection>true</ServerGarbageCollection>
</PropertyGroup>
```
Затем пересоберите и запустите:
```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

#### Вариант 3 — Server GC (без изменения .csproj, через переменную окружения)
```powershell
$env:DOTNET_gcServer=1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

---

## Результаты измерений

### Условия тестирования

- .NET 9, конфигурация Release, без отладчика
- 4 потока, по 300 000 объектов `new byte[100]` в каждом
- ПК: 12 логических процессоров
- Запуски повторялись 3 раза для каждого режима

### Сценарий 1 — ПК в простое (ничего не запущено)

| Запуск | Время (мс) | Gen0 | Gen1 | Gen2 | Аллокации (МБ) | IsServerGC |
|--------|-----------|------|------|------|----------------|------------|
| Workstation GC | 18 / 10 / 10 | 16 | 0 | 0 | 146,00 | False |
| Server GC | 12 / 13 / 11 | 58 | 0 | 0 | 146,00 | True |

### Сценарий 2 — Запущен браузер

| Запуск | Время (мс) | Gen0 | Gen1 | Gen2 | Аллокации (МБ) | IsServerGC |
|--------|-----------|------|------|------|----------------|------------|
| Workstation GC | ~13 | 16 | 0 | 0 | 146,00 | False |
| Server GC | ~12 | 58 | 0 | 0 | 146,00 | True |

### Сценарий 3 — Браузер + 4K видео

| Запуск | Время (мс) | Gen0 | Gen1 | Gen2 | Аллокации (МБ) | IsServerGC |
|--------|-----------|------|------|------|----------------|------------|
| Workstation GC | ~13 | 16 | 0 | 0 | 146,00 | False |
| Server GC | ~12 | 58 | 0 | 0 | 146,00 | True |

### Итоговое сравнение

| Параметр | Workstation GC | Server GC |
|----------|---------------|-----------|
| `GCSettings.IsServerGC` | False | True |
| Кучи (heaps) | 1 (общая) | ~12 (по числу ядер) |
| Gen0 коллекций | 16 | 58 |
| Gen1 / Gen2 коллекций | 0 / 0 | 0 / 0 |
| Время выполнения | ~13 мс | ~12 мс |
| Аллокации | 146,00 МБ | 146,00 МБ |

---

## Как самому запустить и проверить результаты

### Подготовка

1. Убедитесь, что установлен **.NET 9 SDK**:
   ```powershell
   dotnet --version
   ```
   Должно вывести что-то вроде `9.0.xxx`.

2. Откройте терминал (PowerShell или cmd) в **корне репозитория** `lifetime-of-CS-objects`, где лежит файл `MemoryLifecycle.sln`.

3. Очистите старые результаты сборки (опционально, но рекомендуется для чистоты эксперимента):
   ```powershell
   dotnet clean src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
   ```

---

### Шаг 1. Сборка проекта в Release

Выполните в терминале:

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```

**Что должно произойти:** сборка завершится без ошибок, в консоли появится строка `Сборка успешно завершена. Предупреждений: 0, Ошибок: 0`.

Скомпилированный файл появится по пути:
`src\MemoryLifecycle\bin\Release\net9.0\MemoryLifecycle.dll`

---

### Шаг 2. Запуск в режиме Workstation GC (по умолчанию)

Выполните:

```powershell
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

**Пример вывода, который вы должны увидеть:**

```
=== GC Benchmark ===
GCSettings.IsServerGC = False
Processor count: 12
Threads: 4
Objects per thread: 300,000
Object size: 100 bytes
Total allocated (approx): 114,44 MB

  Thread 3 finished
  Thread 1 finished
  Thread 0 finished
  Thread 2 finished

=== Results ===
GCSettings.IsServerGC = False
Elapsed time: 18 ms
Gen0 collections: 16
Gen1 collections: 0
Gen2 collections: 0
Total allocated bytes: 146,00 MB
```

**Запишите в таблицу:**
- `GCSettings.IsServerGC` — должно быть `False`
- `Elapsed time` — время выполнения в мс
- `Gen0 collections`, `Gen1 collections`, `Gen2 collections`
- `Total allocated bytes`

**Повторите запуск 2–3 раза**, чтобы убедиться, что результаты стабильны (время может немного плавать, но Gen0 должен оставаться около 16).

---

### Шаг 3. Запуск в режиме Server GC

Есть два способа переключить проект на Server GC. Выберите любой один.

#### Способ А — через файл проекта `.csproj` (рекомендуется для отчёта)

1. Откройте файл `src/MemoryLifecycle/MemoryLifecycle.csproj` в любом текстовом редакторе (Блокнот, VS Code, Visual Studio).

2. Найдите блок `<PropertyGroup>` и добавьте в него строку:

   ```xml
   <PropertyGroup>
     <OutputType>Exe</OutputType>
     <TargetFramework>net9.0</TargetFramework>
     <ImplicitUsings>enable</ImplicitUsings>
     <Nullable>enable</Nullable>
     <ServerGarbageCollection>true</ServerGarbageCollection>
   </PropertyGroup>
   ```

3. Сохраните файл.

4. Пересоберите проект:
   ```powershell
   dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
   ```

5. Запустите:
   ```powershell
   dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
   ```

**Пример вывода (Server GC):**

```
=== GC Benchmark ===
GCSettings.IsServerGC = True
Processor count: 12
Threads: 4
Objects per thread: 300,000
Object size: 100 bytes
Total allocated (approx): 114,44 MB

  Thread 2 finished
  Thread 0 finished
  Thread 3 finished
  Thread 1 finished

=== Results ===
GCSettings.IsServerGC = True
Elapsed time: 12 ms
Gen0 collections: 58
Gen1 collections: 0
Gen2 collections: 0
Total allocated bytes: 146,00 MB
```

**Запишите в таблицу:** те же метрики, что и на шаге 2. `GCSettings.IsServerGC` должно быть `True`.

#### Способ Б — через переменную окружения (без изменения .csproj)

Если не хотите править `.csproj`, выполните в том же окне терминала:

```powershell
$env:DOTNET_gcServer=1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

После этого переменная окружения действует только в текущем сеансе терминала. Чтобы вернуть Workstation GC, закройте терминал или выполните `$env:DOTNET_gcServer=0`.

**Важно:** если используете этот способ, укажите в отчёте обе команды:
```powershell
$env:DOTNET_gcServer=1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

---

### Шаг 4. Проверка в трёх условиях компьютера

Повторите **Шаг 2** и **Шаг 3** в каждом из следующих сценариев:

| Сценарий | Что сделать перед запуском |
|----------|---------------------------|
| **1. ПК в простое** | Закройте браузер, мессенджеры и другие тяжелые приложения. Оставьте только терминал. |
| **2. Браузер запущен** | Откройте браузер с несколькими вкладками. Не закрывайте его во время замеров. |
| **3. Браузер + 4K видео** | Запустите браузер и включите воспроизведение видео в 4K (YouTube и т.п.) на фоне. |

**Для каждого сценария запишите:**

| Режим GC | Время (мс) | Gen0 | Gen1 | Gen2 | Аллокации (МБ) | IsServerGC |
|----------|-----------|------|------|------|----------------|------------|
| Workstation | | | | | | False |
| Server | | | | | | True |

**Что вы должны увидеть в результатах:**

- `GCSettings.IsServerGC` = `False` для Workstation GC и `True` для Server GC — это главное доказательство, что режим действительно изменился.
- Gen0 у Server GC существенно выше (примерно 58 против 16 при 4 потоках и 12 ядрах) — **это нормально, не ошибка**.
- Gen1 и Gen2 остаются `0` в обоих режимах.
- Аллокации одинаковы (примерно 146 МБ) — число потоков и объектов не менялось, сравнение корректно.
- Время выполнения примерно сопоставимо (Server GC может быть чуть быстрее или на том же уровне).

---

### Шаг 5. Интерпретация результата: почему Server GC показывает больше Gen0

Это не ошибка, а особенность работы Server GC:

- **Workstation GC** использует **одну общую кучу** для всего процесса. У неё одно поколение Gen0, которое заполняется постепенно.
- **Server GC** создаёт **несколько куч** — обычно по одной на логический процессор (у вас 12 ядер = до 12 куч). У каждой кучи своё поколение Gen0.

Когда 4 потока одновременно создают `new byte[100]`, объекты распределяются по разным кучам. Каждая куча заполняет свой Gen0 быстрее, чем одна общая куча в Workstation GC. Поэтому суммарное число Gen0-сборок у Server GC выше (58 против 16).

Это ожидаемое поведение: Server GC оптимизирован для **пропускной способности (throughput)** на многопроцессорных системах, а не для минимального числа отдельных сборок.

---

### Шпаргалка: команды для копирования

**Сборка:**
```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```

**Workstation GC:**
```powershell
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

**Server GC через .csproj** (после добавления `<ServerGarbageCollection>true</ServerGarbageCollection>` и сохранения файла):
```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

**Server GC через переменную окружения:**
```powershell
$env:DOTNET_gcServer=1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```


---

## КТ №41. Workstation и Server GC

Для этапа КТ №41 добавлены:

- `Gc/GcModeScenarios.cs` — сценарии работы с режимами GC
- Команда `gc-modes` в `Program.cs`
- Статья `docs/02-gcmodes.md`

### Команда запуска

Соберите проект в Release и запускайте без отладчика:

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```

Workstation GC (по умолчанию):

```powershell
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll gc-modes
```

Server GC (через `.csproj` с `<ServerGarbageCollection>true</ServerGarbageCollection>`):

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll gc-modes
```

Server GC (через переменную окружения):

```powershell
$env:DOTNET_gcServer=1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll gc-modes
```

### Таблица 1. Workstation GC (по умолчанию)

| Запуск | IsServerGC | Время (мс) | Gen0 | Gen1 | Gen2 | Аллокации (МБ) |
|--------|------------|------------|------|------|------|----------------|
| 1 | False | | | | | |
| 2 | False | | | | | |
| 3 | False | | | | | |

### Таблица 2. Server GC

| Запуск | IsServerGC | Время (мс) | Gen0 | Gen1 | Gen2 | Аллокации (МБ) |
|--------|------------|------------|------|------|------|----------------|
| 1 | True | | | | | |
| 2 | True | | | | | |
| 3 | True | | | | | |

### Почему результаты могут различаться (внимание к типичным ошибкам)

- **Workstation GC** использует **одну общую кучу**. У Server GC **несколько куч** — обычно по одной на логический процессор, каждая со своим Gen0. Поэтому у Server GC больше Gen0-сборок — это не ошибка, а особенность архитектуры с несколькими кучами.
- **Не делайте вывод о деградации** только из-за большего числа Gen0 у Server GC.
- **Число потоков (4) и объектов (300 000 на поток) должно быть одинаковым** в обоих запусках, иначе сравнение недействительно.
- **Запуск должен быть в конфигурации Release без отладчика.** Сравнение в Debug или с подключённым отладчиком некорректно.
- **Недостаточно одного запуска.** Необходимо минимум 2–3 запуска для оценки стабильности, а также учитывать прогрев (warm-up) и фоновую нагрузку на ПК.
- **Обязательно выводите `GCSettings.IsServerGC`** в обоих режимах — иначе невозможно доказать, что режим действительно изменился.
- **Время измеряется через `Stopwatch`**, а не субъективными ощущениями.

---

## Реализация (соответствует методичке КТ №40, раздел 3.2)

- `Gc/GcSnapshot.cs` — `readonly record struct GcSnapshot` с `Capture()` и `Diff()`, использует `GC.GetTotalAllocatedBytes(precise: true)` и `GC.GetGCMemoryInfo().HeapSizeBytes`
- `Gc/GcObserver.cs` — `GcObserver.PrintMemoryInfo(string label)` для вывода состояния кучи
- `Gc/PressureScenarios.cs` — четыре сценария: `RunSmallObjects`, `RunLargeObjects`, `RunLongLived`, `RunForcedGen2`
- `Gc/GcModeScenarios.cs` — сценарии для КТ №41 (Workstation vs Server GC)
- `src/MemoryLifecycle/Program.cs` — команды `gc-pressure` и `gc-modes`, GC-бенчмарк Workstation vs Server GC

Отчёт: `docs/01-gc.md`

