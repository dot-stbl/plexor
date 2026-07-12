---
description: "Don't use `this.field = field;` to disambiguate constructor parameters that shadow field names. Rename the parameter (or the field) so the assignment is unambiguous without `this.`."
globs: ["**/*.cs"]
priority: low
---

# No `this.` qualifier — rename instead

> **When a constructor parameter shadows a field name, rename the parameter (or the field) so the assignment is unambiguous.** `this.value = value;` is a code smell — it suggests the naming wasn't thought through.

## The rule

If a ctor parameter has the same name as a field, ONE of them gets renamed. Default: rename the **parameter** (the field is the "canonical" name; the parameter is a transient input).

This is a low-priority style preference. The build doesn't fail on `this.` usage. Code review catches it. But on first write, prefer the rename.

## Examples

```csharp
// ✓ Correct — parameter renamed
public PasswordHash(string hash)
{
    if (!IsWellFormed(hash))
    {
        throw new IdentityException(...);
    }
    value = hash;  // field is `value`, parameter is `hash`
}
```

```csharp
// ✗ Wrong — `this.` qualifier
public PasswordHash(string value)
{
    if (!IsWellFormed(value))
    {
        throw new IdentityException(...);
    }
    this.value = value;  // field and parameter both `value`
}
```

## When `this.` IS appropriate

- **Inside a struct constructor** when C# requires `this.field` to disambiguate (struct has field-like semantics for `this`).
- **Indexer / property setter** when there's a parameter named `value` and a field also named `value` (extremely rare; don't hand-roll these).

## Self-audit grep

```bash
# Constructor body containing `this.<name> = <name>;`
rg -n "this\.\w+ = \w+;" src/ --type cs
# → Each hit: rename the parameter.
```

## Related

- `coding/constructors-and-fields.md` §Primary constructors — most of these cases go away when the field is a primary ctor parameter (no shadowing).