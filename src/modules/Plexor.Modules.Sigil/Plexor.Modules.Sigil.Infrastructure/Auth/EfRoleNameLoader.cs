// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfRoleNameLoader — EF Core implementation of IRoleNameLoader. Joins
// sigil.role_bindings to sigil.roles and projects the distinct role
// names. One DB roundtrip per call, AsNoTracking.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     EF Core implementation of <see cref="IRoleNameLoader" />. Joins
///     <c>sigil.role_bindings</c> to <c>sigil.roles</c> by
///     <c>role_id</c> and returns the distinct role names assigned to
///     the user.
/// </summary>
/// <param name="db"></param>
/// <remarks>
///     <para><b>Why a JOIN instead of two queries.</b> Two queries
///     (bindings → ids → roles by ids) round-trip twice and risk
///     drift under concurrent writes (binding row added/removed
///     between queries). A single JOIN reads consistently.</para>
///     <para><b>AsNoTracking.</b> The result is consumed as a value
///     projection (role names baked into a JWT) — no entity update
///     path ever touches it. Tracking would waste change-tracker
///     memory.</para>
///     <para><b>Distinct.</b> A user can hold multiple bindings for
///     the same role at different scope tiers (org-wide, team,
///     folder); the JWT must contain one claim per role name, so the
///     projection deduplicates.</para>
/// </remarks>
public sealed class EfRoleNameLoader(IdentityDbContext db) : IRoleNameLoader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> LoadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.RoleBindings
            .AsNoTracking()
            .Where(binding => binding.UserId == userId)
            .Join(
                db.Roles.AsNoTracking(),
                binding => binding.RoleId,
                role => role.Id,
                (_, role) => role.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}
