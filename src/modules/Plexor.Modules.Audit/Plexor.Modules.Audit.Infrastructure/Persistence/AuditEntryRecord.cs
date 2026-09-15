using Plexor.Modules.Audit.Application.Abstractions;

namespace Plexor.Modules.Audit.Infrastructure.Persistence;

/// <summary>
///     EF Core row representation of an <see cref="AuditEntry" />.
///     Lives in the Infrastructure layer so the Application layer's
///     port type (<see cref="AuditEntry" />, a record) stays free of
///     EF-specific attributes. The mapper in
///     <c>Persistence/Mappers/AuditEntryMapper.cs</c> translates
///     between the two.
/// </summary>
/// <remarks>
///     <para><b>Append-only EF entity.</b> All properties are
///     <c>init</c>-only — there is no EF tracking of a "modified"
///     state because the store contract forbids UPDATE. EF Core's
///     change tracker still functions for INSERT (we use
///     <c>Add</c> / <c>AddRange</c>) but cannot mutate an existing
///     row through the entity.</para>
///     <para><b>Why not <see cref="AuditEntry" /> directly?</b>
///     <see cref="AuditEntry" /> is an immutable record in the
///     Application layer; using it as an EF entity would couple the
///     port type to EF Core (a framework concern the Application
///     layer is supposed to be agnostic to). The two-type split
///     keeps the boundary clean.</para>
///     <para><b>Metadata storage.</b> <see cref="MetadataJson" /> is
///     a <see cref="string" /> mapped to <c>jsonb</c>; the mapper
///     serialises / deserialises via
///     <see cref="System.Text.Json.JsonSerializer" /> with the
///     framework-frozen <c>Web</c> options. EF stores the raw
///     string in a JSONB column; PostgreSQL validates it on
///     write.</para>
///     <para><b>Append-only invariant.</b> The migration emits
///     <c>REVOKE UPDATE, DELETE ON atlas.audit_entries FROM
///     PUBLIC</c> so the database refuses to honour UPDATE/DELETE
///     even if an <c>ExecuteUpdate</c> slipped past code review.
///     This entity does not need any other "immutability"
///     affordance — EF Core 10's own contract is enough.</para>
/// </remarks>
public sealed class AuditEntryRecord
{
    /// <summary>UUID v7 (sortable by creation time).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant this row belongs to.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Actor type as a string (e.g. <c>"User"</c>, <c>"Node"</c>).</summary>
    public AuditActor Actor { get; init; }

    /// <summary>Stable id of the actor within the tenant.</summary>
    public Guid ActorId { get; init; }

    /// <summary>Stable action string (e.g. <c>"cluster.create"</c>).</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Resource type label (<c>"cluster"</c>, <c>"user"</c>, ...) or <c>null</c>.</summary>
    public string? ResourceType { get; init; }

    /// <summary>Resource id (paired with <see cref="ResourceType" />) or <c>null</c>.</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>Outcome of the action (Succeeded / Failed / Denied).</summary>
    public AuditOutcome Outcome { get; init; }

    /// <summary>Stable error code on Failed / Denied outcomes; <c>null</c> on Succeeded.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    ///     Free-form structured context as a JSON object string
    ///     (e.g. <c>{"ip":"...","user_agent":"..."}</c>). Empty
    ///     object (<c>"{}"</c>) when no metadata.
    /// </summary>
    public string MetadataJson { get; init; } = "{}";

    /// <summary>Wall-clock instant (UTC) the action was attempted.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}
