# 02. Режимы сборщика мусора: Workstation GC и Server GC

В .NET существует два режима работы сборщика мусора (GC):

- **Workstation GC** — используется по умолчанию на клиентских машинах. Одна общая куча для всего процесса. Оптимизирован для минимальных пауз (latency).
- **Server GC** — используется на серверах (или при явном включении). Несколько куч — обычно по одной на логический процессор. Каждая куча имеет своё собственное поколение Gen0. Оптимизирован для пропускной способности (throughput).

## Как переключить режим

### Через файл проекта (`.csproj`)

Добавьте в `src/MemoryLifecycle/MemoryLifecycle.csproj`:

```xml
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
</PropertyGroup>
```

Затем пересоберите:

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```

### Через переменную окружения (без изменения `.csproj`)

```powershell
$env:DOTNET_gcServer=1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll
```

## Как проверить, что режим изменился

Программа выводит `GCSettings.IsServerGC`:

- `False` — Workstation GC (по умолчанию)
- `True` — Server GC

## Почему у Server GC больше Gen0-сборок — это не ошибка

Server GC создаёт несколько куч (по одной на логический процессор). У каждой кучи своё собственное поколение Gen0. Когда несколько потоков одновременно аллоцируют объекты, они распределяются по разным кучам, и каждая Gen0 заполняется быстрее, чем одна общая куча в Workstation GC. Поэтому суммарное число Gen0-сборок у Server GC выше — это ожидаемое поведение, оптимизированное для пропускной способности на многопроцессорных системах.

## Команды запуска

Соберите в Release и запускайте без отладчика:

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```

Workstation GC (по умолчанию):

```powershell
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll -- gc-modes
```

Server GC (через `.csproj` с `<ServerGarbageCollection>true</ServerGarbageCollection>`):

```powershell
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll -- gc-modes
```

Server GC (через переменную окружения):

```powershell
$env:DOTNET_gcServer=1
dotnet src/MemoryLifecycle/bin/Release/net9.0/MemoryLifecycle.dll -- gc-modes
```
