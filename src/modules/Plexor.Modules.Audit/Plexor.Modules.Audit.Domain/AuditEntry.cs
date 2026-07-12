using Plexor.Shared.Filtering;

namespace Plexor.Modules.Audit.Domain;

/// <summary>
///     One row in the audit log: who did what, against which resource, when.
///     Append-only — updates and deletes are forbidden.
/// </summary>
/// <remarks>
///     <para><b>Schema.</b> Persisted in the <c>atlas</c> PostgreSQL schema
///     (see <c>Plexor.Shared.Persistence.DatabaseInformation.Schemes.Audit</c>).
///     Read paths filter via the standard <see cref="FilterQuery" /> DSL;
///     properties marked public-and-not-<c>[NotMapped]</c> are picked up
///     by the <c>FilterableFieldRegistry</c> and exposed to the frontend
///     as <c>x-filterable</c> via the OpenAPI transformer.</para>
///     <para><b>Append-only.</b> Domain layer enforces this — there is no
///     setter for any property and no <c>Remove</c> handler. EF Core
///     tracks the entity but the application layer never calls
///     <c>SetState(Modified)</c> or <c>Remove</c>.</para>
/// </remarks>
public sealed class AuditEntry : IFilterableEntity
{
    /// <summary>Unique identifier (UUID v7, sortable by creation time).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant the action was scoped to. Every audit row is
    /// tenant-scoped for multi-tenancy isolation.</summary>
    public Guid TenantId { get; init; }

    /// <summary>User (actor) who performed the action.</summary>
    public Guid ActorId { get; init; }

    /// <summary>Action verb, lowercase kebab. <c>"vm.create"</c>,
    /// <c>"vm.delete"</c>, <c>"cluster.provision"</c>, ...</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Resource type the action targeted. <c>"vm"</c>,
    /// <c>"node"</c>, <c>"cluster"</c>, ...</summary>
    public string ResourceType { get; init; } = string.Empty;

    /// <summary>Resource id (null when the action is not resource-scoped).</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>Free-form correlation id propagated from the caller's
    /// request headers, used to trace one user action across services.</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>Outcome label: <c>"succeeded"</c> or <c>"failed"</c>.
    /// Filterable enum-style.</summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>Wall-clock time of the action (UTC, recorded by the
    /// server, not by the actor).</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Optional structured payload — request context, error
    /// stack, before/after snapshots. Persisted as JSONB.</summary>
    public string? MetadataJson { get; init; }
}
