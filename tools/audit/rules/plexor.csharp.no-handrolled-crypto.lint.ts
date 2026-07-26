/**
 * plexor.no-handrolled-crypto -- project rule for Plexor.
 * Mirrors .agents/rules/coding/prefer-built-ins-over-hand-rolled.md.
 *
 * If .NET / ASP.NET Core / EF Core / Microsoft.Extensions.* ships a
 * primitive for what the code does, use the primitive. Hand-rolled
 * crypto, password hashing, key wrapping, or DI helpers are smells.
 *
 * Banned (in product code):
 *   BCrypt.*                 (use Microsoft.AspNetCore.Identity.PasswordHasher<T>)
 *   Rfc2898DeriveBytes       (PBKDF2; use AspNetCore Identity or DataProtection)
 *   Aes.Create / Aes.*       (use IDataProtector for reversible secrets)
 *   RSA.Create / RSA.*       (use DataProtection for at-rest keys)
 *   HMACSHA1/HMACSHA256      (use DataProtection)
 *
 * Use instead:
 *   Microsoft.AspNetCore.Identity.PasswordHasher<TUser>
 *   Microsoft.AspNetCore.DataProtection.IDataProtector
 *   Microsoft.Extensions.DependencyInjection (the standard container)
 *   UserClaimsPrincipalFactory<TUser>
 *
 * Strategy (RE2): match the banned types / methods on a non-test,
 * non-generated source line. We DO NOT try to infer misuse -- a single
 * match means the symbol is referenced, and the project rule says
 * reference it = suspect. Reviewer verdict.
 */
export default {
  id: 'plexor.no-handrolled-crypto',
  severity: 'warning',
  pattern:
    '\\b(?:BCrypt\\.|Rfc2898DeriveBytes|HMACSHA(?:1|256|384|512)|Aes\\.Create|AesManaged|RSACryptoServiceProvider|RSA\\.Create|System\\.Security\\.Cryptography\\.Aes)\\b',
  globs: ['**/*.cs'],
  excludePaths: [
    '**/bin/**',
    '**/obj/**',
    '**/Migrations/**',
    '**/*.Designer.cs',
    '**/*.g.cs',
    '**/*.AssemblyAttributes.cs',
    '**/Generated/**',
    '**/*Tests*/**',
    '**/tests/**',
  ],
  message:
    'Hand-rolled crypto primitive (BCrypt, AES, RSA, HMAC, PBKDF2) -- use the .NET / ASP.NET Core platform primitive: PasswordHasher<T>, IDataProtector, or DataProtectionTokenProvider<T>. See .agents/rules/coding/prefer-built-ins-over-hand-rolled.md.',
  source: 'coding/prefer-built-ins-over-hand-rolled.md',
  rationale:
    'Hand-rolled crypto is insecure by default: salt format differs, iteration counts drift, key-wrap algorithms change, padding mismatches. The .NET BCL ships PBKDF2 via Microsoft.AspNetCore.Identity.PasswordHasher<T>, reversible-secret crypto via Microsoft.AspNetCore.DataProtection.IDataProtector, and one-shot / TOTP tokens via DataProtectionTokenProvider<T>. All have audited defaults and adapters to match the rest of the platform. Plexor uses the primitives (PasswordHasher<User> in SigilInfrastructureInstaller; IDataProtector in Plexor.Shared.Security.Mtls via PlexorCertAuthorityInstaller; signing key wrapping via IDataProtector per Plexor.Modules.Sigil.Api.Setup) -- hand-rolling is by definition new and unwelcome.',
};
