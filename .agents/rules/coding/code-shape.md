---
description: c# code shape — pattern matching, var, file-scoped namespaces, braces, comments, collections, interface organization, no #region, no ThrowIfNull
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

## 9. Private business logic — выносим

**Класс не должен содержать приватную бизнес-логику.**

Допустимо оставить `private`:
- В **HostedService / Worker / BackgroundService** — оркестрирующие методы.
- В **Controller / Handler** — методы валидации, форматирования, тривиальные хелперы.
- `Dispose` / `DisposeAsync`.
- Static утилиты без DI — в `file static class`.

Выносим в отдельный класс/сервис:
- Любая private-логика, которую можно переиспользовать.
- Логика, требующая изолированного тестирования.
- Сложная логика с собственными зависимостями.

```csharp
// ✅ HostedService — оркестрирующие private OK
public sealed class ProxyValidationWorker(
    IProxyRepository proxyRepository,
    IProxyHealthChecker proxyHealthChecker,
    ILogger<ProxyValidationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ValidateProxiesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ValidateProxiesAsync(CancellationToken cancellationToken)
    {
        foreach (var proxy in await proxyRepository.GetAliveProxiesAsync(cancellationToken))
        {
            var checkResult = await proxyHealthChecker.CheckAsync(proxy, cancellationToken);
            if (checkResult.IsHealthy)
            {
                await proxyRepository.MarkHealthyAsync(proxy.Id, checkResult.LatencyMs, cancellationToken);
            }
            else
            {
                logger.LogWarning("Proxy {ProxyId} failed health check", proxy.Id);
            }
        }
    }
}

// ✅ Бизнес-логика — отдельный класс с интерфейсом
public interface IProxyHealthChecker
{
    public Task<HealthCheckResult> CheckAsync(ManagedProxy proxy, CancellationToken cancellationToken = default);
}

// ✅ Чистая утилита — file static
file static class ProxyUriBuilder
{
    public static string Build(ManagedProxy proxy)
    {
        return string.IsNullOrEmpty(proxy.Username)
            ? $"http://{proxy.Host}:{proxy.Port}"
            : $"http://{proxy.Username}:{proxy.Password}@{proxy.Host}:{proxy.Port}";
    }
}
```

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

## 11. `ArgumentNullException.ThrowIfNull` — ЗАПРЕЩЁН в nullable-enabled контексте

`<Nullable>enable</Nullable>` включён глобально через `Directory.Build.props`.
Когда параметр объявлен non-nullable (`string x`, не `string? x`), компилятор
**уже** enforced non-null на каждом call-site. Дублировать через
`ThrowIfNull(x)` — noise + ложь о контракте + нарушение DRY.

```csharp
// ❌ Wrong — services/configuration non-nullable, компилятор уже enforces
public static IServiceCollection AddFooCore(
    this IServiceCollection services,
    IConfiguration configuration)
{
    ArgumentNullException.ThrowIfNull(services);       // ← duplicate
    ArgumentNullException.ThrowIfNull(configuration);  // ← duplicate
    // ...
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

**Что НЕ запрещено** (бизнес-валидация, не null-контракт):

```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);  // empty/whitespace check
ArgumentException.ThrowIfNullOrEmpty(name);             // empty check
```

Empty/whitespace ортогональны null: non-nullable `string` всё ещё может быть `""`.

**Boundary exception (редкая):** `ThrowIfNull` оправдан только когда значение
проходит через nullable-oblivious границу:

- Reflection — `MethodInfo.Invoke`, `Activator.CreateInstance`.
- Interop / P/Invoke — unmanaged код не nullable-aware.
- Serialization — десериализаторы конструируют объект в обход constructor.
- External library скомпилированная с `<Nullable>disable`.

В таких случаях добавить комментарий с обоснованием:

```csharp
// boundary: invoked via reflection, compiler cannot enforce nullability
ArgumentNullException.ThrowIfNull(instance);
```

Внутри solution (везде `<Nullable>enable</>`) таких границ для внутренних
вызовов нет.

**Enforcement:** convention + `worker-audit.md`. CA1062 в `.editorconfig`
`none` — проект уже решил, что валидация аргументов избыточна под nullable.

---

## Связанные правила

- `naming-and-types.md` — naming, sealed, record vs class
- `constructors-and-fields.md` — primary ctor, fields, constants
- `class-layout-and-tooling.md` — XML docs, model placement, required tooling
- `async-and-tasks.md` — async/await
- `anti-patterns.md` — enum anti-patterns, tuple ban, validation