// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOidcUserProvisioner — IOidcUserProvisioner implementation.
// Find-or-create the Plexor User row + default `viewer`-role
// RoleBinding for a freshly-onboarded OIDC user. Phase 4.6.3c.
//
// Atomicity: the User + RoleBinding inserts run inside a single EF
// transaction so a half-provisioned state (User row but no role
// binding) is impossible. A failed insert rolls back; the caller
// surfaces 500 via OidcUserProvisioningFailed.
//
// Display-name fallback chain:
//   1. preferred_username (OIDC `preferred_username` claim)
//   2. name             (OIDC `name` claim)
//   3. email local-part (always present when email is — required)
// `email` itself is required and validated upstream by the
// validator (claim extraction) — the provisioner treats a null
// email as a hard 400 oidc.user.missing_claims error.
//
// Phase 5+ may add a per-tenant role-mapping table; today the
// OIDC user is auto-bound to the built-in `viewer` role only. An
// admin can promote them later via the existing role-binding
// APIs.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Provisioners;

/// <summary>
///     <see cref="IOidcUserProvisioner" /> implementation. Reads
///     and writes through <see cref="IdentityDbContext" />; the
///     User + RoleBinding inserts run inside a single transaction.
/// </summary>
/// <param name="db">Identity module DbContext — owns the User,
/// Role, and RoleBinding tables.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the
/// <c>created_at</c> / <c>updated_at</c> / <c>password_changed_at</c>
/// stamps (per <c>time-and-wire-format.md</c> §3).</param>
public sealed class EfOidcUserProvisioner(IdentityDbContext db, TimeProvider clock) : IOidcUserProvisioner
{
    /// <summary>Default non-admin role name granted to a freshly
    /// provisioned OIDC user. The Migrator seeds this role per
    /// tenant at first boot — see the built-in roles documented on
    /// <see cref="Domain.Entities.Role" />.</summary>
    private const string DefaultRoleName = "viewer";

    /// <inheritdoc />
    public async Task<User> ProvisionAsync(
        OidcTenantConfig config,
        string subject,
        string email,
        string? preferredUsername,
        string? name,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new IdentityException(
                IdentityExceptions.OidcUserMissingClaims,
                "OIDC id_token has no `email` claim; cannot provision a Plexor user without one.");
        }

        var deterministicId = ExternalOidcAuthProviderHelpers.DeterministicUserId(
            config.Authority, subject);

        var existing = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == deterministicId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var resolvedDisplayName = ResolveDisplayName(preferredUsername, name, email);

        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        User persisted;
        try
        {
            var now = clock.GetUtcNow();
            var newUser = new User
            {
                Id = deterministicId,
                OrgId = config.OrgId,
                Email = new Email(email),
                DisplayName = resolvedDisplayName,
                Status = "active",
                PasswordHash = null,
                FailedLoginCount = 0,
                LockedUntil = null,
                LastLoginAt = null,
                CreatedAt = now,
                UpdatedAt = now,
                PasswordChangedAt = now,
            };

            await db.Users.AddAsync(newUser, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            // Find the tenant's default role. The Migrator seeds
            // `viewer` per tenant at first boot, but we look it up
            // by name rather than hardcoding the id — Phase 5+
            // adds per-tenant custom defaults and the lookup
            // continues to work without code changes.
            var defaultRole = await db.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    role => role.OrgId == config.OrgId && role.Name == DefaultRoleName,
                    cancellationToken);

            if (defaultRole is not null)
            {
                var binding = new RoleBinding
                {
                    Id = Guid.NewGuid(),
                    OrgId = config.OrgId,
                    UserId = newUser.Id,
                    RoleId = defaultRole.Id,
                    TeamId = null,
                    FolderId = null,
                    CreatedAt = now,
                };

                await db.RoleBindings.AddAsync(binding, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            persisted = newUser;
        }
        catch (Exception exception) when (exception is not IdentityException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new IdentityException(
                IdentityExceptions.OidcUserProvisioningFailed,
                $"OIDC user provisioning failed: {exception.GetType().Name}.",
                exception);
        }

        return persisted;
    }

    /// <summary>
    ///     Apply the display-name fallback chain:
    ///     <c>preferred_username</c> → <c>name</c> → email local-part.
    ///     Empty / whitespace strings are skipped; the email
    ///     local-part is always non-empty when <paramref name="email" />
    ///     is well-formed.
    /// </summary>
    /// <param name="preferredUsername">The OIDC <c>preferred_username</c> value.</param>
    /// <param name="name">The OIDC <c>name</c> value.</param>
    /// <param name="email">The OIDC <c>email</c> value (required —
    ///     the local-part is the final fallback).</param>
    private static string ResolveDisplayName(string? preferredUsername, string? name, string email)
    {
        if (!string.IsNullOrWhiteSpace(preferredUsername))
        {
            return preferredUsername;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : email;
    }
}
