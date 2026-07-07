# HANDOFF — контекст для новой сессии pi

> Скопируй содержимое этого файла и вставь как первый промт в свежем окне
> pi-coding-agent, чтобы продолжить работу над Plexor без потери контекста.

---

## Что мы строим

**Plexor** — self-hosted cloud platform (типа Yandex Cloud) для команды
`.stbl`. MVP = 8-10 сервисов, расширяемый через provider-plugin pattern.
Stack: .NET 10 + Vite/React.

**Команда .stbl**, лид — bradw (senior C#). Текущая задача: разработать архитектуру,
подготовить документацию, спроектировать UI в OpenDesign.

## Репо и reference

- **Наш репо**: `C:\Users\bradw\source\stbl\plexor` (только что развернули)
- **Reference** (gold standard .NET проект, чужой, НЕ трогаем):
  `C:\Users\bradw\source\hybrid\console.x`
  - Оттуда взяли: `.editorconfig`, `Directory.Build.props/targets`,
    `Directory.Packages.props`, naming conventions, test stack,
    build gate pattern.

## Что уже сделано в этой сессии

1. **Структура репо** готова: `.agents/rules/` (30 файлов правил),
   `.agents/docs/` (23 файла архитектурной и UX-документации).
2. **Build system работает**: `dotnet build plexor.slnx -c Debug`
   даёт 0 warnings / 0 errors. Два гейта в `Plexor.Build.Tools.targets`:
   - `VerifyFormatOnBuild` (dotnet format)
   - `VerifyAntiPatternsOnBuild` (RoslynCodeTaskFactory — ловит
     `.ConfigureAwait(false)` и `var x = ...; if (x is null)` на
     отдельных строках).
3. **.NET структура** (30 csproj + 10 test projects):
   - `src/shared/Plexor.Shared.{Kernel,Contracts,Id,Persistence,Telemetry,Http,Composition}`
   - `src/modules/Plexor.Modules.<X>/{Domain,Application,Infrastructure}` — Compute, Network, Storage, Identity, Tenants, Billing, Telemetry
   - `src/host/Plexor.{Host,Migrator,NodeAgent}`
   - `src/installer/Plexor.Installer.Cli` (NativeAOT)
   - `src/providers/Plexor.Core.Providers` + 5 конкретных провайдеров (Kvm/MinIo/Ovs/Ubuntu/K3s)
   - `src/build/Plexor.Build.Tools` (targets)

## Ключевые РЕШЕННЫЕ вопросы (НЕ пересматривать без явного запроса)

| Что | Значение |
|-----|----------|
| **Продукт** | Plexor |
| **CLI prefix** | `plx` (NativeAOT) |
| **.NET version** | **.NET 10** (НЕ .NET 9) |
| **Architecture** | Modular monolith + Node agents (НЕ microservices) |
| **Provider pattern** | IProvider + I<Resource>Provider, plugin через NuGet, `plx provider install <pkg>` |
| **MVP scope** (8-10 svc) | VM, Volume, Bucket, VPC+Subnet+SG, Floating IP, LB, IAM (users/roles/SSH keys), Tenant+Project, Billing |
| **UI стек** | Vite + React 18 + TS + shadcn/ui + Tailwind + TanStack Query + Zustand |
| **UI routing** | TanStack Router (file-based, type-safe) |
| **OpenDesign** | Open-source дизайн-тулз (типа Figma-альтернатива). Лид рисует UI там, я даю брифы |
| **Test stack** | xUnit + Shouldly + NSubstitute + Bogus + Testcontainers + Respawn + NetArchTest + BenchmarkDotNet |
| **Mapper** | Riok.Mapperly (НЕ AutoMapper) |
| **Validation** | FluentValidation |
| **Service bus** | NATS (НЕ Kafka/RabbitMQ) |
| **DB** | PostgreSQL + EF Core 10 + Dapper для raw queries |
| **API docs** | Scalar (НЕ Swagger UI) |
| **Auth provider** | Keycloak (production), local (MVP/dev) |
| **Storage providers MVP** | Ceph (block+object), MinIO (object only) |
| **Compute providers MVP** | KVM, LXD, Pod (для dev) |

## Конвенции кода (ОБЯЗАТЕЛЬНО соблюдать)

- **Host-программы**: top-level statements заканчиваются на `app.Run()` (sync, void),
  НЕ `await app.RunAsync().ConfigureAwait(false)`. Это даёт VSTHRD/MA0004 violations.
- **Async**: методы с `Task/ValueTask` ВСЕГДА имеют суффикс `Async` (VSTHRD200 = error)
- **`ConfigureAwait(false)` ЗАПРЕЩЁН** в app code (MA0004 + VerifyAntiPatternsOnBuild)
- **CancellationToken** — последний параметр, пробрасывается во все async-вызовы
- **File-scoped namespaces**: `namespace Foo;` (не блочные)
- **`var` всегда** (IDE0007 = error)
- **sealed** для всех internal/public классов без наследников (CA1852)
- **`<TargetFramework>net10.0</TargetFramework>`** везде
- **`<Nullable>enable</Nullable>`**, **`<ImplicitUsings>enable</ImplicitUsings>`**
- **`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`**
- **Analyzers**: Roslynator + Meziantou + VSTHRD (копия из reference)
- **Endpoint URL pattern**: `/api/v1/{module}/{resource}`
- **Provider plugins** в `src/providers/Plexor.Providers.<X>.<Y>`
- **Tests**: `tests/unit/Plexor.<X>.Unit` и `tests/integration/Plexor.<X>.Tests`
- **Naming**: `Plexor.Modules.<X>.<Layer>`, `Plexor.Shared.<X>`, не "Utils/Common/Helpers"

## Что в работе сейчас

Лид рисует UI в **OpenDesign** по брифам из `.agents/docs/ui/screens/`.
Готовых дизайнов пока нет.

## Открытые вопросы / Roadmap

1. **(в работе)** UI в OpenDesign — нужен фидбек на готовые дизайны
2. **API контракты** — вывести из wireframes
3. **Domain model** — вывести из API контрактов
4. **Код первого модуля** — начнём с `Plexor.Core.Providers` SDK
5. **Installer autodetect + resolver** — Spectre.Console TUI
6. **Architecture runtime-протоколы** — resource state machine, reconciliation loop, eventing semantics, multi-tenancy enforcement (детально описал в предыдущей беседе, готовы к реализации когда дойдёт очередь)

## Файлы, которые нужно прочитать ПЕРВЫМИ в этом порядке

1. `.agents/docs/README.md` — навигация
2. `.agents/docs/scope.md` — что в MVP, что нет
3. `.agents/docs/architecture.md` — слои и data flow
4. `.agents/docs/modules.md` — каждый модуль
5. `.agents/docs/providers.md` — каталог провайдеров + SDK
6. `.agents/docs/yandex-cloud-parity.md` — маппинг на YC
7. `.agents/docs/ui/README.md` — вход в UX-документацию
8. `Directory.Build.props` + `Directory.Packages.props` — build setup
9. `plexor.slnx` — структура солюшена
10. `.agents/HANDOFF.md` (этот файл)

## Что НЕ делать

- ❌ Не путать наш репо с reference (`C:\Users\bradw\source\hybrid\console.x`) — туда НЕ лезем
- ❌ Не использовать `await app.RunAsync()` или `.ConfigureAwait(false)` в host-программах
- ❌ Не рефакторить `Directory.Build.props/targets` без моего запроса
- ❌ Не править `.editorconfig` severity без моего запроса
- ❌ Не менять naming conventions без моего запроса
- ❌ Не использовать .NET 9 или старше — везде .NET 10
- ❌ Не использовать AutoMapper (используем Riok.Mapperly)
- ❌ Не использовать Swagger UI (используем Scalar)

## Как со мной работать (стиль общения)

- **Я senior C# разработчик**, токсичный минимум, прямой стиль
- Общаемся **на русском**, английский для технических терминов
- Люблю короткие ответы и конкретный код
- Если нужно посоветоваться — спрашиваю прямо
- Если что-то непонятно в моем запросе — спроси **до** того как начнёшь действовать
- Если я говорю "примени" / "apply" / "код" — пиши код, не доку
- Если "промты" / "брифы" — пиши в `.agents/docs/`
- Если "обсуждаем" — задавай вопросы, предлагай варианты

## Стартовый промт

При старте сессии скажи:

> Прочитал HANDOFF. Состояние понятно. Что делаем — продолжаем документацию,
> переходим к API/domain-модели, или пишем код модуля?

---

Версия handoff: 0.1.0-dev · обновлено: 2026-07-07