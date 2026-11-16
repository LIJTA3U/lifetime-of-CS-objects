# 04. Паттерн Dispose и IAsyncDisposable — отчёт по КТ №43

## Цель

Реализовать полный паттерн Dispose с поддержкой синхронного (`IDisposable`) и асинхронного (`IAsyncDisposable`) освобождения ресурсов. Показать разницу между детерминированным (`Dispose`) и недетерминированным (`~Finalize`) освобождением, а также роль `GC.SuppressFinalize`.

## Реализация (варианты 1 и 2)

| Файл | Класс | Вариант | Управляемый ресурс | Нативный ресурс |
|------|-------|---------|-------------------|-----------------|
| `Resources/DatabaseConnection.cs` | `DatabaseConnection : IDisposable, IAsyncDisposable` | 1 | `List<string> queryLog` | `int connectionId` |
| `Resources/FileBuffer.cs` | `FileBuffer : IDisposable, IAsyncDisposable` | 2 | `MemoryStream? buffer` | `int fileHandle` |

## Структура паттерна Dispose (оба класса)

```
Dispose()                          // публичный, детерминированный
  → Dispose(disposing: true)       // управляемые + нативные ресурсы
    → GC.SuppressFinalize(this)    // убирает из очереди финализации

DisposeAsync()                     // публичный, асинхронный
  → DisposeAsyncCore()             // асинхронная очистка управляемых
    → Dispose(disposing: false)    // нативные ресурсы
    → GC.SuppressFinalize(this)

~ClassName()                       // финализатор, недетерминированный
  → Dispose(disposing: false)      // только нативные ресурсы
```

## Демонстрационные сценарии

### Сценарий 1 — Синхронный `using`

Объект создаётся в `using`-блоке. При выходе из блока вызывается `Dispose()` → `Dispose(true)`, управляемые ресурсы освобождаются, нативный дескриптор закрывается, `GC.SuppressFinalize` предотвращает вызов финализатора.

### Сценарий 2 — Асинхронный `await using`

Объект создаётся в `await using`-блоке. При выходе вызывается `DisposeAsync()` → `DisposeAsyncCore()` (с `await Task.Delay(10)` для имитации асинхронного сброса), затем `Dispose(false)` для нативных ресурсов и `GC.SuppressFinalize`.

### Сценарий 3 — Проверка `GC.SuppressFinalize`

После `using` вызывается `GC.Collect()` + `GC.WaitForPendingFinalizers()`. Финализатор **не** срабатывает — отсутствие сообщения из `~ClassName()` подтверждает, что объект удалён из очереди финализации.

### Сценарий 4 — Финализатор без `Dispose`

Объект создаётся без `Dispose()`. После `GC.Collect()` + `GC.WaitForPendingFinalizers()` срабатывает финализатор `~ClassName()` → `Dispose(false)`. Освобождается только нативный ресурс (управляемый уже может быть собран GC к моменту финализации).

## Ключевые моменты

- **`Dispose(bool disposing)`** — разделение веток: при `disposing == true` освобождаются управляемые ресурсы, нативные — всегда.
- **`GC.SuppressFinalize(this)`** — вызывается и в `Dispose()`, и в `DisposeAsync()` после успешной очистки.
- **`DisposeAsyncCore()`** — асинхронная часть очистки, вызывается из `DisposeAsync()` через `try/finally`, чтобы нативный ресурс закрылся даже при исключении.
- **`ConfigureAwait(false)`** — уместен в библиотечном коде, где не требуется возврат в исходный контекст синхронизации.
- **`sealed` классы** — `Dispose(bool)` объявлен `private` (в наследуемых классах должен быть `protected virtual`).

## Файлы реализации

- `src/MemoryLifecycle/Resources/DatabaseConnection.cs` — вариант 1
- `src/MemoryLifecycle/Resources/FileBuffer.cs` — вариант 2
- `src/MemoryLifecycle/Program.cs` — команды `dispose-v1` и `dispose-v2`

## Запуск

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll dispose-v1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll dispose-v2
```

Скриншот консоли со результатами запуска обоих вариантов — в этом же отчёте (см. вывод выше в разделе «Демонстрационные сценарии»).
