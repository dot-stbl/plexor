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

**Главное правило:** суффикс ОБЯЗАН описывать роль / ответственность класса или DTO, не generic-категорию. Если суффикс не добавляет информации о том ЧТО класс делает — он запрещён.

**Запрещённые generic-категории** (описывают pattern, не роль — ВСЕГДА):
- `*Dto` — запрещён. Каждый Web API класс — это "data transfer object" в широком смысле. Суффикс не описывает роль. Используй `*Request` / `*Response` / `*Summary` / `*Detail` / etc., что говорит о позиции в DTO-флоу
- `*Model` — запрещён в DTO-имени (см. `*Dto`); ДОПУСТИМ как `UserEntity` в DDD-контексте, но не в DTO-имени
- `*ViewModel` — запрещён всегда (тот же generic-pattern антипаттерн как `*Dto`, но ViewModel ещё и утечка MVVM-pattern'а в наш CQRS/DDD-стек)
- `*Impl` — Java-стиль, не нужен в C#

**Разрешённые role-specific суффиксы** (описывают роль, не категорию):
- `*Manager` — управляет чем-то: `EntityManager`, `ConnectionManager`, `ClusterManager`
- `*Helper` — помогает с чем-то: `HttpHelper`, `JsonHelper`
- `*Utility` / `*Util` — утилита для чего-то: `StringUtility`, `PathUtil`
- `*Service` — сервис чего-то: `PaymentService`, `TenantService`
- `*Scheduler`, `*Authenticator`, `*Calculator` — конкретная ответственность

**Суффиксы DTO / response envelope / query / event:**
| Суффикс | Когда использовать |
|---------|------------------|
| `*Request` | DTO входящего HTTP-запроса (body) |
| `*Response` | DTO исходящего HTTP-ответа (body) |
| `*Result` | результат внутренней операции (service / validator / executor) |
| `*Spec` | DDD Specification — query criteria, immutable, composable |
| `*Query` | CQRS-query объект (запрос на read-сторону) |
| `*Command` | CQRS-command объект (запрос на write-сторону) |
| `*Notification`, `*Event` | DDD domain event |
| `*Handler` | конкретный command/query handler, названный по тому что обрабатывает |
| `*Entity` | ДОПУСТИМ в DDD (EF model), не в DTO. `UserEntity` ОК, `UserEntityDto` — НЕ ОК |

`UserDto` — **запрещено**. `UserModel` — **запрещено** в DTO-имени (хоть и в GOOD колонке в исходном примере, это ОШИБКА исходного правила — `Model` такой же generic как `Dto`).

**Хорошие имена говорят роль / позицию:**
- `TaskRequest` (HTTP DTO запроса) vs `TaskResult` (результат внутренней операции) — НЕ `TaskDto`/`TaskModel`/`TaskService`
- `TenantSummary` (projection для list) vs `TenantDetail` (projection для single) — НЕ `TenantDto`
- `NodeHardware` (value object) — имя описывает что представляет, не generic
- `EntityManager` (управляет entities) — НЕ `Manager` в generic смысле, а конкретная роль

**`Response` vs `Result` различие:**
- `*Response` — DTO исходящего HTTP body (используется в `ProducesResponseType<T>` контроллера)
- `*Result` — internal operation result (метод сервиса / валидатора / executor'а)

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

## Self-audit grep

Перед коммитом — запрещённые суффиксы (case-insensitive, `\b` word boundary):

```bash
# Class / record names ending in *Dto — the WORST offender (describes
# implementation pattern, not role; every Web API class is a DTO)
rg -in "(class|record)\s+\w+Dto\b" src/ --type cs

# Class names ending in *Model, *Impl — other generic categories
rg -in "class\s+\w+(Model|Impl)\b" src/ --type cs

# Forbidden generic parameter name (use 'cancellationToken' not 'ct')
rg -n " ct\b" src/ --type cs

# Underscore-prefixed private fields (forbidden; use primary ctor)
rg -n "private\s+\w+_\w+\s*=" src/ --type cs
```

Любой результат grep'а = потенциальный violation. Перед merge'ем каждый результат
либо fixed (переименовать), либо подавлен в комментарии с обоснованием
почему конкретное использование допустимо.