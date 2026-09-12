using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Quotas.Domain.Entities;

/// <summary>
///     A limit value bound to a polymorphic scope. The enforcer walks
///     folder → team → org → default and returns the minimum.
/// </summary>
/// <remarks>
///     <para><b>Polymorphic scope.</b> <see cref="ScopeKind" /> +
///     <see cref="ScopeId" /> is the (Kind, Id) tuple that maps to a
///     row in <c>realm.organizations</c>, <c>realm.teams</c>, or
///     <c>realm.folders</c>. There is no DB-level FK — see
///     <c>openspec/changes/phase-4-5-quotas/specs/quotas/spec.md</c>
///     Requirement "Migration order".</para>
///     <para><b>FK to <see cref="QuotaDefinition" />.</b>
///     <see cref="DefinitionId" /> is a real DB FK; the definition
///     row carries the catalog key, unit, period, and default value.
///     Renaming a definition's <c>Key</c> updates the FK target's
///     <c>Key</c> column in-place; assignments follow automatically.</para>
///     <para><b>UNIQUE constraint.</b> Exactly one assignment per
///     <c>(definition_id, scope_kind, scope_id, period)</c> — enforced
///     by a composite UNIQUE index.</para>
/// </remarks>
public sealed class QuotaAssignment : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>FK to the catalog entry this assignment binds.</summary>
    public Guid DefinitionId { get; init; }

    /// <summary>Org / Team / Folder the assignment targets.</summary>
    public QuotaScopeKind ScopeKind { get; init; }

    /// <summary>Id of the matching Realm entity.</summary>
    public Guid ScopeId { get; init; }

    /// <summary>Tenant the scope belongs to (denormalized for
    /// org-scoped authorization + audit filtering).</summary>
    public Guid OrgId { get; init; }

    /// <summary>The limit value for this scope. Must be positive; the
    /// PUT-validator in 4.5.g enforces <c>value &gt; 0</c>.</summary>
    public decimal Value { get; init; }

    /// <summary>Period override — if non-<see cref="QuotaPeriod.None" />,
    /// replaces the catalog's <c>Period</c> for this assignment. v1
    /// only emits <see cref="QuotaPeriod.None" /> and
    /// <see cref="QuotaPeriod.Hour" />.</summary>
    public QuotaPeriod Period { get; init; }

    /// <summary>Id of the user that created the assignment (audit trail).</summary>
    public Guid CreatedBy { get; init; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
