---
description: >-
  authoring background workers via ScheduledWorkerBase — mandatory patterns for
  Schedule choice (Interval/Cron/AdaptivePoll/Startup), DI scope-per-cycle,
  reflection-based counter emission via [Counter] attribute, error policy,
  cancellation, test infrastructure
globs:
  - src/**/Workers/**/*.cs
  - src/**/BackgroundServices/**/*.cs
  - src/**/ScheduledWorkerBase*.cs
  - src/**/*ScheduledWorker*.cs
  - src/**/Hybrid.Shared.Kernel/BackgroundServices/**/*.cs
priority: high
interactive: false
always: false
---

# Background workers — ScheduledWorkerBase pattern

Это правило — companion для скилла `background-worker-author`. **Все новые**
periodic / startup background workers в проекте ОБЯЗАНЫ наследовать от
[`ScheduledWorkerBase`](../../../src/shared/Hybrid.Shared.Kernel/BackgroundServices/ScheduledWorkerBase.cs)
(не от `BackgroundService` напрямую). PR без соответствия отклоняется.

## Зачем base class

Без `ScheduledWorkerBase` каждый worker сам реализует:
- `while (!ct.IsCancellationRequested)` loop + `Task.Delay` / cron wait
- per-cycle DI scope (`await using var scope = services.CreateAsyncScope();`)
- per-cycle telemetry (`using var op = diagnostics.Operation(...).Build();`)
- cancellation propagation (`try { } catch (OperationCanceledException) {}`)
- start/stop logging
- manual error policy + counter emission (`diagnostics.X.WithTag(...).Add(1)`)

12 работников в codebase делали это каждый по-своему. `ScheduledWorkerBase`
owner — это 100+ строк boilerplate per worker, сокращённое до 1 строки
`protected override WorkerSchedule Schedule => new ...;`.

## Анатомия worker'а

```csharp
public sealed class DailyWithdrawalWorker(
    IServiceProvider services,                    // ← root only; scope per cycle в base
    TimeProvider clock,
    IOptions<BillingCronOptions> cron,
    IBillingDiagnostics diagnostics,
    ILogger<DailyWithdrawalWorker> logger)
    : ScheduledWorkerBase(services, clock, diagnostics, logger)
{
    protected override string WorkerName =>
        nameof(DailyWithdrawalWorker);

    protected override WorkerSchedule Schedule =>
        new CronSchedule(cron.Value.DailyWithdrawal, TimeZoneInfo.Utc);

    protected override async Task<object?> ExecuteCycleAsync(
        WorkerContext context, CancellationToken cancellationToken)
    {
        var wallets = context.GetService<IWalletRepository>();  // scoped
        // бизнес-логика возвращает ICycleCounters record (или null)
        return new DailyWithdrawalResult(Processed: 10, Failed: 1, Skipped: 0);
    }
}
```

Сравните с worker'ом до принятия base class — `BackgroundService`,
~110 строк, повторяющийся boilerplate, inconsistent error policy. Теперь:
business-only код в `ExecuteCycleAsync`, всё остальное в base.

## Шесть обязательных паттернов

### 1. Schedule — выбор формы

| Когда | Что | Cron expression vs Interval? |
|-------|-----|-------------------------------|
| Периодическая работа без привязки к "wall clock" (выкатка раз в 5 мин, очистка каждый час) | `IntervalSchedule(TimeSpan)` | НЕ cron |
| Работа привязана к времени суток/недели (02:00 daily, по воскресеньям) | `CronSchedule(expr, TimeZoneInfo)` | ДА cron |
| DB-poll: "drain as fast as possible когда есть работа, sleep когда пусто, sleep при ошибке" | `AdaptivePollSchedule(Idle, Busy, Error)` | НЕ cron |
| One-shot на старте хоста (миграции EF, schema guard, warmup cache) | `StartupSchedule.Instance` | one-time |

Антипаттерн: комбинировать несколько тасок в один worker (каждое — отдельный).

### 2. Worker — return `ICycleCounters` record

```csharp
public sealed record SendBalanceResult(
    [property: Counter("processed")] int Processed,
    [property: Counter("failed")] int Failed,
    [property: Counter("skipped")] int Skipped = 0)
    : ICycleCounters, ISkippedCounters;

protected override async Task<object?> ExecuteCycleAsync(
    WorkerContext context, CancellationToken ct)
{
    // business; collect counters
    return new SendBalanceResult(Processed: 10, Failed: 1, Skipped: 2);
    // null = no counters for this cycle
}
```

`base` рефлексирует свойства с `[Counter]` и эмитит:
- `worker.{WorkerName}.{CounterName}` — `DiagnosticsCounter.Add(value)`

**Опциональные интерфейсы (помимо `ICycleCounters`):**
- `ISkippedCounters` — `{ int Skipped }` для idempotency-хитов
- `IRetriedCounters` — `{ int Retried }` для batch replay

Worker НЕ вызывает `diagnostics.X.Add(1)` напрямую для processed/failed.
Per-entity domain-метрики (e.g. per-advertiser duration) — всё ещё идут
через typed counters на `IXxxDiagnostics`.

### 3. ctor — `IServiceProvider`, base создаёт scope per cycle

```csharp
public sealed class MyWorker(
    IServiceProvider services,      // ← root only; НЕ IServiceScopeFactory
    TimeProvider clock,
    IDiagnosticSource? diagnostics,  // ← может быть null (no telemetry)
    ILogger<MyWorker> logger)
    : ScheduledWorkerBase(services, clock, diagnostics, logger)
```

В `ExecuteCycleAsync` services доступны через `context.Services` — это свежий
DI scope создаваемый base'ом на старте каждого cycle и disposed на выходе.

- ✅ Per-cycle scope — DbContext не leakится между циклами
- ❌ НЕ `IServiceScopeFactory scopeFactory` — base берёт это на себя
- ❌ НЕ передавать `IServiceProvider` worker'у — только base имеет к нему доступ

### 4. Error policy

| Failure mode | Что делает base |
|--------------|-----------------|
| Cycle throws | Log error, increment `outcome=failed` (через Schedule, если `AdaptivePollSchedule`), continue to next tick |
| Stopping token cancelled | Break loop gracefully, run `finally` (logs "stopped"), exit |
| `AdaptivePollSchedule` extra | `RecordCycle(failed: true)` records error backoff |

Base **НЕ** останавливает host при cycle failure. Per-entity try/catch
**внутри** `ExecuteCycleAsync` body обязателен — не валить весь цикл из-за
одной сломанной записи.

### 5. Per-cycle success emission: `WorkerCycleResult.Describe()`

`Describe()` для `Schedule` используется в startup-логе:
- `IntervalSchedule(5m)` → `every 00:05:00`
- `CronSchedule("0 2 * * *", UTC)` → `cron '0 2 * * *' (UTC)`
- `StartupSchedule` → `once on startup`

### 6. Test infrastructure

- Tests вызывают protected `ExecuteAsync` через reflection
- Использовать custom `WorkerSchedule` subclasses которые ждут cancellation после
  N циклов (например `OneTickThenWaitSchedule`) — иначе loop runs forever
- 9 unit tests в `tests/unit/Hybrid.Shared.Kernel.Unit/BackgroundServices/ScheduledWorkerBaseShould.cs`
  покрывают: per-cycle scope, reflection emission (success + failure),
  error continuation, cancellation, Schedule.Describe()

## Регистрация в DI

Поведение DI не меняется — каждый worker — `AddHostedService<T>()`:

```csharp
services.AddHostedService<DailyWithdrawalWorker>();    // singleton per host
services.AddHostedService<SendBalanceToDspWorker>();
services.AddHostedService<OutboxRelayWorker>();
```

Cron expression is bound из `IOptions`:

```csharp
public sealed class BillingCronOptions
{
    public const string SectionName = "Cron:Billing";
    public string DailyWithdrawal { get; init; } = "0 2 * * *";   // 02:00 UTC daily
    public string SendBalanceToDsp { get; init; } = "*/5 * * * *"; // every 5 min
    public string Overlimit { get; init; } = "0 1 * * *";          // 01:00 UTC daily
    public string Reconciliation { get; init; } = "0 3 * * *";    // 03:00 UTC daily
}
```

`Cronos.Parse` fails-fast в `CronSchedule` ctor — misconfig invalidates DI at
boot, не на первом tick.

## Антипаттерны

| Anti-pattern | Why forbidden | Fix |
|--------------|---------------|-----|
| `class XxxWorker : BackgroundService` (наследует напрямую) | Duplicates loop/scope/error boilerplate; bypasses reflection emission; inconsistent error policy | `: ScheduledWorkerBase` |
| `new PeriodicTimer(...)` / `new Task.Delay(...)` in worker loop | Bypasses `Schedule`; not testable | `protected override WorkerSchedule Schedule => new IntervalSchedule(...)` |
| `diagnostics.X.WithTag("outcome", "success").Add(processed)` в worker body | Inconsistent metric naming; bypasses reflection; duplicates counter-emit logic | `[Counter("processed")]` on a record property |
| `var processed = 0; var failed = 0;` returned via out-param or ref | Mixed logic; not typed | Return `record : ICycleCounters` with `[Counter]` props |
| `_ = await Task.Run(...).ConfigureAwait(false)` / `Task.Run(async () => ...)` | Bypasses loop scoping | Just `await` it inline |
| `BackgroundService.ExecuteAsync` returning early without checking token | Crashes entire app | Use `while (!stoppingToken.IsCancellationRequested)` (built into base) |
| `IServiceScopeFactory scopeFactory` in worker ctor | Worker now depends on infra detail that's base's job | `IServiceProvider services` (root) only |
| Creating new `IOptions<T>` snapshot on every Schedule getter | Mutates per-call; record types are value-types and new on every access — `AdaptivePollSchedule.nextDelay` would reset every iteration | Cache `Schedule` in `private readonly` field; **never** `Schedule => new XxxSchedule(...)` |
| Swallowing exceptions silently (`catch { }`) | Telemetry never sees them | Use base loop's exception handler; per-entity try/catch in body |

## PR review checklist

При приёмке PR с новым / изменённым worker'ом:

- [ ] Worker наследует `ScheduledWorkerBase` (НЕ `BackgroundService` напрямую)
- [ ] `WorkerSchedule` — правильный тип для сценария (Interval/Cron/Adaptive/Startup)
- [ ] `Schedule` cached в `private readonly` field (НЕ `Schedule => new ...()` getter)
- [ ] ctor: `(IServiceProvider services, TimeProvider clock, IDiagnosticSource? diagnostics, ILogger<XxxWorker> logger)`
- [ ] `ExecuteCycleAsync` resolves services через `context.Services` (per-cycle scope)
- [ ] Return type — `ICycleCounters` record (или null) с `[Counter]` properties
- [ ] Per-entity try/catch внутри body — не валить весь цикл
- [ ] Cron expressions из `IOptions<XxxCronOptions>`, не hardcoded
- [ ] Test в `tests/unit/Hybrid.Shared.Kernel.Unit/BackgroundServices/` или
      `tests/unit/Hybrid.Modules.X.Unit/Workers/XxxWorkerShould.cs`
- [ ] DI registration — `services.AddHostedService<XxxWorker>()`

Без этого — PR **отклоняется**.

## Migration от raw `BackgroundService`

Существующие workers (legacy модули ещё на raw `BackgroundService`):

1. Поменять ctor на `(IServiceProvider services, TimeProvider clock, IDiagnosticSource? diagnostics, ILogger<XxxWorker> logger)`
2. Переместить `while (!ct.IsCancellationRequested)` логику в base (удалить из `ExecuteAsync`)
3. Заменить `using var scope = scopeFactory.CreateAsyncScope()` на `context.GetService<T>()` внутри body
4. Удалить try/catch boilerplate (base обрабатывает)
5. Удалить ручной `Operation(...).Build()` (base обёртка)
6. Заменить `diagnostics.XxxCount.Add(...)` на `[Counter] int Processed` в return record
7. `Schedule => new XxxSchedule(...)` → `private readonly XxxSchedule schedule = new(...); protected override WorkerSchedule Schedule => schedule;`

После миграции каждый worker меньше на ~60% строк. Commit per worker —
rolling миграция (Phase 17.2 follow-up).

## Связанные правила

- `.agents/rules/observability/diagnostics.md` — when + how to add OTel spans + counters
- `.agents/rules/coding/di-lifetimes.md` — scoped deps / `IServiceScopeFactory`
- `.agents/rules/coding/async-and-tasks.md` — async/await + `CancellationToken` discipline
- `.agents/rules/coding/anti-patterns.md` — record DTO placement, validation, JsonSerializerOptions
- `.agents/rules/process/build-verification.md` — `dotnet build` gate перед commit
- `.agents/rules/process/worker-audit.md` — self-audit checklist
- `src/shared/Hybrid.Shared.Kernel/BackgroundServices/ScheduledWorkerBase.cs` — сама base class
- `src/shared/Hybrid.Shared.Kernel/BackgroundServices/WorkerSchedule.cs` — sealed-hierarchy schedules
- `src/shared/Hybrid.Shared.Kernel/BackgroundServices/WorkerContext.cs` — per-cycle scope context
- `src/shared/Hybrid.Shared.Kernel/BackgroundServices/ICycleCounters.cs` — counter contract
- `src/shared/Hybrid.Shared.Kernel/BackgroundServices/CounterAttribute.cs` — `[Counter]` reflection marker

## Связанные скиллы

- `background-worker-author` — этот же набор правил в форме скилла для агентов