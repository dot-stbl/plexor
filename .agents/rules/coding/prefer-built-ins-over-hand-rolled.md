---
description: "When .NET BCL, ASP.NET Core, EF Core, or Microsoft.Extensions.* provide a primitive for what the code does, use it. Hand-rolled crypto, password hashing, or DI helpers are a smell."
globs: ["**/*.cs"]
priority: high
---

# Prefer built-in over hand-rolled

> **If .NET / ASP.NET Core / EF Core / Microsoft.Extensions.* ships a primitive for what the code does, use the primitive.** Hand-rolled crypto, password hashing, DI helpers, claims-issuance, signing-key management are smells — they duplicate hardened, audited platform code with subtle differences in salt format, padding, key rotation, etc.

## The rule

When introducing a primitive in Plexor, ask: *does the platform already ship this?* If yes, use the platform primitive; if the platform primitive is missing a feature Plexor needs, extend it locally (sealed subclass or extension method), do not reinvent.

### Mapping

| Need | Use | Don't hand-roll |
|---|---|---|
| Password hashing | `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` (PBKDF2) | custom bcrypt, custom PBKDF2 |
| Reversible crypto (secrets, keys) | `Microsoft.AspNetCore.DataProtection.IDataProtector` | custom AES, custom RSA-with-passphrase |
| User CRUD persistence | `Microsoft.AspNetCore.Identity.IUserStore<TUser>` + `IUserPasswordStore<TUser>` + `IUserLockoutStore<TUser>` | direct DbContext queries against `users` table |
| Claims issuance | `Microsoft.AspNetCore.Identity.UserClaimsPrincipalFactory<TUser>` | hand-rolled ClaimsPrincipal builders |
| Signing key private keys at rest | `IDataProtector` for the private key in DB | custom key-store with manual rotation |
| Password reset tokens | `DataProtectionTokenProvider<TUser>` | custom token store with random 256-bit tokens |
| Two-factor TOTP | `Microsoft.AspNetCore.Identity.AuthenticatorTokenProvider<TUser>` (or `Otp.NET` directly) | hand-rolled RFC 6238 |
| DI lifetime wiring | `Microsoft.Extensions.DependencyInjection` + `Scrutor` for scanning | custom service locators |

## What we DON'T do

- **Don't pull in `Microsoft.AspNetCore.Identity` wholesale** — its `IdentityUser` / `IdentityRole<TKey>` base classes bake in email confirmation, phone number, security stamp, two-factor enabled, etc. Use the *primitives* (PasswordHasher, IUserStore, IDataProtector) with our own User / Role entities.
- **Don't fall into `UserManager<TUser>` / `SignInManager<TUser>`** — they're coupled to `IdentityUser` + cookie-based sign-in + the `IdentityDbContext`. We're JWT-bearer, not cookie.

## Self-audit grep

```bash
rg -n "BCrypt\.|HashAlgorithm\.|Aes\.|RSA\." src/ --type cs
rg -n "new ClaimsPrincipal|new ClaimsIdentity" src/ --type cs
```

Each hit is a candidate for replacement with the platform primitive on the right side of the table.

## Related

- `architecture/identity.md` — what we adopt from ASP.NET Core Identity, what we don't.