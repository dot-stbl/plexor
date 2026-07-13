// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MeQuery + MeResult — return the authenticated caller's identity.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Marker command for the "who am I?" query. Always returns
///     <see cref="MeResult" /> populated from the current
///     <see cref="Plexor.Modules.Sigil.Application.Abstractions.ICurrentUser" />;
///     never touches the DB on the hot path.
/// </summary>
public sealed record MeQuery;

/// <summary>
///     Snapshot of the authenticated caller's identity, roles, and
///     effective permissions as resolved by the bearer handler.
/// </summary>
/// <param name="UserId">Caller identity (<c>Guid.Empty</c> only when
///     the request is anonymous — the handler treats that as an error).</param>
/// <param name="OrgId">Tenant scope (<c>Guid.Empty</c> when anonymous).</param>
/// <param name="Roles">Role names bound to the caller at sign time.</param>
/// <param name="Permissions">Effective permissions after role resolution.</param>
public sealed record MeResult(
    Guid UserId,
    Guid OrgId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
