// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentityBootstrapper — seeds the initial admin org + user when the
// sigil.users table is empty. Reads the bootstrap password from
// PLEXOR_INITIAL_ADMIN_PASSWORD (env var). Idempotent: re-runs are
// no-ops if any active user exists.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Migrator;

/// <summary>
///     First-run admin seeder. <c>PLEXOR_INITIAL_ADMIN_PASSWORD</c>
/// must be set in the environment before the migrator starts — the
/// seeder fails fast (and aborts the host) when the variable is
/// missing OR the table already has an admin. After the first run
/// it stays a no-op so subsequent starts don't accidentally rotate
/// the seeded credentials.
/// </summary>
/// <remarks>
///     <para><b>What gets seeded.</b>
///     <list type="bullet">
///       <item>Two built-in roles: <c>admin</c> (all permissions —
///       represented by a single sentinel string <c>*</c>) and
///       <c>viewer</c> (no permissions yet).</item>
///       <item>One user <c>admin@plexor.local</c> bound to the
///       <c>admin</c> role, with <c>MustChangePassword = true</c> so
///       the operator's first login forces a rotation.</item>
///     </list></para>
///     <para><b>Org placeholder.</b> Phase 4's
///     <c>CreateUserRequest</c> requires an <c>OrgId</c> — the seed
///     generates a random orgId (v0.1 doesn't run Sigil.Realm
///     migrations). Future Realm support will read the
///     <c>"default"</c> org from the Realm schema instead.</para>
///     <para><b>Why only on empty.</b> Once an operator has rotated
///     the bootstrap password, the seeder stops touching the
///     identity tables so a redeploy / migrator rerun never
///     accidentally resets credentials.</para>
/// </remarks>
internal sealed class IdentityBootstrapper(
    IdentityDbContext db,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<IdentityBootstrapper> logger) : IHostedService
{
    /// <summary>
    ///     Email address baked into the bootstrap admin user. The
    ///     operator can't pick their own email at first run — that
    ///     flow comes with the self-hosted installer wizard.
    /// </summary>
    private const string InitialAdminEmail = "admin@plexor.local";

    /// <summary>Display name baked into the bootstrap user.</summary>
    private const string InitialAdminDisplayName = "Bootstrap Admin";

    /// <summary>Built-in admin role name.</summary>
    private const string AdminRoleName = "admin";

    /// <summary>Built-in read-only role name.</summary>
    private const string ViewerRoleName = "viewer";

    /// <summary>
    ///     Env var the seeder reads. Picked as the unmistakable-name
    ///     over <c>PLEXOR_PASSWORD</c> / <c>PLEXOR_ADMIN_PASSWORD</c>
    ///     so a missed variable doesn't silently rotate an
    ///     unrelated secret.
    /// </summary>
    private const string PasswordEnvironmentVariable = "PLEXOR_INITIAL_ADMIN_PASSWORD";

    /// <summary>
    ///     Sentinel permission string meaning "all". The
    ///     <see cref="Plexor.Shared.Authorization.PermissionAuthorizationHandler" />
    ///     short-circuits a token carrying <c>*</c>.
    /// </summary>
    private const string WildcardPermission = "*";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var hadUsers = await db.Users
                .AsNoTracking()
                .AnyAsync(cancellationToken);

            if (hadUsers)
            {
                logger.LogDebug(
                    "IdentityBootstrapper: at least one user already exists; skipping seed.");
                return;
            }

            var password = configuration[PasswordEnvironmentVariable];
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogCritical(
                    "IdentityBootstrapper: {Env} is not set. Refusing to start without a bootstrap password.",
                    PasswordEnvironmentVariable);
                lifetime.StopApplication();
                return;
            }

            var adminId = Guid.NewGuid();
            var orgId = Guid.NewGuid();
            var adminRoleId = Guid.NewGuid();
            var viewerRoleId = Guid.NewGuid();
            var bindingId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            // Built-in roles. Permissions stored as PermissionScope
            // (the value-object form, not raw strings) so the
            // IReadOnlyList<PermissionScope> column converter in
            // RoleConfiguration accepts them.
            var adminPermissions = new[] { new PermissionScope(WildcardPermission) };
            var viewerPermissions = Array.Empty<PermissionScope>();

            var adminRole = new Role
            {
                Id = adminRoleId,
                OrgId = orgId,
                Name = AdminRoleName,
                Description = "Built-in admin (wildcard).",
                Permissions = adminPermissions,
                BuiltIn = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var viewerRole = new Role
            {
                Id = viewerRoleId,
                OrgId = orgId,
                Name = ViewerRoleName,
                Description = "Built-in viewer (no permissions yet).",
                Permissions = viewerPermissions,
                BuiltIn = true,
                CreatedAt = now,
                UpdatedAt = now,
            };

            // Org placeholder row. The schema's there for future
            // Sigil.Realm binding; in v0.1 we don't run its
            // migrations. The user/org_id UUIDs are sufficient for
            // the FK in sigil.users + role_bindings.
            var adminUser = new User
            {
                Id = adminId,
                OrgId = orgId,
                Email = new Email(InitialAdminEmail),
                DisplayName = InitialAdminDisplayName,
                Status = "active",
                PasswordHash = new PasswordHash(
                    passwordHasher.HashPassword(
                        new User { Id = adminId }, password)),
                FailedLoginCount = 0,
                LockedUntil = null,
                LastLoginAt = null,
                MustChangePassword = true,
                CreatedAt = now,
                UpdatedAt = now,
            };

            var adminBinding = new RoleBinding
            {
                Id = bindingId,
                OrgId = orgId,
                UserId = adminId,
                RoleId = adminRoleId,
                TeamId = null,
                FolderId = null,
                CreatedAt = now,
            };

            await db.Roles.AddAsync(adminRole, cancellationToken);
            await db.Roles.AddAsync(viewerRole, cancellationToken);
            await db.Users.AddAsync(adminUser, cancellationToken);
            await db.RoleBindings.AddAsync(adminBinding, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "IdentityBootstrapper: seeded initial admin user {Email} for org {OrgId}. " +
                "MustChangePassword = true — operator must rotate on first login.",
                InitialAdminEmail,
                orgId);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "IdentityBootstrapper: unexpected failure during first-run seed.");
            lifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
