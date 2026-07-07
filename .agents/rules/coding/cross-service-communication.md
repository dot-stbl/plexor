---
description: >-
  cross-service communication between Hybrid.Host.* deploy-apps — Refit HTTP
  contract, IStatisticsApi-style public API, no auth (internal closed network),
  W3C trace + metrics per call, no formal versioning (lock-step deploy).
globs:
  - src/shared/Hybrid.Shared.Contracts/Ports/**/I*.cs
  - src/shared/Hybrid.Shared.Contracts/Statistics/I*.cs
  - src/**/I*Api.cs
  - src/**/Refit*.cs
  - src/**/StatisticsApiClient.cs
priority: high
interactive: false
always: false
---

# Cross-service communication (Hybrid.Host.* ↔ Hybrid.Host.*)

> **Scope**: synchronous + asynchronous calls **between** Hybrid.Host.* deploy-apps (Hybrid.Host → Hybrid.Host.Statistics, Hybrid.Host → Hybrid.Host.Metadata when it lands, etc.). External integrations (Alliance DSP, ClickHouse) live in **dedicated adapter modules**, not under this rule.
>
> **Today**: only `Hybrid.Host.Statistics` exists as a peer deploy-app. `Hybrid.Host.Metadata` is planned (owner-confirmed). Every new cross-service call MUST go through the public kernel contract — never a raw HTTP client or a private in-process call into a peer's module.

## When this rule applies

- Adding a new Refit interface for a peer host (e.g. `IMetadataApi`, `IAuditApi`).
- Calling an existing peer host (`IStatisticsApi`, future `IMetadataApi`) from any module.
- Wiring the HTTP client in DI (base address, resilience handler, diagnostics).
- Designing the request/response shape for a peer contract.
- Debating whether to put business logic in the caller module vs the peer module.

## Pattern (canonical)

```
   Caller module                   Peer host
   ─────────────                   ──────────
   public sealed class FooService(
       IFooApi foo,            ←── Refit-generated proxy
       ILogger<FooService> logger)
   {
       public async Task<X> DoIt(...)
       {
           var response = await foo.PostSomethingAsync(req, ct);
           return response.IsSuccessStatusCode
               ? response.Content
               : /* fallback / exception */;
       }
   }
```

DI registration (kernel, **all** callers):

```csharp
services.AddHttpClient<IFooApi, FooApiClient>(static (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<FooApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
})
.AddStandardResilienceHandler();   // Polly: retry + circuit breaker + timeout

public sealed class FooApiOptions
{
    public const string SectionName = "Foo";           // env: HYBRID_FOO__BASEURL
    public string BaseUrl { get; set; } = "http://localhost:5100";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
```

**Caller NEVER** builds an `HttpClient` directly; **caller NEVER** injects the peer's `DbContext` / `IClickHouseExecutor` / any other infrastructure type. The contract is the only surface.

## Rules

### 1. Contract lives in `Hybrid.Shared.Contracts`

Every cross-service contract is a `public interface` in `src/shared/Hybrid.Shared.Contracts/...` (usually `<Domain>/` subfolder). Caller module + peer module both depend on the same contract assembly. **No** module owns the contract — `IDspClient` is in `Ports/`, `IStatisticsApi` is in `Statistics/`, `IValueDecoder` is in `Statistics/`. Caller-side DI registers a `Refit`-generated client in the same file or a dedicated `Installers/*ClientExtensions.cs`.

```csharp
// Refit attributes in the contract, not in the impl — the Refit source generator
// creates the proxy from the contract alone.
public interface IStatisticsApi
{
    [Get("/api/v1/reporting/campaigns/{campaignId}/daily")]
    public Task<IApiResponse<StatsReport>> GetCampaignDailyAsync(
        ObjectId campaignId, [AliasAs("from")] DateOnly from, [AliasAs("to")] DateOnly to,
        CancellationToken cancellationToken = default);
}
```

### 2. **Caller never sees the peer's infrastructure**

`Hybrid.Modules.Billing.ClickHouseExpenseReader` does **not** inject `IClickHouseExecutor`, `RenderedQuery`, `RawSessionsTable`, or any `DbContext` from a peer module. The query goes through `IStatisticsApi` (the public kernel contract) — full stop. SQL strings are owned by the peer; the caller never writes them. This is the same rule as `coding/di-lifetimes.md` §"Layer dependencies" but for **cross-host** instead of **cross-module**.

If a caller needs data that isn't in the public contract → add the method to the contract first, then implement it in the peer. No bypassing.

### 3. No auth, today

All `Hybrid.Host.*` apps run on the same internal closed network (private subnet / cluster network, no public ingress). Auth is at the **edge** (Hybrid.Host is the only public-facing app via Scalar / OpenAPI; internal hosts have no ingress). The peer-to-peer `IStatisticsApi` etc. are unauthenticated. **Do not** add JWT / API-key / mTLS at the inter-service layer until the deployment model changes (public ingress, multi-tenant, or per-host public endpoint). When it does — see the migration checklist at the bottom of this file.

When auth is added later, the migration is:
1. New `IAuthenticatedPeerClient` interface in `Hybrid.Shared.Contracts` (still kernel).
2. Per-host service-account in `Identity` (the existing `ApiKey` scheme — `Phase 7` style — is sufficient; no new auth module).
3. `AddHttpClient<IFooApi, FooApiClient>` adds `.AddHttpMessageHandler(sp => new ApiKeyHandler(...))` — the *contract* is unchanged.

### 4. No formal versioning; **lock-step deploy**

We do not run multiple versions of `Hybrid.Host.Statistics` in production — every host is deployed as a single instance per environment. Therefore there is no `/api/v2/` style URL versioning for inter-service contracts. When a breaking change is needed:

- Update the contract in `Hybrid.Shared.Contracts`.
- Update the caller module(s) to use the new shape.
- Update the peer host to implement the new shape.
- Deploy in lock-step (peer first, then caller, same release window).
- Old contract is removed in the same release.

If multi-version deployment is ever needed (e.g. a rolling upgrade with mixed versions), the **migration** is:
- Add the new shape as additive (new optional field, new method).
- Both versions coexist.
- Switch callers atomically.
- Then remove the old shape.

This is documented in the same `versioning` section of `api-design.md` §5 for HTTP controllers; the principle is identical.

### 5. W3C trace + metrics per call

Every cross-service call MUST be observable:

```csharp
public sealed class FooService(
    IFooApi foo,
    IOutboxDiagnostics diagnostics)   // OR the peer's diagnostics interface
{
    public async Task<X> DoIt(FooRequest req, CancellationToken ct)
    {
        using var op = diagnostics.Operation($"foo.peer.call")
                .WithTag("peer", "host.Foo")
                .WithTag("operation", "PostSomething")
                .WithHistogram(diagnostics.CallDuration)
                .Build();
        try
        {
            var response = await foo.PostSomethingAsync(req, ct);
            op.WithTag("outcome", response.IsSuccessStatusCode ? "success" : "failed");
            diagnostics.CallCount.WithTag("peer", "host.Foo")
                                .WithTag("outcome", response.IsSuccessStatusCode ? "success" : "failed")
                                .Add(1);
            return response.IsSuccessStatusCode ? response.Content : throw new InvalidOperationException(...);
        }
        catch (Exception ex)
        {
            op.RecordException(ex);
            throw;
        }
    }
}
```

The OTel `ActivitySource` registered in `Hybrid.Shared.Telemetry` already propagates W3C `traceparent` + `tracestate` headers over the Refit-generated `HttpClient` — no extra config needed. Per `observability/diagnostics.md`, **every component that talks to a peer MUST emit a counter with `peer` and `outcome` tags**.

If the caller module is currently emitting counters with a different tag set, **migrate** the existing code, do not skip the `peer` tag.

### 6. Failure semantics: `IApiResponse<T>`, not `T`

Per `IStatisticsApi` XML doc: the Refit method signature is **`Task<IApiResponse<T>>`**, not `Task<T>`. The default Refit signature throws `ApiException` on non-2xx, which crashes the worker loop. We want the caller to **inspect the status** and degrade gracefully (skip, retry later, surface metric). The convention:

```csharp
var response = await foo.PostSomethingAsync(req, ct);
if (response.IsSuccessStatusCode) { return response.Content; }
diagnostics.CallCount.WithTag("peer", "host.Foo").WithTag("outcome", "http_error").Add(1);
logger.LogWarning("peer foo returned {StatusCode}: {Error}", response.StatusCode, response.Error?.Content);
// Decide: rethrow, return null, or throw a domain-specific exception
throw new PeerUnavailableException("foo", response.StatusCode);
```

`PeerUnavailableException` lives in `Hybrid.Shared.Kernel` (or a dedicated `Hybrid.Shared.Communication` once we have one); per-host it can have a more specific subclass with a richer payload.

### 7. Resilience: `AddStandardResilienceHandler` is mandatory

Every peer-client registration includes Polly's standard resilience handler:

```csharp
services.AddHttpClient<IFooApi, FooApiClient>(...)
    .AddStandardResilienceHandler();
```

The default Microsoft.Extensions.Http.Resilience policy covers retry (3 attempts, exponential backoff with jitter) + circuit breaker (5 failures in 30s opens for 30s) + total request timeout (10s default). **Do not** customise these without a documented reason in the consumer's PR.

`AddStandardResilienceHandler` is applied AFTER `BaseAddress` configuration; the handler is per-client, not global. Two `IFooApi` and `IBarApi` clients have independent retry / breaker state.

### 8. Test pattern (unit, no real host)

Every cross-service caller has a unit test that **fakes the Refit client via NSubstitute**, not a real HTTP server. The fake is a `Substitute.For<IFooApi>()` whose `PostSomethingAsync(...)` returns a `new HttpResponseMessage<T>(HttpStatusCode.OK, value) { Content = ... }` wrapped via Refit's `IApiResponse` constructor.

For integration tests (deferred to `tests/integration/`):
- Spin up the peer host in-process via `WebApplicationFactory<Program>`.
- Use the real Refit client pointed at `factory.CreateClient()`.
- Test 4xx / 5xx / timeout scenarios.
- Do not put integration tests in unit suites — they are slow and require real infra.

## Anti-patterns (rejected by this rule)

| Anti-pattern | Why rejected |
|--------------|--------------|
| Caller injects the peer's `IClickHouseExecutor` / `DbContext` / `RenderedQuery` directly | Breaks the "no SQL in caller" rule. Per-host infrastructure stays in the host. |
| Caller builds a `new HttpClient()` for a peer | Bypasses the Refit-generated proxy and the resilience handler. Always `AddHttpClient<IFooApi, FooApiClient>`. |
| Caller adds `services.AddHttpClient(...)` for a raw URL | The Refit proxy is the contract; raw URLs are not. If you need a one-off endpoint, add a method to the contract first. |
| Caller writes `HttpClient.DefaultRequestHeaders.Add("X-Api-Key", ...)` | Auth at this layer is **not** required today. If you find yourself adding it, file a question — the project rule is "internal closed network". |
| Caller uses `IApiResponse.Error.Content` as a happy path | Treat non-2xx as failure. The `Error` is for logging only. |
| Caller catches `ApiException` from a `Task<T>` method | Use the `IApiResponse<T>` signature. Catching exceptions is for **infrastructure** errors (DNS, TLS, timeout) only. |
| Caller hard-codes `http://localhost:5100` | Use `IOptions<FooApiOptions>` bound from `HYBRID_FOO__BASEURL`. |
| Caller does `cancellationToken.ThrowIfCancellationRequested()` in addition to `ct` | The `ct` already propagates cancellation; manual checks duplicate the work. |
| Caller adds cross-service tracing via custom `ActivitySource` instead of `Operation` scope | `Operation` scope already wraps `ActivitySource.StartActivity` + `RecordException` + emits the standard spans. |
| Caller registers the same peer client twice (once per module) | **One** registration per contract in the kernel — `AddFooApiClient()` extension. Modules add the contract, not the registration. |

## What is **not** covered by this rule

| Concern | Covered by |
|---------|-----------|
| User-facing HTTP API (controllers, OpenAPI) | `api-design.md` |
| DI lifetimes and per-request scope | `di-lifetimes.md` |
| `IOptions<T>` pattern and `[Range]` validation | `di-options.md` |
| Outbox events (kernel-to-kernel via DB, not HTTP) | `coding/anti-patterns.md` §"Outbox" + `kernel-outbox.md` (Phase 16 design) |
| External integrations (Alliance DSP, ClickHouse) | Each in its own adapter module + `coding/di-lifetimes.md` §"HTTP clients" + `IStatisticsApi.cs` as a model |
| In-process module-to-module (same host) | `di-lifetimes.md` + layer rules in `coding/PROJECT-STRUCTURE.md` |
| Async messaging (Kafka, RabbitMQ) | Deferred — when added, will be a new rule (`coding/async-messaging.md`). For now, outbox-only. |

## Migration checklist (when the auth model changes)

1. New `IAuthenticatedPeerClient` interface in `Hybrid.Shared.Contracts` (Refit-friendly marker).
2. New `ApiKeyHandler : DelegatingHandler` in `Hybrid.Shared.Kernel` (or `Hybrid.Shared.Communication`).
3. `Identity` module: extend the existing `ApiKey` scheme with a "service-account" type (per-host API key, stored in `Identity.ApiKey` table).
4. `AddHttpClient<IFooApi, FooApiClient>` registration: `.AddHttpMessageHandler(sp => new ApiKeyHandler(sp, peer: "host.Foo"))`.
5. `FooApi` host startup: reads its own service-account API key from `HYBRID_FOO_IDENTITY__SERVICEACCOUNT` env var, validates inbound `X-Api-Key` header on `/api/v1/*` endpoints.
6. Test matrix: 2xx with key, 401 without key, 401 with wrong key, 503 when peer is down.
7. Update this rule: remove the "no auth today" section, point to `auth/migration.md` (new file).

## Enforcement

- **Code review**: PR reviewers MUST reject any change that introduces a raw `HttpClient` for a peer host, a private peer call (`services.AddDbContext<PeerDbContext>` in a non-peer module), or a hand-rolled `Task<HttpResponseMessage>` instead of a Refit interface.
- **Architecture tests** (`tests/unit/Hybrid.ArchitectureTests/`): add a rule that searches for `AddHttpClient<` in module projects, ensures the type parameter is a `public interface` in `Hybrid.Shared.Contracts` and is not in the **same** module's namespace.
- **PR template**: reviewers MUST see a "cross-service" checkbox ticked if the PR touches any file under `src/shared/Hybrid.Shared.Contracts/Statistics/` or adds an `I*Api` interface anywhere.

## See also

- `coding/di-lifetimes.md` §"HTTP clients" — base pattern (this rule is a strict superset for cross-service calls)
- `coding/api-design.md` — user-facing controller design (related but different scope)
- `observability/diagnostics.md` — per-component diagnostics surface
- `src/shared/Hybrid.Shared.Contracts/Statistics/IStatisticsApi.cs` — the canonical example of a public peer contract
- `kernel-outbox.md` (Phase 16 design) — async path via outbox events (out of scope here)