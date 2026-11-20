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

---

### Шаг 3. Интерпретация результата: почему Server GC показывает больше Gen0

Это не ошибка, а особенность работы Server GC:

- **Workstation GC** использует **одну общую кучу** для всего процесса. У неё одно поколение Gen0, которое заполняется постепенно.
- **Server GC** создаёт **несколько куч** — обычно по одной на логический процессор (у вас 12 ядер = до 12 куч). У каждой кучи своё поколение Gen0.

Когда 4 потока одновременно создают `new byte[100]`, объекты распределяются по разным кучам. Каждая куча заполняет свой Gen0 быстрее, чем одна общая куча в Workstation GC. Поэтому суммарное число Gen0-сборок у Server GC выше (58 против 16).

Это ожидаемое поведение: Server GC оптимизирован для **пропускной способности (throughput)** на многопроцессорных системах, а не для минимального числа отдельных сборок.

---

![Скриншот КТ №40 — вывод gc-pressure](docs/screenshots/kt40.png)

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
| 1 | False | 10 | 16 | 0 | 0 | 146,00 |
| 2 | False | 9 | 16 | 0 | 0 | 146,00 |
| 3 | False | 11 | 16 | 0 | 0 | 146,00 |

### Таблица 2. Server GC

| Запуск | IsServerGC | Время (мс) | Gen0 | Gen1 | Gen2 | Аллокации (МБ) |
|--------|------------|------------|------|------|------|----------------|
| 1 | True | 12 | 58 | 0 | 0 | 146,00 |
| 2 | True | 11 | 58 | 0 | 0 | 146,00 |
| 3 | True | 13 | 58 | 0 | 0 | 146,00 |


Отчёт: `docs/01-gc.md`

---

![Скриншот КТ №41 — вывод gc-modes](docs/screenshots/kt41.png)

---

## КТ №42. Финализируемые объекты. IDisposable. using

Для этапа КТ №42 добавлены:

- `Resources/FakeResource.cs` — класс с финализатором (`~FakeResource`), без `IDisposable`
- `Resources/ManagedResource.cs` — `sealed class : IDisposable` с флагом `disposed` и свойством `IsDisposed`
- `Resources/LogFile.cs` — `sealed class : IDisposable` с `StreamWriter`, `GC.SuppressFinalize(this)` и финализатором
- `Demos/ResourceDemos.cs` — четыре демо-сценария: финализатор, `using`, забытый `Dispose`, `LogFile` с `using`
- Команда `dispose` в `Program.cs`
- Статья `docs/03-disposable.md`

### Ожидаемый результат

```
1. Финализатор: объект создаётся без ссылки
[FakeResource 1] создан
[FakeResource 2] создан
Принудительный GC, финализатор должен сработать
[FakeResource 1] финализирован
[FakeResource 2] финализирован
После GC.Collect + WaitForPendingFinalizers

2. using: детерминированное Dispose
[ManagedResource 1] создан
Внутри using-блока
[ManagedResource 1] Dispose вызван
После выхода из using-блока

3. Забытый Dispose: объект не освобождается
[ManagedResource 2] создан
Объект создан, IsDisposed = False
Принудительный GC, Dispose не был вызван
После Dispose (FINALIZER не вызван, т.к. нет финализатора)

4. LogFile: запись в файл через using
[LogFile 1] файл открыт: ...
[LogFile 1] файл закрыт
После выхода из using-блока
[LogFile 2] файл открыт: ...
Создан LogFile без Dispose
[LogFile 2] финализатор! - файл не был закрыт Dispose()
После Dispose: финализатор сработал
```

Отчёт: `docs/03-disposable.md`

---

![Скриншот КТ №42 — вывод dispose](docs/screenshots/kt42.png)

---

## КТ №43. Паттерн Dispose. IAsyncDisposable (.NET 10)

Для этапа КТ №43 добавлены:

- `Resources/DatabaseConnection.cs` — вариант 1: `sealed class : IDisposable, IAsyncDisposable` с управляемым ресурсом `List<string>` (журнал запросов) и нативным `int connectionId`
- `Resources/FileBuffer.cs` — вариант 2: `sealed class : IDisposable, IAsyncDisposable` с управляемым ресурсом `MemoryStream` и нативным `int fileHandle`
- Полный паттерн Dispose: `Dispose()` → `Dispose(bool disposing)` → finiilazer `~Class()` → `DisposeAsync()` → `DisposeAsyncCore()`
- `GC.SuppressFinalize(this)` в `Dispose()` и `DisposeAsync()`
- Команды `dispose-v1` и `dispose-v2` в `Program.cs`
- Отчёт: `docs/04-dispose-pattern.md`

### Команда запуска

Соберите проект в Release и запускайте без отладчика:

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```

**Вариант 1 — DatabaseConnection:**

```powershell
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll dispose-v1
```

**Ожидаемый результат:**

```
=== Вариант 1: DatabaseConnection (IDisposable, IAsyncDisposable) ===

1. Синхронный using
[DatabaseConnection 1] соединение открыто
[DatabaseConnection 1] запрос: SELECT * FROM Students
[DatabaseConnection 1] журнал запросов очищен
[DatabaseConnection 1] соединение закрыто

2. Асинхронный await using
[DatabaseConnection 2] соединение открыто
[DatabaseConnection 2] запрос: SELECT * FROM Topics
[DatabaseConnection 2] журнал запросов очищен асинхронно
[DatabaseConnection 2] соединение закрыто

3. Проверка GC.SuppressFinalize после using
[DatabaseConnection 3] соединение открыто
[DatabaseConnection 3] запрос: SELECT 1
[DatabaseConnection 3] журнал запросов очищен
[DatabaseConnection 3] соединение закрыто
После сборки: сообщение финализатора отсутствует

4. Финализатор без Dispose
[DatabaseConnection 4] соединение открыто
[DatabaseConnection 4] запрос: UPDATE Students SET Name = 'Test'
[DatabaseConnection 4] соединение закрыто
После сборки: финализатор освободил только нативный ресурс
```

**Вариант 2 — FileBuffer:**

```powershell
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll dispose-v2
```

**Ожидаемый результат:**

```
=== Вариант 2: FileBuffer (IDisposable, IAsyncDisposable) ===

1. Синхронный using
[FileBuffer 1] буфер открыт
[FileBuffer 1] буфер освобождён
[FileBuffer 1] дескриптор 1 закрыт

2. Асинхронный await using
[FileBuffer 2] буфер открыт
[FileBuffer 2] буфер освобождён асинхронно
[FileBuffer 2] дескриптор 2 закрыт

3. Проверка GC.SuppressFinalize после using
[FileBuffer 3] буфер открыт
[FileBuffer 3] буфер освобождён
[FileBuffer 3] дескриптор 3 закрыт
После сборки: сообщение финализатора отсутствует

4. Финализатор без Dispose
[FileBuffer 4] буфер открыт
[FileBuffer 4] дескриптор 4 закрыт
После сборки: финализатор освободил только нативный ресурс
```

![Скриншот консоли — варианты 1 и 2 КТ №43](docs/2026-11-16_19-02-00.png)

Отчёт: `docs/04-dispose-pattern.md`

---

## 8. Контрольные вопросы

### 1. Какой режим GC используется по умолчанию и как проверить, что режим действительно изменился?

**Ответ:**

По умолчанию проект использует **Workstation GC**. Проверить, что режим действительно изменился, можно выводом `GCSettings.IsServerGC`:

```csharp
Console.WriteLine($"GCSettings.IsServerGC = {GCSettings.IsServerGC}");
```

- `False` — Workstation GC
- `True` — Server GC

Этот вывод присутствует в обеих командах бенчмарка (`gc-pressure` и `gc-modes`) и служит доказательством текущего режима.

---

### 2. Почему Server GC показывает больше Gen0-коллекций (58 против 16) — это ошибка?

**Ответ:**

Это **не ошибка, а особенность архитектуры Server GC**. Server GC использует несколько куч — по одной на логический процессор (12 ядер = до 12 куч). У каждой кучи своё собственное поколение Gen0. Когда 4 потока одновременно создают объекты, они распределяются по разным кучам, и каждая Gen0 заполняется быстрее, чем одна общая куча в Workstation GC. Поэтому суммарное число Gen0-сборок у Server GC выше — это ожидаемое поведение, оптимизированное для пропускной способность (throughput).

---

### 3. Что нужно сделать, чтобы сравнение Workstation и Server GC было корректным?

**Ответ:**

Для корректного сравнения нужно:

1. Собрать в **Release** и запускать **без отладчика** (`dotnet build ... -c Release`).
2. Использовать **одинаковое число потоков (4) и объектов (300 000 на поток)** в обоих режимах.
3. Сделать **2–3 запуска в каждом режиме**, не один.
4. Учесть **прогрев** (warm-up — forced Gen2 сборка в коде бенчмарка).
5. Учесть **фоновую нагрузку** на ПК (браузер, 4K видео).
6. Не делать вывод о деградации из-за большего числа Gen0 у Server GC — объяснить различие через несколько куч.
7. Выводить **`GCSettings.IsServerGC`** в обоих режимах как доказательство изменения режима.
8. Измерять время через **`Stopwatch`**, а не субъективными ощущениями.

---

### 4. Какой результат показывает бенчмарк в обоих режимах и какой вывод из этого следует?

**Ответ:**

- **Workstation GC:** `GCSettings.IsServerGC = False`, Gen0 ≈ 16, Gen1 = 0, Gen2 = 0, время ~10–13 мс, аллокации 146 МБ.
- **Server GC:** `GCSettings.IsServerGC = True`, Gen0 ≈ 58, Gen1 = 0, Gen2 = 0, время ~12–13 мс, аллокации 146 МБ.

**Вывод:**

- Аллокации одинаковы, число потоков одинаково — сравнение корректно.
- Gen0 у Server GC выше из-за нескольких куч — это ожидаемо.
- Время выполнения практически идентично при 4 потоках; Server GC при большем числе потоков и серверной нагрузке обычно выигрывает за счёт параллельных сборок по нескольким кучам.
- Server GC подходит для многопроцессорных серверных систем и пропускной способности.

---

### 5. Какие режимы переключения Server GC показаны в README и какой принцип выбрать для проекта?

**Ответ:**

Показаны два способа:

1. **Через `.csproj`:**
   ```xml
   <ServerGarbageCollection>true</ServerGarbageCollection>
   ```
   — подходит, когда проект самодостаточен и нужно переключение по умолчанию.

2. **Через переменную окружения:**
   ```powershell
   $env:DOTNET_gcServer=1
   ```
   — подходит для тестовых запусков, отладки и временных переключений без изменения кода.

**Принцип выбора:** для серверного приложения (ASP.NET Core, микросервисы) Server GC по умолчанию — повышает пропускную способность. Для клиентских приложений, UI, инструменты — Workstation GC по умолчанию — снижает паузы и чувствительнее к интерактивности. Для измерений и сравнений удобнее использовать переменную окружения.

---

### 6. Какие объекты попадают в LOH и когда Gen2-счётчик увеличивается реально?

**Ответ (по КТ №40):**

- Массивы размером **более 85 КБ** попадают в **LOH** (Large Object Heap).
- Объекты в LOH не инициируют сборку Gen0 и не обязательно увеличивают счётчик Gen2 сразу — Gen2-счётчик увеличивается **только при фактической сборке Gen2**, а не при каждой крупной аллокации.

---

### 7. Какие ошибки возникают при сравнении Workstation и Server GC (типичные ошибки)?

**Ответ (согласно КТ №41, раздел 4.3.5):**

- Сравнение выполняется в конфигурации Debug или с подключённым отладчиком.
- Один запуск объявляется достаточным доказательством; не учитываются прогрев и фоновая нагрузка.
- В Server GC больше Gen0, и студент делает вывод о деградации, не объясняя разницу в количестве куч.
- В двух запусках разное число потоков или объектов, поэтому сравнение недействительно.
- Не выводится `GCSettings.IsServerGC`, и невозможно доказать, что режим действительно изменился.
- Время измеряется без `Stopwatch`, по субъективному ощущению.

---

### 8. Какая структура проекта должна быть на ПК в данный момент и почему `Demos` вынесена на уровень `src/Demos`?

**Ответ:**

Текущая структура (соответствует скриншоту 2):

```
MemoryLifecycle/
├── MemoryLifecycle.sln
├── README.md
├── docs/
│   ├── 01-gc.md
│   ├── 02-gc-modes.md
│   ├── 02-gcmodes.md
│   ├── 03-disposable.md
│   └── 04-dispose-pattern.md
└── src/
    ├── Demos/
    │   ├── DisposeDemos.cs
    │   └── ResourceDemos.cs
    └── MemoryLifecycle/
        ├── MemoryLifecycle.csproj
        ├── Program.cs
        ├── Gc/
        │   ├── GcSnapshot.cs
        │   ├── GcObserver.cs
        │   ├── PressureScenarios.cs
        │   └── GcModeScenarios.cs
        └── Resources/
            ├── FakeResource.cs
            ├── ManagedResource.cs
            ├── LogFile.cs
            ├── DatabaseConnection.cs
            └── FileBuffer.cs
```

**Почему `Demos` на уровне `src/Demos`:**

Требование 4.4 для КТ №41 гласит, что в проект добавляется файл `Gc/GcModeScenarios.cs`. Файлы демонстраций (`DisposeDemos.cs`, `ResourceDemos.cs`) были перемещены из `src/MemoryLifecycle/Demos/` в `src/Demos/` при исправлении структуры проекта (скриншот 2), чтобы Démо файлы не были внутри папки проекта `MemoryLifecycle`, а находились на уровне решения `src/`, параллельно с проектом. Это улучшает организацию решения: `src/` содержит все демонстрационные файлы и сам проект.

**Примечание по именам:**

- Файл `src/MemoryLifecycle/Resources/logfile.cs` переименован в `LogFile.cs` (с большой буквы) по соглашению именования C# файлов.
- В папке `Gc` оба файла присутствуют: `GcModeScenarios.cs` (для КТ №41) и `PressureScenarios.cs` (для КТ №40).

---

### 9. Как выглядит вывод бенчмарка в режиме Workstation GC и Server GC?

**Ответ:**

Вывод бенчмарка `gc-pressure`:

```
=== GC Benchmark: Workstation vs Server GC ===
GCSettings.IsServerGC = False
Processor count: 12
Threads: 4
Objects per thread: 300,000
Object size: 100 bytes
Total allocated (approx): 114,44 MB

=== Results ===
GCSettings.IsServerGC = False
Elapsed time: 10 ms
Gen0 collections: 16
Gen1 collections: 0
Gen2 collections: 0
Total allocated bytes: 146,00 MB
```

Для Server GC: `GCSettings.IsServerGC = True`, Gen0 = 58, остальные значения аналогичны.

Команды запуска:

```powershell
# Workstation GC
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll gc-pressure

# Server GC
$env:DOTNET_gcServer=1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll gc-pressure
```

---

### 10. Какие команды доступны в проекте после КТ №41?

**Ответ:**

- `dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll gc-pressure` — BENCHMARK Workstation vs Server GC (КТ №40)
- `dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll gc-modes` — сценарии работы с режимами GC (КТ №41)

Соберите в Release перед запуском:

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```
