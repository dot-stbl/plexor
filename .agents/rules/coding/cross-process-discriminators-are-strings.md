---
description: "Discriminator values that cross process boundaries (HTTP, RPC, OpenAPI, log streams) are string constants, not C# enums. Enums force a client redeploy on every value addition; strings stay stable."
globs: ["**/*.cs"]
priority: high
---

# Cross-process discriminators are strings, not enums

> **Enum types must not be used for discriminator values that leave the process** (HTTP response codes, OpenAPI ProblemDetails.type, log event names, RPC error codes, RBAC permission strings). The constant-value string is the wire format.

## The rule

When a class of values needs to be sent in an HTTP response, persisted to disk and read by a different process, exposed in an OpenAPI document, or round-tripped to a frontend, use `public static class Name { public const string Foo = "domain.foo"; }` instead of `public enum Name { Foo }`.

Strings stay stable across renames; enums force every client to redeploy on every value addition. openapi-typescript + kubb codegen reads the string from the OpenAPI document and produces a typed union — no C# rebuild needed.

## Examples

```csharp
// ✓ Correct — string constants
public static class IdentityExceptions
{
    public const string InvalidEmail = "identity.email.invalid";
    public const string AccountLocked = "identity.account.locked";
}

public sealed class IdentityException(string code, string message) : Exception
{
    public string Code { get; } = code;
}
```

```csharp
// ✗ Wrong — enum forces every client to redeploy on a new value
public enum IdentityExceptionKind
{
    InvalidEmail,
    AccountLocked,
}

public sealed class IdentityException(IdentityExceptionKind kind, string message) : Exception
{
    public IdentityExceptionKind Kind { get; } = kind;
}
```

## Self-audit grep

```bash
rg -in "public enum \w+Kind\b|public enum \w+Type\b" src/ --type cs
# → Each hit is a candidate. If used as a wire-format discriminator
#   (HTTP / RPC / OpenAPI), replace with a string constants class.
#   Pure internal enums (algorithm state, config flags) stay as enum.
```

## Related

- `coding/anti-patterns.md` — postfixes (Dtos/Models/Impls forbidden)
- `architecture/identity.md` §RBAC — `PermissionScope` is a string for the same reason.