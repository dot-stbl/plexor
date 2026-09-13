// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaAssignmentSummary — projection of QuotaAssignment returned by
// GET /api/v1/quotas/assignments. Carries the catalog key resolved at
// read time so the dashboard can render rows without a join.
// ============================================================================

namespace Plexor.Modules.Quotas.Api.Models;

/// <summary>
///     One assignment row at a single scope. Returned by
///     <c>GET /api/v1/quotas/assignments</c>.
/// </summary>
/// <remarks>
///     <para><b>Why denormalise the key.</b> The store only carries
///     <see cref="DefinitionId" />; the catalog row holds the stable
///     key string (<c>"compute.vms.count"</c>). The 4.5.g.2 controller
///     joins at read time so the dashboard can render rows by key
///     without a second round-trip. The cost is one extra catalog
///     SELECT per list call — acceptable for v1; revisit if the catalog
///     grows past ~50 keys.</para>
/// </remarks>
public sealed class QuotaAssignmentSummary
{
    /// <summary>Assignment row id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Catalog key resolved from the assignment's
    /// <see cref="DefinitionId" /> via <c>IQuotaCatalog</c>.</summary>
    public string DefinitionKey { get; init; } = string.Empty;

    /// <summary>Catalog row the assignment binds.</summary>
    public Guid DefinitionId { get; init; }

    /// <summary>Org / Team / Folder scope the assignment targets,
    /// serialised as the enum member name.</summary>
    public string ScopeKind { get; init; } = string.Empty;

    /// <summary>Id of the matching Realm entity.</summary>
    public Guid ScopeId { get; init; }

    /// <summary>Tenant the scope belongs to (denormalized for
    /// org-scoped authorization + audit filtering).</summary>
    public Guid OrgId { get; init; }

    /// <summary>The limit value for this scope.</summary>
    public decimal Value { get; init; }

    /// <summary>Period override — <c>"None"</c>, <c>"Hour"</c>, ...,
    /// serialised as the enum member name.</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Id of the user that created the assignment
    /// (<see cref="Guid.Empty" /> for system-seeded rows from the
    /// 4.5.f OrgSeeder).</summary>
    public Guid CreatedBy { get; init; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
