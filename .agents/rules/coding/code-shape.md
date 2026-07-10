---
description: c# code shape — pattern matching, var, file-scoped namespaces, braces, comments, collections, interface organization, no #region, no ThrowIf* (argument checks)
globs: ["**/*.cs"]
always: true
---

# Code shape

Этот файл — правила формы кода: pattern matching, var, скобки, namespace,
комментарии, коллекции, организация интерфейсов, запрет `#region`, запрет
`ThrowIfNull` в nullable-контексте.

Naming/sealed — в `naming-and-types.md`. Constructors/fields — в `constructors-and-fields.md`.

## 1. Pattern matching — минимальность через синтаксический сахар

**Главный принцип:** не разделяй присвоение и проверку. Если C# позволяет
уложить объявление переменной + условие в одно выражение — делай это.

### Inline assignment + check

```csharp
// ❌ Wrong — две строки
var user = await repository.GetByIdAsync(userId, cancellationToken);
if (user == null) return NotFound();

// ✅ Correct — одна строка
if (await repository.GetByIdAsync(userId, cancellationToken) is not { } user)
{
    return NotFound();
}
```

### Get-or-return-existing — merge обязателен в `is { } x`

Самый частый паттерн, где LLM ошибается: "получил сущность, если есть —
вернул её". Здесь `var + if is not null + return` — **всегда** неправильно,
даже если переменная формально упоминается и в `if`, и в `return`. Тело без
сайд-эффектов, только проброс значения → merge обязателен.

```csharp
// ✅ Correct — assign + check слиты
if (await repository.GetByIdAsync(userId, cancellationToken) is { } existing)
{
    return existing;
}

// ❌ Wrong — assign и check разделены
var existing = await repository.GetByIdAsync(userId, cancellationToken);
if (existing is not null)
{
    return existing;
}
```

**Merge не требуется** когда внутри ветки есть сайд-эффект или нетривиальное
использование: логирование, маппинг, повторное чтение поля, ветвление по
свойствам.

### `is var x and > N`

```csharp
// ✅ Присвоить и проверить одним выражением
if (await registry.MarkHungTasksAsync(timeout, cancellationToken) is var hungCount and > 0)
{
    logger.LogInformation("Marked {Count} tasks as Hung", hungCount);
}
```

### Switch expressions

```csharp
// ❌ Wrong
JobStatus status;
if (state == JobState.Running) status = JobStatus.Running;
else if (state == JobState.Disconnected) status = JobStatus.Disconnected;
else status = JobStatus.Waiting;

// ✅ Correct
var status = state switch
{
    JobState.Running       => JobStatus.Running,
    JobState.Disconnected  => JobStatus.Disconnected,
    _                      => JobStatus.Waiting,
};
```

### Inline single-use variables

Если переменная используется **один раз** — инлайн её.

```csharp
// ❌ Wrong
var fundingData = await fundingService.GetFundingDataAsync(fundingId, cancellationToken);
return Ok(fundingData);

// ✅ Correct
return Ok(await fundingService.GetFundingDataAsync(fundingId, cancellationToken));
```

### Inline в `foreach`

```csharp
// ❌ Wrong
var items = await ComputeAsync(cancellationToken);
foreach (var item in items) Process(item);

// ✅ Correct
foreach (var item in await ComputeAsync(cancellationToken))
{
    Process(item);
}
```

### Ternary в return

```csharp
// ❌ Wrong
var dataJson = await db.HashGetAsync(metaKey, field);
if (dataJson.IsNullOrEmpty) return null;
return JsonSerializer.Deserialize<ManagedProxy>(dataJson.ToString());

// ✅ Correct
return await db.HashGetAsync(metaKey, field) is { } dataJson
    ? JsonSerializer.Deserialize<ManagedProxy>(dataJson.ToString())
    : null;
```

### Когда промежуточная переменная нужна

- Значение используется дважды и более.
- Имя переменной добавляет смысл.
- Выражение слишком сложное для inline.

---

## 2. Async / await

- Весь IO — через `async`/`await`.
- Запрещено: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`.
- Async suffix — всегда (см. `naming-and-types.md` §1).
- `CancellationToken` последним параметром со значением по умолчанию.
- Прокидывай `cancellationToken` во все вложенные async-вызовы.
- Никаких `Task.Delay(...)` без токена.

**Enforcement:** VSTHRD103 (severity=error).

---

## 3. `var` vs explicit type

**Всегда `var`** когда тип выводится из правой части.

```csharp
// ✅ Correct
var user = await repository.GetUserByIdAsync(userId, cancellationToken);
var symbols = new List<string>();
var count = items.Count;

// ❌ Wrong (избыточно)
List<string> symbols = new List<string>();
User user = await repository.GetUserByIdAsync(userId, cancellationToken);
```

**Enforcement:** IDE0007 / IDE0008 (severity=error в `.editorconfig`).

---

## 4. File-scoped namespaces — REQUIRED

**Всегда `namespace Foo;` (file-scoped). Block-scoped запрещён.**

```csharp
// ✅ Correct
namespace MyProject.Services;

public sealed class UserService { }

// ❌ Wrong
namespace MyProject.Services
{
    public sealed class UserService { }
}
```

**Enforcement:** IDE0161 (severity=error).

---

## 5. Braces — REQUIRED всегда

Фигурные скобки требуются для **всех** control flow statements: `if`, `else`,
`for`, `foreach`, `while`, `do`, `using`, `lock`, `fixed`.

```csharp
// ✅ Correct
if (user is null)
{
    return NotFound();
}

foreach (var item in items)
{
    Process(item);
}

// ❌ Wrong — однострочник без скобок
if (user is null) return NotFound();

foreach (var item in items) Process(item);
```

Применимо и к `else`.

```csharp
// ✅ Correct
if (proxy.IsHealthy)
{
    await repository.MarkHealthyAsync(proxy.Id, cancellationToken);
}
else
{
    logger.LogWarning("Proxy {ProxyId} failed health check", proxy.Id);
}
```

**Минимальность через синтаксический сахар (раздел 1)** + **braces (этот раздел)**
работают вместе: первое — отсутствие промежуточных шагов; второе — форма
control flow.

**Enforcement:** csharp_prefer_braces = true (severity=error в `.editorconfig`).

### Expression-bodied methods — ЗАПРЕЩЕНЫ

**Методы — только block-body `{ }`. Expression-bodied методы запрещены.**

- **Свойства, accessors, индексаторы, операторы, лямбды** — expression-bodied разрешены.
- **Switch expressions** — не control statement, `=>` часть синтаксиса, braces не нужны.

```csharp
// ✅ Method — block body
public Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
{
    return repository.GetByIdAsync(userId, cancellationToken);
}

// ❌ Wrong — expression-bodied method, IDE0022 fail
public Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    => repository.GetByIdAsync(userId, cancellationToken);

// ✅ Property — expression-bodied OK
public string FullName => $"{FirstName} {LastName}";
```

**Enforcement:** IDE0022 (severity=error).

---

## 6. Inline-комментарии — только когда они ВАЖНЫ

Запрещены комментарии, дублирующие имя метода или операцию.
Разрешены комментарии, объясняющие **"почему"**, особенно если без
комментария код выглядит "неправильным" и его захочется "починить".

```csharp
// ❌ Wrong — дублирует имя
// increment counter
counter++;

// ❌ Wrong — описывает "что", это видно из кода
// loop through items
foreach (var item in items) ...

// ✅ Correct — объясняет "почему", предотвращает регрессию
// Binance throttles at 1200 req/min — увеличение приведёт к 429
private const int MaxRequestsPerMinute = 1100;

// ✅ Correct — workaround
// EF Core 8 теряет точность для decimal при ToList() — материализуем вручную
var rates = await query.AsAsyncEnumerable().ToListAsync(cancellationToken);

// ✅ TODO/HACK с автором или ссылкой
// TODO(ANL-1234): убрать после миграции на новый API биржи
```

Правило ревьюера: комментарий принимается, если без него существует реальный
риск того, что следующий читатель сломает код или потратит время на
понимание.

---

## 7. Collections — минимально достаточный тип

| Что нужно потребителю | Тип |
|----------------------|-----|
| Только итерация | `IEnumerable<T>` (с осторожностью) |
| Итерация + `Count` | `IReadOnlyCollection<T>` |
| Итерация + `Count` + индекс | `IReadOnlyList<T>` |
| Membership check | `HashSet<T>` / `IReadOnlySet<T>` |
| Фиксированный набор, max perf | `T[]` |

### Default для public API — `IReadOnlyCollection<T>`

`List<T>` / `IList<T>` в публичном API не возвращаем.

```csharp
// ✅ Default
public IReadOnlyCollection<string> ActiveSymbols => activeSymbols;

// ✅ Нужен индексный доступ
public IReadOnlyList<Trade> RecentTrades => recentTrades;

// ✅ Фиксированный, часто итерируется
public string[] SupportedExchanges { get; } = ["Binance", "OKX", "Bybit"];
```

### `HashSet<T>` для membership — O(1)

```csharp
// ✅ Fast lookups
private readonly HashSet<string> activeSymbols = new(StringComparer.OrdinalIgnoreCase);

// ❌ Wrong — O(n)
private readonly List<string> activeSymbols = new();
```

### `List<T>` / `IList<T>` — только локально для мутации

```csharp
// ✅ Локальная мутация — List
var symbols = new List<string>();
foreach (var instrument in instruments)
{
    if (instrument.IsActive) symbols.Add(instrument.Symbol);
}
return symbols.ToArray();

// ❌ Wrong — List в публичном API
public List<string> Symbols { get; }
```

### Empty collections

```csharp
// ✅ Бесплатно
return Array.Empty<Trade>();
return [];

// ❌ Аллокация
return new List<Trade>();
return new Trade[0];
```

### Anti-patterns

```csharp
// ❌ IEnumerable когда нужен Count или повторная итерация — LINQ дёргает источник заново
public IEnumerable<string> Symbols => symbols;

// ❌ List для lookups — O(n)
public List<string> Symbols { get; }
```

---

## 8. Interface organization — default в `Interfaces/`

Интерфейсы лежат в папке `Interfaces/` на одном уровне с реализациями.

```
Services/
├── Interfaces/
│   ├── IProxyHealthChecker.cs
│   └── IProxyRepository.cs
├── ProxyHealthChecker.cs
└── ProxyRepository.cs
```

**Исключение — one-to-one co-location:** если у интерфейса ровно одна
реализация и они никогда не разойдутся — можно положить рядом, в одной
папке, с общим префиксом имени (`OrderProcessor.cs` + `IOrderProcessor.cs`).

**Где НЕ обязательно `Interfaces/`:**
- Marker interfaces в `*.Markers`, `*.Abstractions`.
- Domain abstractions в `*.Entity.Core` / `*.Domain` — допустимо рядом.

---

## 9. Private business logic — ЗАПРЕЩЕНА в production-коде

### Принцип одной строкой

> **Никаких `private` методов в production-классах. Без исключений, кроме contract override.**

### Что считается contract override (разрешено)

`private` modifier допустим ТОЛЬКО в одном из двух случаев:

1. **Override метод интерфейса / base class** — `Dispose()` / `DisposeAsync()`,
   `OnModelCreating(ModelBuilder)`, `Configure(EntityTypeBuilder<T>)`,
   `ExecuteAsync(...)`, `ExecuteCycleAsync(...)`, `BuildAsync(...)`,
   `HandleAsync(...)`, `Contribute(...)`, `TryDequeue(...)`, и т.п. — везде
   ключевое слово `override` стоит в сигнатуре.
2. **Explicit interface implementation** — `void IDisposable.Dispose()`.

`override` modifier НЕ эквивалентен `private`. После `override` метод
становится `public` (или inherited visibility) — но его разрешено держать
**sealed override private** в наследнике, чтобы не светить наружу.

### Что запрещено (полный список)

`private` методы в **любом** из следующих классов, **если это не override**:

- Repository, QueryService, port implementation, port interface impl.
- EF DbContext, EF Configuration (`IEntityTypeConfiguration<T>`),
  EF migration.
- Controller / minimal API endpoint.
- Command / query handler (`ICommandHandler<,>`, `IQueryHandler<,>`).
- Worker / BackgroundService / HostedService.
- Adapter / external SDK wrapper / `IHttpClientFactory` consumer.
- Validator (`AbstractValidator<T>`).
- Seeder (`ISeeder`).
- Dispatcher / behavior / pipeline middleware.
- `*JsonOptions.Instance`-style configuration holder.
- Маппер (`IEntityMapper`, `IRequestMapper`, ...).

Этот список не закрытый. **Если класс — production-код и не DTO/entity, то
там не должно быть `private` методов кроме override.**

### Почему жёстко

Потому что `private` метод в production-классе — это anti-pattern, который
**противоречит другим правилам проекта** в каждом конкретном случае:

| Если пишешь | Это нарушает |
|---|---|
| "private валидацию в Controller" | `anti-patterns.md` §4 ("никаких приватных методов-валидаторов в controller, только FluentValidation") |
| "private форматирование в Controller" | `anti-patterns.md` §6 (маппинг через `Mapper`, не private helper) |
| "private оркестрацию в Worker" | `background-workers.md` §"Анатомия worker'а" (base class уже оркестрирует; тело в `ExecuteCycleAsync`) |
| "private SQL builder в Repository" | `ef-core.md` §1 ("no magic strings") — SQL собирается из `DatabaseInformation.Tables.X` / `Schemes.Y`, не литералами |
| "private маппинг row → DTO в Repository" | `code-shape.md` §11 ("private business logic выносим") — маппинг отдельно тестируется без БД |
| "private валидацию в `AbstractValidator`" | `anti-patterns.md` §4 — проверки через `RuleFor`, не private `BeUniqueAsync` |
| "private seed fixture в Seeder" | `code-shape.md` §9 — каждый `EnsureXxx` fixture в отдельный класс с зависимостями |
| "private URL/JSON helper в Adapter" | `ef-core.md` §"..." + DRY — extension method или отдельный маппер |

Старая формулировка §9 ("HostedService/Worker/Controller — допустимо")
**сама себе противоречила** — пример `ProxyValidationWorker.ValidateProxiesAsync`
нарушал `background-workers.md`. Эта версия правила закрывает дыру.

### Куда выносить

| Ситуация | Куда |
|---|---|
| Есть свои зависимости (DI: repo, logger, client, ...) | `internal sealed` helper в той же папке, отдельный файл, конструктор принимает зависимости. Если сложный pipeline с интерфейсом — `IXxxHelper` / `IXxxBuilder` / `IXxxFixture`. |
| Нет зависимостей, чистая функция | `file static class` рядом (в том же или соседнем файле `*Helpers.cs`). |
| Расширение существующего типа | extension methods в `*Extensions.cs` (отдельный файл, `file static class`). |
| Маппинг между слоями | отдельный `*Mapper.cs` с интерфейсом `IXxxMapper`. |
| Валидация | FluentValidation rules (`RuleFor` chain + переиспользуемые Rule-объекты), не private-методы. |

### 9.1 — Repository / port implementation

```csharp
// ❌ WRONG — Repository с private helper'ами
public sealed class CampaignRepository(CampaignsDbContext db) : ICampaignRepository
{
    public async Task<Campaign?> GetAsync(CampaignId id, CancellationToken ct)
    {
        var entity = await db.CampaignsSet.FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity is null ? null : MapToAggregate(entity);  // ← private method
    }

    private static Campaign MapToAggregate(CampaignEntity e) => new(...) { ... };
    private static BannerEntity MapBanner(Banner b) => new(...) { ... };
    private async Task<IReadOnlyList<Banner>> LoadBannersAsync(Guid campaignId, CancellationToken ct) { ... }
    private static string ToColumnName(string name) => name.ToSnakeCase();
}
```

```csharp
// ✅ CORRECT — Repository orchestration only; helpers in separate files
public sealed class CampaignRepository(
    CampaignsDbContext db,
    ICampaignMapper mapper) : ICampaignRepository
{
    public async Task<Campaign?> GetAsync(CampaignId id, CancellationToken ct)
    {
        var entity = await db.CampaignsSet.FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity is null ? null : mapper.ToAggregate(entity);
    }
}

// Persistence/Mappers/CampaignMapper.cs
internal sealed class CampaignMapper : ICampaignMapper
{
    public Campaign ToAggregate(CampaignEntity e) => new(...) { ... };
    public BannerEntity ToEntity(Banner b) => new(...) { ... };
}
```

### 9.2 — EF Configuration (`IEntityTypeConfiguration<T>`)

```csharp
// ❌ WRONG — IEntityTypeConfiguration с private helper ConfigureBanners / ConfigureFolderRelations
public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Campaigns, CampaignsDbContext.Schema);
        ConfigureBanners(builder);  // ← private method
        ConfigureFolderRelations(builder);  // ← private method
    }

    private void ConfigureBanners(EntityTypeBuilder<Campaign> builder) { ... }
    private void ConfigureFolderRelations(EntityTypeBuilder<Campaign> builder) { ... }
}
```

```csharp
// ✅ CORRECT — flat Configure; nested owned-types выносятся в `file static class` рядом
public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Campaigns, CampaignsDbContext.Schema);
        builder.Property(static c => c.AdvertiserId).HasColumnName("advertiser_id").IsRequired();
        builder.OwnsMany(static campaign => campaign.Banners, BannersConfiguration.Configure);
    }
}

// Configurations/BannersConfiguration.cs
file static class BannersConfiguration
{
    public static void Configure(OwnedNavigationBuilder<Campaign, Banner> banner)
    {
        banner.ToTable(DatabaseInformation.Tables.Banners, CampaignsDbContext.Schema);
        banner.HasKey(static b => b.Id);
        banner.Property(static b => b.ClickUrl).HasColumnName("click_url").HasMaxLength(2048).IsRequired();
    }
}
```

### 9.3 — Worker / BackgroundService / HostedService

```csharp
// ❌ WRONG — Worker с private orchestration (нарушает background-workers.md)
public sealed class ProxyValidationWorker(
    IProxyRepository proxyRepository,
    IProxyHealthChecker proxyHealthChecker,
    ILogger<ProxyValidationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ValidateProxiesAsync(stoppingToken);  // ← private method
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ValidateProxiesAsync(CancellationToken cancellationToken) { ... }
}
```

```csharp
// ✅ CORRECT — Worker через ScheduledWorkerBase; всё тело в ExecuteCycleAsync (override)
public sealed class ProxyValidationWorker(
    IServiceProvider services,
    TimeProvider clock,
    IDiagnosticSource? diagnostics,
    ILogger<ProxyValidationWorker> logger) : ScheduledWorkerBase(services, clock, diagnostics, logger)
{
    protected override WorkerSchedule Schedule => new IntervalSchedule(TimeSpan.FromMinutes(1));

    protected override async Task<object?> ExecuteCycleAsync(WorkerContext ctx, CancellationToken ct)
    {
        var repository = ctx.GetService<IProxyRepository>();
        var checker = ctx.GetService<IProxyHealthChecker>();
        var processed = 0;
        var failed = 0;

        foreach (var proxy in await repository.GetAliveProxiesAsync(ct))
        {
            var checkResult = await checker.CheckAsync(proxy, ct);
            processed++;
            if (!checkResult.IsHealthy) { failed++; continue; }
            await repository.MarkHealthyAsync(proxy.Id, checkResult.LatencyMs, ct);
        }

        return new ValidationResult(Processed: processed, Failed: failed);
    }
}
```

### 9.4 — Controller / minimal endpoint

```csharp
// ❌ WRONG — Controller с private валидацией и форматированием
public sealed class CampaignsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var validation = ValidateCreateRequest(request);  // ← private method
        if (!validation.IsValid) return ValidationProblem(validation.Errors);

        var formattedBid = FormatBid(request.Bid);  // ← private method
        var command = new CreateCampaignCommand(..., formattedBid);
        var id = await dispatcher.SendAsync(command, ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { id }, id);
    }

    private static ValidationResult ValidateCreateRequest(CreateCampaignRequest request) { ... }
    private static string FormatBid(decimal bid) => bid.ToString("0.00");
}
```

```csharp
// ✅ CORRECT — FluentValidation (через ValidationBehavior) + CommandMapper
public sealed class CampaignsController(
    IDispatcher dispatcher,
    ICampaignCommandMapper mapper) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateCampaignRequest request,
        CancellationToken ct)
    {
        var command = mapper.ToCommand(request);
        var id = await dispatcher.SendAsync(command, ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { id }, id);
    }
}

// Endpoints/Mappers/CampaignCommandMapper.cs
internal sealed class CampaignCommandMapper : ICampaignCommandMapper
{
    public CreateCampaignCommand ToCommand(CreateCampaignRequest request) =>
        new(request.AdvertiserId, request.Name.Trim(), request.StartDate, request.EndDate, request.Bid);
}

// Application/Validation/CreateCampaignValidator.cs — FluentValidation
public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator()
    {
        RuleFor(static c => c.Name).SetValidator(new NameRule());
        RuleFor(static c => c.Bid).GreaterThan(0).When(static c => c.Strategy == BuyingStrategy.FixedCpm);
    }
}
```

### 9.5 — Command / query handler

```csharp
// ❌ WRONG — handler с private Ensure / Build методами
public sealed class CreateCampaignHandler(ICampaignRepository repo, TimeProvider clock) : ICommandHandler<CreateCampaignCommand, CampaignId>
{
    public async ValueTask<CampaignId> HandleAsync(CreateCampaignCommand cmd, CancellationToken ct)
    {
        await EnsureNameUnique(cmd.Name, ct);  // ← private
        var campaign = BuildCampaign(cmd);     // ← private
        await repo.AddAsync(campaign, ct);
        return campaign.Id;
    }

    private async Task EnsureNameUnique(string name, CancellationToken ct) { ... }
    private Campaign BuildCampaign(CreateCampaignCommand cmd) => Campaign.Create(...);
}
```

```csharp
// ✅ CORRECT — handler orchestration only
public sealed class CreateCampaignHandler(
    ICampaignRepository repo,
    ICampaignFactory factory) : ICommandHandler<CreateCampaignCommand, CampaignId>
{
    public async ValueTask<CampaignId> HandleAsync(CreateCampaignCommand cmd, CancellationToken ct)
    {
        var campaign = factory.Create(cmd);  // factory бросит DomainException при нарушении инвариантности
        await repo.AddAsync(campaign, ct);
        return campaign.Id;
    }
}

// Application/Campaigns/CampaignFactory.cs
internal sealed class CampaignFactory : ICampaignFactory
{
    public Campaign Create(CreateCampaignCommand cmd) => Campaign.Create(
        new AdvertiserId(cmd.AdvertiserId),
        cmd.Name,
        cmd.StartDate,
        cmd.EndDate,
        cmd.Bid,
        ...);
}
```

### 9.6 — Adapter / external SDK wrapper

```csharp
// ❌ WRONG — клиент с private URL builder'ом, маппером и 15 OkStatus fixture'ами
public sealed class AllianceDspClient(IHttpClientFactory http) : IDspClient
{
    public async Task<IReadOnlyList<BannerDto>> GetBannersAsync(..., CancellationToken ct)
    {
        var url = BuildUrl("banners");  // ← private
        var response = await http.CreateClient().GetAsync(url, ct);
        return MapToBannerList(response);  // ← private
    }

    private static string BuildUrl(string path) => $"https://api/v1/{path}";
    private static BannerDto MapToBannerList(HttpResponseMessage response) { ... }
    private static ApiResponse<X> OkStatus() => new(HttpStatusCode.OK, default!);  // ← 15 копий
    // ... ещё 12 приватных OkXxx-хелперов
}
```

```csharp
// ✅ CORRECT — клиент orchestration; URL builder / маппер / fixtures — отдельно
public sealed class AllianceDspClient(
    IHttpClientFactory http,
    IAllianceDspUrlBuilder urls,
    IBannerResponseMapper mapper) : IDspClient
{
    public async Task<IReadOnlyList<BannerDto>> GetBannersAsync(..., CancellationToken ct)
    {
        var response = await http.CreateClient().GetAsync(urls.Banners(), ct);
        return mapper.ToBannerList(response);
    }
}

// Adapter/Helpers/AllianceDspUrlBuilder.cs
internal sealed class AllianceDspUrlBuilder : IAllianceDspUrlBuilder
{
    public Uri Banners() => new("https://api/v1/banners", UriKind.Absolute);
}

// Adapter/Mappers/BannerResponseMapper.cs
internal sealed class BannerResponseMapper : IBannerResponseMapper { ... }

// Tests/DspResponseFixtures.cs (file static)
file static class DspResponseFixtures
{
    public static ApiResponse<T> Ok<T>(T payload) => new(HttpStatusCode.OK, payload);
}
```

В **stub'ах** (например `StubAllianceDspClient`) fixture'ы тоже не
`private static ApiResponse OkStatus()` 15 раз. Один `DspResponseFixtures.Ok(...)`
в `DspResponseFixtures.cs` (file static).

### 9.7 — SQL / ADO.NET port implementation

```csharp
// ❌ WRONG — IBannerContextLookup с inline ADO.NET + SQL literal + 3 private helpers
public sealed class BannerContextLookup(
    CampaignsDbContext dbContext,
    ISettingResolver settingResolver) : IBannerContextLookup
{
    private const string Sql = """ SELECT ... FROM campaigns.banners b ... """;  // ← magic strings

    public async Task<BannerContextSnapshot?> GetAsync(string bannerId, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, ct);  // ← private
        await using var command = connection.CreateCommand();
        command.CommandText = Sql;
        AddParameter(command, "@bannerId", bannerId, DbType.String);  // ← private
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var snapshot = await ReadSnapshotAsync(reader, ct);  // ← private

        if (snapshot.Agency?.Id is { } agencyIdValue)
        {
            var agencyId = ObjectId.Parse(agencyIdValue.Value!.ToString());  // ← round-trip
            var settings = await settingResolver.ResolveAsync(
                new SettingScope.ForAgency(agencyId), ct);
            snapshot = snapshot with { Settings = settings };
        }

        return snapshot;
    }

    private static async Task EnsureOpenAsync(NpgsqlConnection connection, CancellationToken ct) { ... }
    private static void AddParameter(NpgsqlCommand command, string name, object value, DbType type) { ... }
    private static async Task<BannerContextSnapshot> ReadSnapshotAsync(NpgsqlDataReader reader, CancellationToken ct) { ... }
}
```

```csharp
// ✅ CORRECT — port orchestration only; SQL/ADO.NET/проекция — отдельные helper'ы
public sealed class BannerContextLookup(
    IBannerContextSnapshotReader reader,    // SQL + ADO.NET + row→snapshot
    ISettingResolver settingResolver) : IBannerContextLookup
{
    public async Task<BannerContextSnapshot?> GetAsync(string bannerId, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(bannerId, ct);
        if (snapshot is null) return null;
        return snapshot.Agency?.Id is { } agencyId
            ? snapshot with { Settings = await settingResolver.ResolveForAgencyAsync(agencyId, ct) }
            : snapshot;
    }
}

// Persistence/Helpers/BannerContextSnapshotReader.cs
internal sealed class BannerContextSnapshotReader(
    IDbConnectionFactory connectionFactory) : IBannerContextSnapshotReader
{
    private const string Sql = $"""
                                SELECT
                                    b.id                AS banner_id,
                                    c.id                AS campaign_id,
                                    a.id                AS advertiser_id,
                                    ag.id               AS agency_id,
                                    ag.trading_desk_id  AS agency_trading_desk_id,
                                    td.id               AS trading_desk_id
                                FROM {CampaignsDbContext.Schema}.{DatabaseInformation.Tables.Banners} b
                                INNER JOIN {CampaignsDbContext.Schema}.{DatabaseInformation.Tables.Campaigns} c
                                    ON c.id = b.campaign_id
                                LEFT JOIN {TenantsDbContext.Schema}.{TenantsDbInformation.Tables.Advertisers} a
                                    ON a.id = c.advertiser_id
                                ...
                                WHERE b.id = @bannerId
                                """;  // SQL собран через DatabaseInformation, не литералы

    public async Task<BannerContextSnapshot?> ReadAsync(string bannerId, CancellationToken ct) { ... }
}

// Persistence/Helpers/NpgsqlCommandExtensions.cs
internal static class NpgsqlCommandExtensions
{
    public static void AddStringParameter(this NpgsqlCommand command, string name, string value) { ... }
}

// Persistence/Helpers/NpgsqlConnectionExtensions.cs
internal static class NpgsqlConnectionExtensions
{
    public static async Task EnsureOpenAsync(this NpgsqlConnection connection, CancellationToken ct) { ... }
}
```

Заодно выполняется `ef-core.md` §1 (no magic strings): SQL собирается через
константы `DatabaseInformation.Tables.*` / `Schemes.*`, а не литералы.

### 9.8 — Validator (`AbstractValidator<T>`)

```csharp
// ❌ WRONG — Validator с private проверками (нарушает anti-patterns.md §4)
public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator(ICampaignRepository repo)
    {
        RuleFor(static c => c.Name).NotEmpty();
        RuleFor(static c => c.Name).MustAsync(BeUniqueAsync).WithMessage("Name must be unique.");
        RuleFor(static c => c.EndDate).Must(EndAfterStart).WithMessage("End date must not precede start date.");
    }

    private async Task<bool> BeUniqueAsync(string name, CancellationToken ct) =>  // ← private
        !await repo.NameExistsAsync(name, ct);

    private static bool EndAfterStart(CreateCampaignCommand cmd) =>  // ← private
        cmd.EndDate is null || cmd.EndDate >= cmd.StartDate;
}
```

```csharp
// ✅ CORRECT — правила inline + переиспользуемые Rule-объекты
public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator()
    {
        RuleFor(static c => c.Name).SetValidator(new NameRule());
        RuleFor(static c => c.EndDate).GreaterThanOrEqualTo(static c => c.StartDate)
            .When(static c => c.EndDate.HasValue)
            .WithMessage("End date must not precede start date.");
    }
}

// Уникальность имени — отдельная политика домена, не validator:
//   ICampaignUniquenessPolicy.EnsureUniqueAsync(name, ct)
// вызывается из handler'а до AddAsync, не прячется в private MustAsync.
```

### 9.9 — Seeder (`ISeeder`)

```csharp
// ❌ WRONG — Seeder с private EnsureXxx методами
public sealed class SampleDataSeeder(...)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        var td = await EnsureTradingDeskAsync(db, ct);       // ← private
        var agency = await EnsureAgencyAsync(db, td.Id, ct); // ← private
        var team = await EnsureDefaultTeamAsync(db, agency.Id, ct);  // ← private
        var advertiser = await EnsureAdvertiserAsync(db, agency.Id, team.Id, ct);  // ← private
        ...
    }

    private async Task<TradingDesk?> EnsureTradingDeskAsync(...) { ... }
    private async Task<Agency?> EnsureAgencyAsync(...) { ... }
    private async Task<Team?> EnsureDefaultTeamAsync(...) { ... }
    private async Task<Advertiser?> EnsureAdvertiserAsync(...) { ... }
    // ещё 5 приватных EnsureXxx
}
```

```csharp
// ✅ CORRECT — каждый Ensure-блок в `EnsureXxxFixture.cs` с собственными зависимостями
public sealed class SampleDataSeeder(
    TradingDeskFixture tradingDeskFixture,
    AgencyFixture agencyFixture,
    TeamFixture teamFixture,
    AdvertiserFixture advertiserFixture,
    CampaignFixture campaignFixture,
    CreativeFixture creativeFixture,
    BannerFixture bannerFixture) : ISeeder
{
    public string Name => "samples";

    public async Task SeedAsync(CancellationToken ct)
    {
        var td = await tradingDeskFixture.EnsureAsync(ct);
        var agency = await agencyFixture.EnsureAsync(td.Id, ct);
        var team = await teamFixture.EnsureDefaultAsync(agency.Id, ct);
        var advertiser = await advertiserFixture.EnsureAsync(agency.Id, team.Id, ct);
        ...
    }
}

// Seeders/Fixtures/TradingDeskFixture.cs
internal sealed class TradingDeskFixture(TenantsDbContext db)
{
    public async Task<TradingDesk?> EnsureAsync(CancellationToken ct) { ... }
}

// Seeders/Fixtures/AgencyFixture.cs
internal sealed class AgencyFixture(TenantsDbContext db)
{
    public async Task<Agency?> EnsureAsync(TradingDeskId tdId, CancellationToken ct) { ... }
}
```

### 9.10 — Static helpers / parsers

```csharp
// ❌ WRONG — handler с private static parser
public sealed class SymbolParserHandler(...)
{
    public async ValueTask<...> HandleAsync(...) {
        var parts = ParseSymbol(command.Symbol);  // ← private static
        ...
    }

    private static (string Base, string Quote) ParseSymbol(string symbol) { ... }
}
```

```csharp
// ✅ CORRECT — extension method в file static class
public sealed class SymbolParserHandler(...)
{
    public async ValueTask<...> HandleAsync(...) {
        var parts = command.Symbol.ParseTradingPair();  // extension
        ...
    }
}

// Helpers/SymbolExtensions.cs
file static class SymbolExtensions
{
    public static (string Base, string Quote) ParseTradingPair(this string symbol)
    {
        var idx = symbol.IndexOf('_');
        return (symbol[..idx], symbol[(idx + 1)..]);
    }
}
```

### Self-audit шаг для LLM (обязателен перед коммитом)

```bash
# По всему diff'у — найти ВСЕ private методы в добавленных/изменённых файлах
git diff --name-only --diff-filter=AM | xargs -I {} \
  rg -n '^\s*private (static )?(async )?\w+ [A-Z]\w+\s*\(' {}

# Должно быть пусто. Если найдено — выноси в helper/extension/static file.
```

Полный grep по `src/` для ревьюера:

```bash
# Все private методы (исключаем поля, константы, override, explicit interface impl):
rg -n '^\s*private ' src/ -g '*.cs' \
  | rg -v 'private (sealed )?(class|record)\b' \
  | rg -v 'private (const|static readonly|readonly)\b' \
  | rg -v 'override\b' \
  | rg -v '_[A-Z]\w*\s*='
```

Каждый результат = потенциальное нарушение §9. Действия:

1. Вынести в `internal sealed` helper в той же папке.
2. Или в `file static class` (если нет зависимостей).
3. Или extension method в `*Extensions.cs`.
4. Если это всё-таки contract override — добавить `override` keyword
   (после `override` modifier'а уже не `private`).

### Reviewer checklist

При code review класса, в котором есть `private` метод (не override):

- [ ] Это contract override (`Dispose`/`DisposeAsync`/`Configure`/`OnModelCreating`/`ExecuteAsync`/`ExecuteCycleAsync`/`HandleAsync`/...)? Если да — `override` keyword присутствует?
- [ ] Если нет — почему метод в этом классе, а не в helper/extension/separate service?
- [ ] Можно ли его изолированно протестировать без внешних зависимостей класса?
- [ ] Не нарушает ли это `anti-patterns.md` §4 (валидация) или `background-workers.md` (оркестрация)?
- [ ] Не маскирует ли он god-object (класс > 200 строк, много private методов)?
- [ ] Не использует ли он magic strings (literals), которые должны быть в `DatabaseInformation` / `*Constants`?

Если на любой вопрос "нет" / "не знаю" — просить вынести.

### Hard rule для LLM

> **Если ты (LLM) написал `private` метод в production-классе и это не
> `override` — ты нарушил §9. Исправь ДО коммита. Self-audit grep выше —
> это Definition of Done, не опция.**

### Существующий техдолг (forward-only)

Это правило **forward-only**. Существующие `private` методы остаются как
есть до момента их естественного рефакторинга в рамках задачи, которая
их трогает. При любом изменении файла, содержащего `private` метод, LLM
**обязан** прогнать self-audit и привести файл к §9 в рамках этой задачи.

> **Pre-existing private violations** (для awareness при будущих тасках):
> - `src/engine/Hybrid.Engine.PreAggregation/PostgresAffectedSetResolver.cs` —
>   SQL builder + ADO.NET helpers, ~6 `private` методов.
> - `src/engine/Hybrid.Engine.Delivery/StubAllianceDspClient.cs` —
>   ~15 `private static ApiResponse OkXxx()` fixture-хелперов.
> - `src/host/Hybrid.Migrator/Seeders/SampleDataSeeder.cs` —
>   ~7 `private async Task EnsureXxxAsync()` fixture-хелперов.
> - `src/host/Hybrid.Migrator/Seeders/SeedDispatcher.cs` — orchestration helpers.
> - `src/modules/Hybrid.Modules.Campaigns/.../BannerContextLookup.cs` — `EnsureOpenAsync` / `AddParameter` / `ReadSnapshotAsync`.

Каждое из них — техдолг. При следующем touch соответствующего файла —
вынести в helper / extension / static file.

---

## 10. No `#region` directives — ЗАПРЕЩЕНЫ во всех C#-файлах

`#region` / `#endregion` запрещены. В проекте 0 таких директив по конвенции.

**Почему — анти-паттерн:**
1. Прячут структуру файла от outline-режима IDE.
2. Поощряют раздувание класса (Helpers-регион длиннее 3 методов = сигнал).
3. Шумят в diff-ах.
4. Ломают source generators и рефакторинги.

**Что делать вместо региона:**

| Случай | Решение |
|--------|---------|
| "Group constructors" | primary ctor + members по visibility |
| "Helpers" | вынести в отдельный тип (mapping → Mapperly, validation → validator) |
| "Constants" | `file static class` рядом с потребителем (см. `constructors-and-fields.md`) |
| "Properties" | порядок по роли, или value object |
| Большой файл | partial по responsibility (last resort) — сначала спросить "не отдельный класс ли это?" |

**Exemptions:** `.g.cs` / `.Designer.cs` (сгенерированные) — вне редактирования.

**Enforcement:** convention + `worker-audit.md`. Build-gate regex на
литеральный токен `#region` не добавлен пока проект в чистом состоянии.

---

## 11. `ThrowIf*` argument checks — ЗАПРЕЩЕНЫ в этом проекте

**Hard rule.** Никаких `ArgumentNullException.ThrowIfNull`,
`ArgumentException.ThrowIfNullOrEmpty`,
`ArgumentException.ThrowIfNullOrWhiteSpace` или аналогичных.
Ни в public API, ни в private helpers, ни в extension-методах.

**Причина:** `<Nullable>enable</Nullable>` включён глобально через
`Directory.Build.props`. Когда параметр объявлен non-nullable (`string x`,
не `string? x`), компилятор **уже** enforced non-null на каждом call-site.

- `ArgumentNullException.ThrowIfNull(x)` — шум: компилятор уже
  отверг вызов с `null`.
- `ArgumentException.ThrowIfNullOrEmpty(text)` — для строки компилятор
  enforced non-null; проверка empty/whitespace — бизнес-инвариант, не
  контракт метода. Если «пустая строка» ломает поведение, это либо
  (a) `default` в caller'е (compile-error), либо (b) ошибка на стороне
  caller'а, не на стороне метода.
- Принцип: **trust the signature**. Если параметр non-nullable и
  не-empty/non-whitespace — это контракт caller'а, не обязанность метода
  перепроверять.

```csharp
// ❌ Wrong — компилятор уже отверг null. ThrowIf* — шум + ложь о контракте.
public static IServiceCollection AddFooCore(
    this IServiceCollection services,
    IConfiguration configuration)
{
    ArgumentNullException.ThrowIfNull(services);       // ← duplicate
    ArgumentNullException.ThrowIfNull(configuration);  // ← duplicate
    ArgumentException.ThrowIfNullOrEmpty(name);        // ← caller должен передать не-пустое
    // ...
}

// ❌ Wrong — в extension-методе.
public static string Ok(this string text)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(text);    // ← шум
    return $"[ok]{text}[/]";
}

// ✅ Correct — trust the signature
public static IServiceCollection AddFooCore(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddOptions<FooOptions>().Bind(...);
    // ...
}
```

**Когда всё-таки можно.** Только в **nullable-oblivious** boundary:

- Reflection — `MethodInfo.Invoke`, `Activator.CreateInstance`.
- Interop / P/Invoke — unmanaged код не nullable-aware.
- Serialization — десериализаторы конструируют объект в обход ctor.
- External library скомпилированная с `<Nullable>disable`.

В этих случаях добавить комментарий с обоснованием:

```csharp
// boundary: invoked via reflection, compiler cannot enforce nullability
ArgumentNullException.ThrowIfNull(instance);
```

Внутри solution (везде `<Nullable>enable</>`) для внутренних вызовов
таких границ нет. **Не используйте ThrowIf* в production-коде этого
репозитория, кроме boundary.**

**Enforcement:** convention + `worker-audit.md`. CA1062 в `.editorconfig`
`none` — проект уже решил, что валидация аргументов избыточна под nullable.

**Self-audit grep (перед коммитом):**

```bash
rg -n 'ArgumentNullException\.ThrowIf|ArgumentException\.ThrowIf' src/ --type cs
```

Должно быть пусто (или содержать только `// boundary:` комментарии).

---

## 12. Builder pattern — content struct для состояния, не private fields

Билдеры с `Set*` методами, которые мутируют `private` поля —
анти-паттерн. Поле спрятано за методом, но метод просто присваивает.
Используйте `internal sealed class` с публичными полями для
состояния, а методы билдера делайте тонкой обёрткой над ним.

**Почему:**

1. **Нет `this`-capture церемонии.** Метод становится
   `Content.X = Y;` вместо `this._x = Y;`. Данные и API — разные
   объекты.
2. **Состояние тестируется / инспектируется изолированно.** Unit-тест
   может создать `BuilderContent` напрямую и assert'ить против него,
   не прогоняя каждый метод билдера.
3. **Контент передаётся в helpers без билдера.** `Build()` может
   принять content параметром — проще рассуждать, чем через `this`.
4. **`internal`/`file` доступ не загрязняет public API.** Контент
   не торчит наружу как часть публичной поверхности.

```csharp
// ❌ Old — private fields, this-capture в каждом setter
public sealed class PlexorCliBuilder
{
    private string? toolName;
    private string? toolVersion;
    private string? clusterName;

    public PlexorCliBuilder Name(string name)
    {
        toolName = name;       // ← this._toolName = name;
        return this;
    }
}

// ✅ New — internal content struct, public fields (внутри assembly)
internal sealed class PlexorCliContent
{
    public string? ToolName;
    public string? ToolVersion;
    public string? ClusterName;
    public List<Action<IConfigurator>> PendingConfigurations = new();
}

public sealed class PlexorCliBuilder
{
    public PlexorCliContent Content { get; } = new();

    public PlexorCliBuilder Name(string name)
    {
        Content.ToolName = name;
        return this;
    }
}
```

### Visibility rules

| Что | Где живёт | Access |
|-----|-----------|--------|
| **Content struct/class** | Тот же файл или `Abstractions/` рядом с билдером | `internal sealed` (по умолчанию), `file sealed` если только в одном файле |
| **Поля на content** | На content | `public` (внутри internal scope — это «доступно всему, что держит Content instance»). Это **единственное место**, где public fields — норм. |
| **Builder** | Public API | `public sealed` |
| **Helper-метод на билдере** | Public API | `public` |
| **File-local helper** | Один файл | `file static class` или `file sealed class` |

### Параметр-object для many-arg методов

Если метод принимает 3+ связанных параметра — оборачивайте их в
record/struct и передавайте одной переменной:

```csharp
// ❌ Many positional parameters
public void Render(string toolName, string toolVersion, string? clusterName, string? nodeName);

// ✅ Parameter object
public sealed record FooterRequest(string ToolName, string ToolVersion, string? ClusterName, string? NodeName);
public void Render(FooterRequest request);
```

Record может быть `internal sealed` если метод internal, `public sealed`
если метод public.

### `file` scope для локальных хелперов

Если helper-тип используется только в одном файле — объявляйте
`file sealed class` / `file static class`. Компилятор enforces:
никакой consumer вне файла не может его reference. Это
предотвращает «utils» / «helpers» папки, в которых живут
shared-утилсы, которые никто не хочет выносить в отдельный модуль.

```csharp
file static class PlexorCliBuilderHelpers
{
    // только для PlexorCliBuilder в этом файле
    public static void ApplyContent(this PlexorCliContent content, CommandApp app) { ... }
}
```

### Self-audit grep

```bash
# По всему src/ — найти билдеры с private field + setter pattern
rg -n 'private\s+(string|int|long|bool|Guid|List<|Dictionary<)\s+_\w+\s*[=;]' src/ --type cs

# Должно показать только DTO/entity (где private поля — норма) и
# не билдеры. Если в билдере private поле + setter — рефактор на Content.
```

---

## Связанные правила

- `naming-and-types.md` — naming, sealed, record vs class
- `constructors-and-fields.md` — primary ctor, fields, constants
- `class-layout-and-tooling.md` — XML docs, model placement, required tooling
- `async-and-tasks.md` — async/await
- `anti-patterns.md` — enum anti-patterns, tuple ban, validation