using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Shared.Filtering.Registry;

using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Sigil.Domain.Entities;

/// <summary>
///     One human user in one tenant. Identity (1:1 to tenant in v0.1) +
///     credentials + display metadata + login-lockout state.
/// </summary>
/// <remarks>
///     <para><b>Append-mostly.</b> Mutations allowed: <see cref="DisplayName" />
///     change, password change (write-only column), login counters, status
///     transitions (active ↔ suspended). Email changes require the user
///     to be re-verified (Phase 2); v0.1 forbids email mutation.</para>
///     <para><b>Filterable.</b> Properties are exposed to the filter DSL
///     via <see cref="IFilterableEntity" />. The kubb plugin
///     (<c>kubb-plugin-filter</c>) reads the OpenAPI schema extension
///     to generate typed filter builders.</para>
///     <para><b>No Repository.</b> Persistence happens via the Identity
///     module's DbContext (Phase 2). the Identity DbContext
///     rule §"Application service — DbContext directly" applies: no
///     <c>IUserRepository</c> wrapper.</para>
/// </remarks>
public sealed class User : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>Unique identifier (UUID v7, sortable by creation time).</summary>
    public Guid Id { get; init; }

    /// <summary>Organization this user belongs to. 1:1 in v0.1; cross-org
    /// users are Phase 2.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Email address (validated, lowercased). Unique per tenant.</summary>
    public Email Email { get; init; } = null!;

    /// <summary>Display name shown in UI + audit entries.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>User status. <c>"active"</c> by default after admin
    /// provisioning; transitions to <c>"suspended"</c> on admin action.</summary>
    public string Status { get; init; } = "active";

    /// <summary>Bcrypt password hash, or <c>null</c> for OAuth-only users
    /// (Phase 2; the column is nullable in <c>sigil.users</c>).</summary>
    public PasswordHash? PasswordHash { get; init; }

    /// <summary>Number of consecutive failed logins since last success.
    /// Reset on successful login; triggers lockout at threshold.</summary>
    public int FailedLoginCount { get; init; }

    /// <summary>Time the account is locked until (UTC), or <c>null</c> if
    /// not locked. Login attempts before this time return 423.</summary>
    public DateTimeOffset? LockedUntil { get; init; }

    /// <summary>Last successful login time (UTC), or <c>null</c> if never.</summary>
    public DateTimeOffset? LastLoginAt { get; init; }

    /// <summary>Account creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC) — bumped on any field write.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}