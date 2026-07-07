---
description: c# anti-patterns — enum-with-behavior, enum-as-command, switch sprawl, records DTO placement, tuples ban, fluentvalidation, [FromServices], JsonSerializerOptions
globs: ["**/*.cs"]
always: true
---

# Anti-patterns — ЗАПРЕТЫ и обязательные замены

Этот файл — то, что **нельзя** делать, и обязательные замены. Покрывает
самые частые нарушения, которые LLM делает по умолчанию.

Naming/sealed/records-as-types — в `naming-and-types.md`. Code shape
(pattern matching, var, braces) — в `code-shape.md`. Async — в `async-and-tasks.md`.

## 1. Enum anti-patterns — три формы

Domain enum — закрытый набор меток (status, type, scope, op). Становится
антипаттерном в трёх повторяющихся ситуациях.

### 1.1 Enum-with-behavior → извлечь value object или extension

Enum несёт **неявные данные** (длительность, размер, лимит) и вынуждает
потребителей писать `switch(enum)` чтобы их восстановить.

```csharp
// ❌ Wrong — FrequencyCapPeriod + PeriodHours вшит в xml-комментарии
public enum FrequencyCapPeriod
{
    /// <summary>24 hours.</summary> Day = 0,
    /// <summary>168 hours (7 days).</summary> Week = 1,
}
public sealed record FrequencyCapSetting
{
    public required FrequencyCapPeriod Period { get; init; }
    public int PeriodHours => Period == FrequencyCapPeriod.Day ? 24 : 168;
}

// И дубль в TargetingContributors.cs:
return setting.Period == FrequencyCapPeriod.Day ? 24 : 168;
```

Два потребителя, два одинаковых switch-а, длительность живёт не там где
должна.

**Замена (минимум-churn):**

```csharp
// ✅ Behavior на enum — extension method
public static class FrequencyCapPeriodExtensions
{
    public static int AsHours(this FrequencyCapPeriod p) => p switch
    {
        FrequencyCapPeriod.Day  => 24,
        FrequencyCapPeriod.Week => 168,
        _ => throw new ArgumentOutOfRangeException(nameof(p), p, null),
    };
}
```

**Замена (когда длительность — реальный domain-инвариант):**

```csharp
// ✅ Value object
public sealed record FrequencyCap(FrequencyCapPeriod Period)
{
    public TimeSpan AsTimeSpan() => Period switch
    {
        FrequencyCapPeriod.Day  => TimeSpan.FromHours(24),
        FrequencyCapPeriod.Week => TimeSpan.FromHours(168),
        _ => throw new ArgumentOutOfRangeException(...),
    };
}
```

**Выбирай A** когда enum остаётся wire-формат меткой (DTO / EF column).
**Выбирай B** когда длительность часть бизнес-инвариантов.

### 1.2 Enum-as-command → один command на операцию

Controller принимает `enum Action` и диспатчит через большой switch —
flag-based dispatch hub, не доменная операция. Каждый `case` концептуально
другая команда, но они слиты и не могут быть авторизованы, валидированы,
аудитированы независимо.

```csharp
// ❌ Тот же switch в CampaignHandlers.cs
public async ValueTask<Unit> HandleAsync(ApplyCampaignStatusCommand command, ...)
{
    switch (command.Action)
    {
        case CampaignStatusAction.SubmitForModeration: ...
        case CampaignStatusAction.Approve:             ...
        case CampaignStatusAction.Reject:              ...
        case CampaignStatusAction.Pause:               ...
        case CampaignStatusAction.Resume:              ...
        case CampaignStatusAction.Complete:            ...
    }
}
```

**Замена — каждая операция = свой command/request/handler:**

```csharp
public sealed record ApproveCampaignCommand(CampaignId Id, string ModeratorNote)
    : ICommand<Unit>;
public sealed class ApproveCampaignHandler(CampaignRepository repo, ...) : ...
{
    public ValueTask<Unit> HandleAsync(ApproveCampaignCommand c, ...) { ... }
}
```

Wire-format (HTTP) может всё ещё expose один endpoint, принимающий verb в
body — controller парсит verb и диспатчит в нужный command. Это controller
concern, не domain.

### 1.3 Switch sprawl → полиморфизм

`switch(enum)` с 4+ ветками, каждая из которых **выполняет логику**, не
просто `return X`. Добавление нового enum-значения молча ломает switch
(нет compile-time reminder). Каждый новый caller копирует switch.

**Замена — state pattern:**

```csharp
public abstract class CampaignState
{
    public abstract CampaignState SubmitForModeration(Campaign c);
    public abstract CampaignState Approve(Campaign c, string note);
    public abstract CampaignState Reject(Campaign c, string reason);
    public abstract CampaignState Pause(Campaign c);
}
public sealed class DraftState : CampaignState { ... }
public sealed class ActiveState : CampaignState { ... }

// В handler — никакого switch:
public async ValueTask<Unit> HandleAsync(ApproveCampaignCommand c, ...)
{
    var campaign = await repo.GetAsync(c.Id, ct);
    campaign.ApplyTransition(Transition.Approve(c.ModeratorNote));
    await repo.SaveAsync(campaign, ct);
}
```

**Rule of thumb.** Если `switch(myEnum)` имеет 4+ веток и каждая больше
2 строк логики (не просто `return X`) — это state-machine smell. Новое
значение enum должно требовать **новый код**, не редактирование существующего
switch.

### 1.4 Когда enum — это ОК

- Closed set labels без поведения — `Currency`, `AccountStatus`, `FolderScope`.
- Wire-format / EF column — JSON `status: "Active"`, integer column.
- `[Flags]` bitmask — `Permissions { Read = 1, Write = 2, ... }`.

Правило срабатывает когда enum несёт **поведение** или **диспатчит workflow**.
Plain labels остаются enum-ами.

---

## 2. Records DTO — отдельный файл, не в controller

Request/Response record-DTO (wire-format) **никогда** не объявляются внутри
controller-файла. Каждый record — отдельный файл, в `Application/Models/`
подпапке соответствующего модуля.

```csharp
// ❌ Wrong — record в конце controller-файла
[ApiController]
[Route($"{ApiRoutes.Base}/workspaces")]
public sealed class WorkspacesController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost(Name = "workspaces-create")]
    public async Task<...> CreateAsync(...) { ... }
    // ... 6 action-методов ...
}

public sealed record CreateWorkspaceRequest(ObjectId AgencyId, string Name);
public sealed record CreateWorkspaceResponse(ObjectId Id);
public sealed record EnableWorkspaceFeatureRequest(string FeatureKey);
public sealed record GetWorkspaceResponse(ObjectId Id, string Name, DateTimeOffset CreatedAt);
```

```csharp
// ✅ Correct — record в Application/Models/
// Application/Models/CreateWorkspaceRequest.cs
namespace Hybrid.Modules.Tenants.Application.Models;

public sealed record CreateWorkspaceRequest(ObjectId AgencyId, string Name);
```

**Почему:**
1. Размер файла — CampaignsController с 7 records = 350+ строк, тяжело навигировать.
2. Поиск — хочешь найти `CreateCampaignRequest` — идёшь в `Models/`.
3. Cohesion records — `CreateXxxRequest` + `CreateXxxResponse` живут парой.
4. Reuse — record в отдельном файле, import одинаковый из любого места.
5. Wire-format stability — OpenAPI генератор читает тип из одного места.

**Namespace:** `Hybrid.Modules.<X>.Application.Models`.
**Имя файла = имя типа** в PascalCase.

**Enforcement:** convention + `worker-audit.md`. Нет analyzer'а.
**Test:** `grep -nE '^public (sealed )?record ' <Controller>.cs` — должно быть пусто.

---

## 3. Tuples — ЗАПРЕТ в public API

`ValueTuple` / `(TypeA Type1, TypeB Type2)` запрещены в **public API**:
return types, parameters, properties, fields.

```csharp
// ❌ Wrong — tuple в public API: анонимный, без семантики
public (ApiKey Key, string Plaintext) IssueApiKey(string name) { ... }

// caller:
var (key, plaintext) = user.IssueApiKey("bot");
// Что такое plaintext? Имя не говорит.

// ✅ Correct — record с именем типа и осмысленными именами полей
public sealed record IssuedApiKey(ApiKey Key, string Plaintext)
{
    public string MaskedPlaintext => $"{Plaintext[..4]}...";  // поведение!
}

public IssuedApiKey IssueApiKey(string name) { ... }
```

**Почему tuples — антипаттерн в public API:**
1. Анонимность — `(ApiKey, string)` не имеет имени типа.
2. Нет семантики — `string` во второй позиции — что это?
3. Не расширяется — добавить третье поле = breaking change.
4. Нет поведения — record может иметь computed properties.
5. Сериализация — `System.Text.Json` пишет `{"Item1":..., "Item2":...}`.

**Где tuples допустимы:**
- Intermediate LINQ projections внутри метода.
- Dictionary keys с составным ключом.
- Private helper return types внутри одного файла.

```csharp
// ✅ Tuple внутри метода
var pairs = items.Select(item => (item.Id, item.Name))
                 .Where(p => p.Name.Length > 0);
```

**Test:** `grep -nE 'public\s+\([A-Z]\w+,.*\)\s+\w+' *.cs` — должно быть пусто.

---

## 4. Validation — два слоя, FluentValidation, переиспользуемые правила

**Валидация входных данных — только через FluentValidation.**
`ModelState.AddModelError` запрещён **полностью** — никаких исключений,
даже для `[FromQuery]` cross-field.

### Два слоя

| Слой | Что проверяет | Как | Когда |
|------|--------------|-----|-------|
| **FluentValidation** (Application) | Structural — non-empty, max length, format, range | `AbstractValidator<T>` + `ValidationBehavior` в CQRS pipeline | До handler'а |
| **Domain factory** (Domain) | Semantic — business invariants, state transitions | `throw DomainException` | Внутри aggregate |

### Когда валидатор ОБЯЗАН

```csharp
// ✅ ОБЯЗАН — есть user-input поля
public sealed record CreateCampaignCommand(
    ObjectId AdvertiserId,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal Bid)
    : ICommand<CampaignId>;

// ✅ НЕ НУЖЕН — только ID
public sealed record DeleteCampaignCommand(CampaignId Id) : ICommand<Unit>;
```

**Правило:** если command содержит хотя бы одно **user-editable** поле —
валидатор обязан.

### Переиспользуемые правила — два механизма

**Механизм 1: Rule-объекты** (`AbstractValidator<T>` + `SetValidator`).
Для правил которые **переиспользуются 3+ раз** и имеют собственные
константы. Живут в `Kernel/Cqrs/Validation/Rules/`.

```csharp
// src/shared/Hybrid.Shared.Kernel/Cqrs/Validation/Rules/NameRule.cs
public sealed class NameRule : AbstractValidator<string>
{
    public const int MaxLength = 256;

    public NameRule()
    {
        RuleFor(name => name)
            .NotEmpty()
            .WithMessage("name must not be empty or whitespace.")
            .MaximumLength(MaxLength)
            .WithMessage($"name must be {MaxLength} characters or fewer.");
    }
}
```

Использование — одна строка:

```csharp
public sealed class CreateWorkspaceValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceValidator()
    {
        RuleFor(static command => command.Name).SetValidator(new NameRule());
    }
}
```

**Механизм 2: Extension methods.** Для простых параметризуемых правил.

```csharp
// src/shared/Hybrid.Shared.Kernel/Cqrs/Validation/ValidationExtensions.cs
public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeAllowedVerb<T>(
        this IRuleBuilder<T, string> rule, IReadOnlySet<string> allowed)
    {
        return rule
            .NotEmpty()
            .Must(allowed.Contains)
            .WithMessage(x => $"Unknown action '{x}'. Expected one of: {string.Join(", ", allowed)}.");
    }
}
```

### ModelState — ЗАПРЕЩЁН полностью

```csharp
// ❌ Wrong — даже для query params
if (to < from)
{
    ModelState.AddModelError("to", "'to' must not precede 'from'.");
    return ValidationProblem(ModelState);
}
```

Решение — обернуть query params в request record + FluentValidation:

```csharp
public sealed record CampaignDailyQuery(
    ObjectId CampaignId,
    DateOnly From,
    DateOnly To);

public sealed class CampaignDailyQueryValidator : AbstractValidator<CampaignDailyQuery>
{
    public CampaignDailyQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("'to' must not precede 'from'.");
    }
}
```

### Регистрация

Каждый Application модуль регистрирует валидаторы:

```csharp
_ = services.AddValidatorsFromAssembly(
    typeof(<Module>ApplicationInstaller).Assembly,
    includeInternalTypes: true);
```

**Файл валидатора:** `<ValidatedType>Validator.cs`, рядом с валидируемым типом.

**Test:** `grep -rnE 'ModelState\.AddModelError' src/modules/.../Endpoints` — должно быть пусто.

---

## 5. Endpoint-specific dependencies — `[FromServices]`

Сервис, который нужен **только одному endpoint** в controller, не
инжектируется через конструктор. Он берётся через `[FromServices]` на
параметре action-метода.

```csharp
// ❌ Wrong — splitQuery нужен только PostSplitQueryAsync
public sealed class ReportingController(
    IDispatcher dispatcher,
    ISplitQueryService splitQuery,
    IValidator<CampaignDailyQuery> validator1,
    IValidator<MetricQueryRequest> validator2)
    : ControllerBase

// ✅ Correct — только общие зависимости в конструкторе
public sealed class ReportingController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet("campaigns/{id}/daily")]
    public async Task<ActionResult<StatsReport>> GetDailyAsync(
        ObjectId id,
        [FromQuery] CampaignDailyQuery query,
        [FromServices] IValidator<CampaignDailyQuery> validator,
        CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(query, ct);
        // ...
    }

    [HttpPost("split/query")]
    public async Task<ActionResult<SplitQueryResponse>> PostSplitQueryAsync(
        [FromBody] SplitQueryRequest request,
        [FromServices] ISplitQueryService splitQuery,
        CancellationToken ct = default)
    {
        return Ok(await splitQuery.ExecuteAsync(request, ct));
    }
}
```

**Когда что:**

| Где | Что |
|-----|-----|
| **Конструктор controller'а** | Сервисы которые нужны **2+ endpoints** (обычно `IDispatcher`) |
| **`[FromServices]` на action** | Сервис который нужен **ровно 1 endpoint** |

**Что НЕ переносить в `[FromServices]`:**
- `IDispatcher` — нужен почти каждому endpoint.
- Логгер — если нужен везде.
- Configuration / Options — если влияют на все endpoints.

**Порядок параметров в action-методе:**

```csharp
public async Task<ActionResult<T>> SomeActionAsync(
    [FromRoute] ObjectId id,               // 1. route params
    [FromBody] SomeRequest request,         // 2. body DTO
    [FromQuery] SomeQuery query,            // 3. query params
    [FromServices] ISomeService service,    // 4. injected services
    CancellationToken cancellationToken = default)  // 5. cancellation token (всегда последний)
```

---

## 6. `JsonSerializerOptions` — `Web` или `*JsonOptions.Instance`, не inline

`JsonSerializerOptions` — частая точка расхождения в проекте. Каждый
файл изобретает по-своему: `new JsonSerializerOptions()` инлайн,
`private static readonly` на классе, `JsonSerializerOptions.Default`,
`(JsonSerializerOptions?)null`.

**Правило: не создавай `new JsonSerializerOptions(...)` в app code.**
Используй один из двух framework/project-provided singleton'ов.

### Tier 1 — `JsonSerializerOptions.Web` (framework, кэширован)

.NET 9+ предоставляет **frozen, cached, shared** instance:

```csharp
JsonSerializerOptions Web { get; }   // camelCase + case-insensitive + AllowReadingFromString
```

**Когда использовать `.Web`** — стандартная web-сериализация без
project-specific converters.

**`JsonSerializerOptions.Web` — frozen.** Не вызывай `.Converters.Add()`,
не присваивай `PropertyNamingPolicy`. Это shared instance: мутация
повлияет на всех потребителей в процессе.

### Tier 2 — `*JsonOptions.Instance` (project, mutable singleton)

Когда нужны custom converters (`ObjectIdJsonConverter`,
`JsonStringEnumConverter` для enum-DTO) — собственный singleton:

```csharp
// src/shared/Hybrid.Shared.Persistence/Outbox/OutboxJsonOptions.cs
internal static class OutboxJsonOptions
{
    public static JsonSerializerOptions Instance { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new ObjectIdJsonConverter() }
    };
}
```

**Правила формы:**
- `internal static class` — не часть публичного API.
- `Instance` — статическое read-only свойство.
- Один набор конвенций на `*JsonOptions`. Не write/read-сплит.
- Расположение — рядом с потребителем.

### Tier 3 — `file static class *JsonOptions` — только для file-local

Если options нужны **ровно одному файлу** — file-scope.

### Anti-patterns

```csharp
// ❌ JsonSerializerOptions.Default — PascalCase, ломает camelCase wire-format
JsonSerializer.Serialize(rule.Conditions, JsonSerializerOptions.Default);

// ❌ Inline new JsonSerializerOptions(JsonSerializerDefaults.Web) в call-site
await JsonSerializer.SerializeAsync(stream, payload,
    new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);

// ❌ Inline new JsonSerializerOptions { ... } с конфигурацией
await JsonSerializer.SerializeAsync(stream, payload, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new ObjectIdJsonConverter() }
}, cancellationToken);

// ❌ Мутация JsonSerializerOptions.Web (shared!)
JsonSerializerOptions.Web.Converters.Add(new ObjectIdJsonConverter());

// ❌ private static readonly на классе потребителя
public sealed class CountryRegistry
{
    private static readonly JsonSerializerOptions JsonOpts = new() { ... };
}

// ❌ (JsonSerializerOptions?)null в аргументе
JsonSerializer.Serialize(set, (JsonSerializerOptions?)null);

// ❌ Несколько *JsonOptions с write/read сплитом без причины
```

### Что НЕ покрывается правилом

- `services.AddControllers().AddJsonOptions(...)` в composition root.
- `new JsonSerializerOptions(JsonSerializerDefaults.Web)` в `RefitSettings.ContentSerializer`
  — единственный легитимный случай inline `new`, потому что Refit API ждёт
  именно instance. Если Refit-клиентов станет > 1, вынести в `*JsonOptions.Instance`.

**Test:** `grep -rnE 'new JsonSerializerOptions\b' src/ --include='*.cs'` —
должно показать только `*JsonOptions.cs` файлы.

---

## Связанные правила

- `naming-and-types.md` — sealed, record vs class
- `code-shape.md` — pattern matching, var, braces
- `api-design.md` — controllers, endpoints, DTOs
- `analyzers.md` — analyzer packages