---
description: c# naming, sealed classes, record vs class, no redundant namespace prefix — type declaration rules
globs: ["**/*.cs"]
always: true
---

# Naming, sealed, record vs class, type references

Этот файл — правила декларации типов и имён. Логика конструкторов,
паттерн-матчинг, async, anti-patterns — в соседних файлах.

## 1. Naming

### Общие правила

- **Interfaces**: префикс `I` — `ISchedulerManager`, `IConnectionManager`.
- **Без сокращений**: `user`, `configuration`, `request` — не `usr`, `cfg`, `req`.
- **Methods**: verb phrases — `CreateUserAsync`, `GetFilteredAsync`.
- **Access modifiers**: всегда явно (включая `public` на членах интерфейса).
- **Lambda parameters**: осмысленные имена. **Никогда** одной буквы.
  Исключение — `_` (discard) для неиспользуемых параметров.

**Test:** при виде `u`, `x`, `tmp`, `req`, `resp`, `ct` — переименовать.

### Postfixes

```csharp
// ✅ Correct — описательные суффиксы по назначению
public sealed record UserModel { ... }
public sealed record TaskRequest { ... }
public sealed record TaskResponse { ... }
public sealed record HealthCheckResult { ... }
public sealed class TaskScheduler { ... }
public sealed class UserAuthenticator { ... }

// ❌ Wrong — generic постфикс не несёт смысла
public sealed record UserDto { ... }
public sealed class TaskService { ... }
public sealed class DataHelper { ... }
```

**`Response` vs `Result`:**
- `*Response` — DTO ответа HTTP-эндпоинта.
- `*Result` — результат внутренней операции (сервис, валидатор).

### Async suffix

Все методы с `Task` / `ValueTask` / `Task<T>` / `ValueTask<T>` в return
type оканчиваются на `Async`. Без исключений. Простой проброс Task тоже
получает суффикс.

**Enforcement:** VSTHRD200 (severity=error).

### `Task` vs `ValueTask`

- **`Task<T>`** — IO, БД, HTTP (всегда async).
- **`ValueTask<T>`** — может быть синхронной (кеш-хит).

### `ConfigureAwait` — запрещён в app code

```csharp
// ❌ Wrong — устаревший паттерн из .NET 4.x
await repository.GetUserAsync(id, cancellationToken).ConfigureAwait(false);

// ✅ Correct — .NET 8+ async/await не имеет накладных
await repository.GetUserAsync(id, cancellationToken);
```

**Enforcement:** MA0004 / CA2007.

### Parameter naming

| Bad | Good |
|-----|------|
| `ct` | `cancellationToken` |
| `sp` | `serviceProvider` |
| `id` | `userId`, `taskId`, `orderId` |
| `req` | `request` |
| `resp` | `response` |
| `msg` | `message` |
| `err` | `error` |

Исключение: переменная цикла с коротким скоупом — `foreach (var item in items)`.

### Lambda parameters

```csharp
// ✅ Correct
users.Where(user => user.IsActive)
     .Select(activeUser => activeUser.Id)
     .ToList();

// ❌ Wrong
users.Where(u => u.IsActive)
     .Select(x => x.Id)
     .ToList();
```

**Исключение для `_` (discard):** для неиспользуемых параметров.

```csharp
// ✅ Discard для неиспользуемого параметра
await operation.RunAsync(async _ => { ... });
```

### Private fields — NO underscore prefix

```csharp
// ✅ Best — primary constructor, параметр доступен по имени
public sealed class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    public Task DoAsync() => repository.GetByIdAsync(...);
}

// ❌ Wrong — explicit ctor + дублирующее private readonly
public sealed class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly ILogger<UserService> _logger;
    // ... + копирование в ctor
}
```

Имя параметра primary constructor — это уже поле. Не дублировать.

---

## 2. `sealed` — REQUIRED на concrete classes

**Default:** `sealed` на каждом конкретном классе.

**Исключения (только эти два):**
1. `abstract` базовый класс.
2. Класс с документированной точкой расширения — наследник **существует** в кодовой базе или design-decision зафиксирован в ADR.

```csharp
// ✅ Default
public sealed class UserService(IUserRepository repository) { ... }

// ✅ Abstract база — не sealed
public abstract class WorkerBase : IHostedService { ... }

// ✅ Открыт сознательно — есть наследники
public class DomainEvent { ... }
public sealed class OrderCreated : DomainEvent { ... }
```

**Enforcement:** CA1852 (severity=error в `.editorconfig`).
**Test:** при виде не-`sealed` concrete class — спросить "есть наследник или ADR?".

---

## 4. Record vs class

**Default — `sealed class`.**

`record` используется **только** когда явно нужны:
- Value-equality.
- `with`-expressions.
- Сжатый синтаксис для immutable value-объектов.

```csharp
// ✅ Value object — record оправдан
public sealed record Money(decimal Amount, string Currency);

// ✅ EF entity — class (не record)
[Table(DatabaseInformation.Tables.OrderBook, Schema = ...)]
public sealed class OrderBook : IExchangeObject, IUpdatedEntity { ... }

// ✅ DTO без value-equality — class
public sealed class CreateOrderRequest
{
    public required string Symbol { get; init; }
    public required decimal Quantity { get; init; }
}
```

### Required members

```csharp
public required string DeclarationId { get; init; }
```

---

## 16. Type references — без redundant namespace prefix

Если тип уже в скоупе (текущий namespace или `using`) — **не** дублируй
namespace-prefix.

```csharp
namespace Acme.Shop.Orchestration;  // parent namespace

using Acme.Shop.Orchestration.Interfaces;

// ❌ Wrong — префикс избыточен
public sealed class NoOpOrchestrationService : Interfaces.IOrchestrationService

// ✅ Correct — IOrchestrationService уже в скоупе
public sealed class NoOpOrchestrationService : IOrchestrationService
```

Применимо ко всем type-references: сигнатуры классов, параметры primary
constructor, generic-аргументы, возвращаемые типы, field/property types.

**Префикс остаётся** (не redundant) когда:
- Тип НЕ в скоупе — добавить `using` или полный путь.
- `Type.Member` — статический member access.
- Вложенный тип `MyClass.NestedType` standalone не доступен.
- Disambiguation — в текущем скоупе есть другой тип с тем же именем.

**Test:** перед коммитом grep `<Имя>` где `<Имя>` — namespace из текущего проекта.

---

## Связанные правила

- `constructors-and-fields.md` — primary constructor, private fields, constants
- `code-shape.md` — pattern matching, var, braces, XML docs
- `async-and-tasks.md` — async/await
- `anti-patterns.md` — enum anti-patterns, tuple ban, validation