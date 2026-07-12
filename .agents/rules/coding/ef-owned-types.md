---
description: ef core owned types preferred over manual JSON parse for polymorphic stored values (settings, feature flags, audit payloads)
globs: ["**/*.cs"]
priority: medium
---

# EF Core Owned types — preferred over manual JSON parse

## When to use

Polymorphic stored values (settings, feature flags, audit payloads, etc.)
MUST use EF Core `OwnsOne` / `OwnsMany` with `HasDiscriminator` instead of
manually serialising to a JSONB column and then parsing on the way out.

## Anti-patterns (forbidden)

```csharp
// ❌ Wrong — manual JSON parse in domain code, JSON round-trip on every read
public ISetting AsISetting(SettingKey key)
{
    using var doc = JsonDocument.Parse(ValueJson);
    return SettingDictionaryExtensions.Materialise(
        TypeName, key, doc.RootElement.Clone());
}

// ❌ Wrong — pragma to hide unavoidable sync I/O over a string
#pragma warning disable VSTHRD103
var valueJson = JsonSerializer.Serialize(setting, SerializerOptions);
#pragma warning restore VSTHRD103

// ❌ Wrong — string column "type" + string column "value_json", parsed at every read
public string Type { get; set; }
public string ValueJson { get; set; }
```

`JsonDocument.Parse(string)` and `JsonSerializer.Serialize<T>(T)` are
**unavoidably synchronous** in .NET 10 — there is no async variant for
parsing from a string (only for `PipeReader` streams). Hiding the warning
with a pragma is a code smell: the code is doing manual work that EF Core
can do for free.

## Correct pattern

```csharp
// ✅ Domain entity — typed property, EF hydrates directly
public sealed class AgencyFeatureFlagEntity
{
    public string AgencyId { get; set; }
    public string SettingKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Owned — discriminated union via EF HasDiscriminator
    public ISetting Setting { get; set; } = null!;
}
```

EF mapping (one column per concrete setting type, discriminator picks
which one is populated):

```csharp
b.Entity<AgencyFeatureFlagEntity>(e =>
{
    e.ToTable("agency_feature_flags", "tenants");
    e.HasKey(x => new { x.AgencyId, x.SettingKey });

    e.Property(x => x.AgencyId).HasColumnName("agency_id").HasMaxLength(24);
    e.Property(x => x.SettingKey).HasColumnName("setting_key").HasMaxLength(128);
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    e.OwnsOne(x => x.Setting, s =>
    {
        s.Property(p => p.Key).HasColumnName("setting_key").HasMaxLength(128);

        s.HasDiscriminator<string>("type_name")
            .HasValue<BoolSetting>(nameof(BoolSetting))
            .HasValue<StringListSetting>(nameof(StringListSetting))
            .HasValue<NumberSetting>(nameof(NumberSetting))
            .HasValue<ObjectSetting>(nameof(ObjectSetting));

        // per-type column mapping
        s.Property(typeof(bool), "Value")
            .HasColumnName("value_bool")
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        s.Property(typeof(IReadOnlyList<string>), "Values")
            .HasColumnName("value_string_list")
            .HasColumnType("jsonb");

        s.Property(typeof(decimal), "Value")
            .HasColumnName("value_number")
            .HasColumnType("numeric(38,18)");

        s.Property(typeof(JsonElement), "Value")
            .HasColumnName("value_object")
            .HasColumnType("jsonb");
    });
});
```

## Repository side

```csharp
// ✅ Repository — single roundtrip, typed dictionary
public async Task<IReadOnlyDictionary<string, ISetting>> GetAllForAgencyAsync(
    AgencyId agencyId, CancellationToken ct = default)
{
    var agencyIdStr = agencyId.Value.ToString();
    return await dbContext.Set<AgencyFeatureFlagEntity>()
        .Where(e => e.AgencyId == agencyIdStr)
        .ToDictionaryAsync(
            keySelector: e => e.SettingKey,
            elementSelector: e => e.Setting,    // typed — EF already hydrated
            StringComparer.Ordinal,
            cancellationToken: ct);
}

// ✅ SetAsync — no manual serialise, EF generates UPDATE/INSERT
public async Task SetAsync(
    AgencyId agencyId, ISetting setting, CancellationToken ct = default)
{
    var existing = await dbContext.Set<AgencyFeatureFlagEntity>()
        .FirstOrDefaultAsync(
            e => e.AgencyId == agencyId.Value.ToString()
              && e.SettingKey == setting.Key.Canonical,
            ct);

    if (existing is not null)
    {
        existing.Setting = setting;       // EF tracks Owned property change
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }
    else
    {
        await dbContext.Set<AgencyFeatureFlagEntity>().AddAsync(new AgencyFeatureFlagEntity
        {
            AgencyId = agencyId.Value.ToString(),
            SettingKey = setting.Key.Canonical,
            Setting = setting,             // EF INSERTs with discriminator + value column
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }, ct);
    }
}
```

## Why

1. **No `JsonDocument.Parse` in hot path** — EF handles hydration to typed records.
2. **No `pragma VSTHRD103`** — no manual sync I/O over a string.
3. **Type safety** — adding a new `ISetting` subtype fails compile if not mapped (missing
   `HasValue<NewType>` or column binding is a build error, not a runtime surprise).
4. **Single source of truth** — column metadata (length, type, nullability) lives in EF
   config, not scattered across entity + repository + Materialise switch.
5. **SQL transparent** — `INSERT` / `UPDATE` per setting type is generated by EF; the
   discriminator is one column update, not a JSON rewrite.
6. **No race conditions in Materialise switch** — the switch is removed entirely; EF
   does the polymorphism.

## Materialise — when still needed

`SettingDictionaryExtensions.Materialise` is still used by the **API materialise
path** — when a client sends a setting as raw JSON in a POST/PUT body and the
controller has to turn it into a typed `ISetting` before handing it off to the
handler. That path is one-shot (request boundary), not in the read hot path.

## When NOT Owned types

- **Truly free-form JSON** with no shape contract (audit events, user-typed
  content). Use `jsonb` column + `JsonElement` directly. No domain entity for
  these.
- **Migration cost too high** — see `process/migrations.md` for hybrid
  strategies. Owned is preferred but not retroactive for legacy tables without
  a strong reason.
- **Cross-aggregate composition** — Owned types only model "one owned
  entity per parent". For many-to-one polymorphic collections, use a
  dedicated aggregate (e.g. `SettingEntry` table) with its own repository.

## Enforcement

- **Build gate**: `dotnet build console.x.slnx -c Debug` (analyzers + format).
- **Code review**: any new `JsonDocument.Parse` / `JsonSerializer.Serialize` on
  an entity column, paired with a `TypeName` discriminator string, is a
  reviewer-flagged pattern that should be replaced by Owned types.
- **Architecture test** (planned): grep for `using var doc = JsonDocument.Parse`
  in `Domain/Entities/` and `Application/Persistence/` — should be empty
  except for the API materialise layer.
