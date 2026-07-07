---
description: c# xml documentation, model placement, required analyzer tooling — документация и где живут модели
globs: ["**/*.cs", "**/*.csproj"]
always: true
---

# Class layout, XML docs, required tooling

Этот файл — правила документации, размещения моделей и обязательного
tooling'а. Naming/primary ctor/var/braces — в соседних файлах.

## 1. XML documentation — REQUIRED на public API

### Где `<summary>` обязателен

- **Interfaces**: все члены.
- **Base classes** (от которых наследуются): все public members.
- **Public API** конкретных классов и records.
- **Private fields and methods**: обязательно `<summary>` (не `<inheritdoc/>`).

### `<inheritdoc/>` — для наследников

```csharp
/// <summary>Base user entity.</summary>
public abstract class User
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }
}

public sealed class ApplicationUser : User
{
    /// <inheritdoc />
    public new Guid Id { get; init; }

    /// <summary>Internal session cache.</summary>
    private readonly ConcurrentDictionary<string, Session> sessions = new();
}
```

### Что НЕ требует summary

- `Dispose` / `DisposeAsync` — стандартный паттерн.
- Locally-scoped helper methods (не private — а локальные функции внутри метода).

**Enforcement:** CS1591 (severity=error в `.editorconfig`).

---

## 2. Project structure — где разрешены модели

Модели разрешены в проектах с `Models` / `Entities` / `Contracts` в имени
или пути:

- `src/models/**`
- `src/entity/**`
- `src/feature/*/Models/**`
- `src/feature/*/Entities/**`
- `src/feature/*/Contracts/**`
- `src/api/*.Api.Models/**` — отдельный проект для API DTO/Request/Response

### Где НЕ разрешены

- В API-проектах (`*.Api.*`) — кроме папок `Contracts/` или вынесенного
  `*.Api.Models` проекта.
- В worker-проектах — модели идут в общий entity-проект.

**Исключение:** модель, используемая только внутри одного проекта и не
выходящая наружу — допустима локально в этом проекте, в папке `Models/`.

**Enforcement:** architecture test (NetArchTest) — модели только в Models/Entities/Contracts проектах.

---

## 3. Required tooling

### `.editorconfig`

Лежит в корне репозитория. Все правила там — `severity = error` для
критичных или `warning` для рекомендаций.

### NuGet packages (`Directory.Packages.props` / csproj)

```xml
<PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="*" />
<PackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" Version="*" />
<PackageReference Include="Meziantou.Analyzer" Version="*" />
<PackageReference Include="Roslynator.Analyzers" Version="*" />
```

| Package | Что ловит |
|---------|----------|
| `Microsoft.CodeAnalysis.NetAnalyzers` | CA-правила: `CA1852` (sealed), `CA2254` (logging), `CA1862` (strings) |
| `Microsoft.VisualStudio.Threading.Analyzers` | `VSTHRD200` (async suffix), `VSTHRD103` (`.Result`/`.Wait()`) |
| `Meziantou.Analyzer` | `MA0004` (ConfigureAwait), single-letter lambda warnings |
| `Roslynator.Analyzers` | минимальность кода, pattern matching упрощение |

### Architecture tests

Что `.editorconfig` не ловит — проверяется через NetArchTest или ArchUnitNET
в xUnit:

- Интерфейсы лежат в `Interfaces/`.
- Модели лежат только в Models/Entities/Contracts проектах.
- Controllers зависят только от сервисов из allowed-сборок.
- Запрет на reference между bounded contexts.

```csharp
[Fact]
public void Interfaces_should_live_in_Interfaces_folder()
{
    var result = Types.InAssembly(typeof(IUserService).Assembly)
        .That()
        .AreInterfaces()
        .And()
        .DoNotHaveNameMatching("^I[A-Z].*Markers$")
        .Should()
        .ResideInNamespaceMatching(@".*\.Interfaces(\..*)?$")
        .GetResult();

    Assert.True(result.IsSuccessful,
        $"Interfaces outside Interfaces/ folder: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
```

---

## Связанные правила

- `naming-and-types.md` — naming, sealed, record vs class
- `constructors-and-fields.md` — primary ctor, fields, constants
- `code-shape.md` — pattern matching, var, braces, no #region
- `anti-patterns.md` — records DTO placement, validation
- `analyzers.md` — analyzer packages wiring