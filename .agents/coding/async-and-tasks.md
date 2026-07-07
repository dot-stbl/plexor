---
description: c# async/await, Task vs ValueTask, CancellationToken — асинхронность
globs: ["**/*.cs"]
always: true
---

# Async / await

Этот файл — асинхронность целиком: паттерны, типы, токены отмены,
обработка ошибок. Naming (async suffix) — в `naming-and-types.md` §1.
ConfigureAwait ban — там же.

## 1. Async suffix — REQUIRED, без исключений

Все методы с `Task` / `ValueTask` / `Task<T>` / `ValueTask<T>` в return
type оканчиваются на `Async`. Метод `DoAsync` без await — всё равно `Async`.

```csharp
public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
{
    return repository.CountAsync(cancellationToken);
}

public async Task<User> GetUserByIdAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
{
    // ...
}
```

**Enforcement:** VSTHRD200 (severity=error).

---

## 2. `Task` vs `ValueTask`

- **`Task<T>`** — операция всегда асинхронная (IO, БД, HTTP).
- **`ValueTask<T>`** — операция **может** быть синхронной (кеш-хит, материализованное значение).

```csharp
// ✅ ValueTask — кеш может вернуть синхронно
public async ValueTask<User> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
{
    if (cache.TryGet(userId, out var cached))
    {
        return cached;
    }

    return await loader.LoadAsync(userId, cancellationToken);
}
```

---

## 3. `ConfigureAwait(false)` — ЗАПРЕЩЁН в app code

В .NET 8+ async/await не имеет накладных расходов на `SynchronizationContext`
capture в ASP.NET Core (где `SynchronizationContext` отсутствует).
Добавление `.ConfigureAwait(false)` — лишний шум без пользы.

```csharp
// ❌ Wrong — устаревший паттерн из .NET 4.x
await repository.GetUserAsync(id, cancellationToken).ConfigureAwait(false);

// ✅ Correct — голый await, .NET 8+ оптимизирован
await repository.GetUserAsync(id, cancellationToken);
```

**Где `ConfigureAwait(false)` ещё оправдан** (в этом проекте не встречается):
- Library code (NuGet packages).
- Bcl analyzers (CA2007) — **требуют** для libraries.

**Каноническая позиция проекта:** app code (ASP.NET Core, console hosts,
workers) — `await` без `ConfigureAwait`.

**Enforcement:** MA0004 (severity=error в `.editorconfig`).

---

## 4. CancellationToken — последний параметр везде

Все методы с IO принимают `CancellationToken cancellationToken = default`
**последним параметром**.

```csharp
public async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
{
    return await repository.GetByIdAsync(userId, cancellationToken);
}
```

Прокидывай `cancellationToken` во **все** вложенные async-вызовы. Никаких
`Task.Delay(...)` без токена.

---

## 5. Async запреты

**Запрещено** в app code:

```csharp
// ❌ .Result — dead-lock риск, VSTHRD103
var user = repository.GetUserAsync(id).Result;

// ❌ .Wait() — то же самое
repository.GetUserAsync(id).Wait();

// ❌ .GetAwaiter().GetResult() — то же самое
repository.GetUserAsync(id).GetAwaiter().GetResult();
```

**Enforcement:** VSTHRD103 (severity=error).

---

## Связанные правила

- `naming-and-types.md` — naming (async suffix)
- `code-shape.md` — общий code shape
- `di-lifetimes.md` — DI lifetimes для scoped/transient сервисов