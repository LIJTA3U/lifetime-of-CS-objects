# 03. IDisposable — отчёт по КТ №42

## Цель

Показать разницу между детерминированным освобождением ресурсов через `IDisposable` / `using` и недетерминированной финализацией через `~Finalize`. Показать, почему `GC.SuppressFinalize` нужен после успешного `Dispose`.

## Реализация (соответствует методичке КТ №42, раздел 5.2)

| Файл | Класс | Назначение |
|------|-------|------------|
| `Resources/FakeResource.cs` | `FakeResource` | Класс с финализатором `~FakeResource`, без `IDisposable` |
| `Resources/ManagedResource.cs` | `ManagedResource : IDisposable` | Класс с детерминированным `Dispose()` и флагом `disposed` |
| `Resources/LogFile.cs` | `LogFile : IDisposable` | Класс с `StreamWriter`, `GC.SuppressFinalize(this)` и финализатором |
| `Demos/ResourceDemos.cs` | `ResourceDemos` | Четыре демо-сценария |

## Демонстрационные сценарии

### 1. Финализатор (`RunFinalizerDemo`)

Создаются экземпляры `FakeResource` без ссылок. Вызывается `GC.Collect()` + `GC.WaitForPendingFinalizers()`. Финализатор `~FakeResource` срабатывает — это证明, что GC最終 cleanup выполняет финализаторы для объектов без `IDisposable`.

### 2. `using` (`RunUsingDemo`)

`ManagedResource` создаётся внутри `using`-блока. При выходе из блока автоматически вызывается `Dispose()`. Ресурс освобождается детерминированно, сразу после выхода из области видимости.

### 3. Забытый `Dispose` (`RunForgottenDisposeDemo`)

`ManagedResource` создаётся без `using` и без вызова `Dispose()`. После `GC.Collect()` + `GC.WaitForPendingFinalizers()` финализатор **не** срабатывает, потому что у `ManagedResource` нет финализатора — только `IDisposable`. Объект остаётся в памяти до следующего полного цикла GC.

### 4. `LogFile` с `using` и без (`RunLogFileDemo`)

- Первый `LogFile` создаётся в `using`-блоке — `Dispose()` вызывается автоматически, файл закрывается детерминированно, `GC.SuppressFinalize(this)` предотвращает вызов финализатора.
- Второй `LogFile` создаётся без `Dispose()` — срабатывает финализатор `~LogFile`, выводится предупреждение, что файл не был закрыт через `Dispose()`.

## Ключевые моменты

- **`IDisposable`** — контракт для детерминированного освобождения ресурсов (файлы, сокеты, дескрипторы).
- **`using`** — гарантирует вызов `Dispose()` даже при исключении.
- **`~Finalize`** — резервный механизм, вызываемый GC неопределённое время после того, как объект стал недостижим.
- **`GC.SuppressFinalize(this)`** — убирает объект из очереди финализации после успешного `Dispose()`, избавляя от лишнего цикла GC.
- **`GC.KeepAlive`** — предотвращает удаление объекта JIT-компилятором до конца метода, чтобы финализатор не вызвался преждевременно.

## Файлы

- `src/MemoryLifecycle/Resources/FakeResource.cs`
- `src/MemoryLifecycle/Resources/ManagedResource.cs`
- `src/MemoryLifecycle/Resources/LogFile.cs`
- `src/MemoryLifecycle/Demos/ResourceDemos.cs`
- `src/MemoryLifecycle/Program.cs` — команда `dispose`
