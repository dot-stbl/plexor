namespace Plexor.Modules.Audit.Application.Abstractions;

/// <summary>
///     One immutable audit entry. Append-only — once written, the row
///     is never updated or deleted by application code (the database
///     REVOKEs UPDATE/DELETE on <c>atlas.audit_entries</c> to enforce
///     this at the storage layer).
/// </summary>
/// <remarks>
///     <para><b>Identity.</b> <see cref="Id" /> is a UUID v7 minted by
///     the Application layer (sortable by creation time). The
///     Application layer is the only place where a new audit id is
///     allocated; Infrastructure just persists it.</para>
///     <para><b>Tenant scope.</b> Every entry carries an
///     <see cref="OrgId" />. Cross-tenant audit queries are
///     impossible by construction — <see cref="IAuditStore.QueryAsync" />
///     and <see cref="IAuditStore.QueryByActorAsync" /> filter on
///     <see cref="OrgId" /> when supplied.</para>
///     <para><b>Free-form metadata.</b> <see cref="Metadata" /> carries
///     action-specific structured data (e.g. <c>{ "ip": "...", "user_agent": "..." }</c>).
///     Serialised as a JSON object in the <c>metadata</c> column
///     (<c>jsonb</c>). <c>null</c> when the action has no
///     structured context.</para>
///     <para><b>Resource reference.</b> <see cref="ResourceType" /> +
///     <see cref="ResourceId" /> identify the target of the action
///     (e.g. <c>("cluster", clusterId)</c>, <c>("user", userId)</c>).
///     Both nullable — some actions don't target a specific resource
///     (system-wide maintenance, sign-in events).</para>
/// </remarks>
/// <param name="Id">UUID v7 minted by the Application layer.</param>
/// <param name="OrgId">Tenant this entry belongs to.</param>
/// <param name="Actor">Who initiated the action (user / service / node / system).</param>
/// <param name="ActorId">
///     Stable id of the actor within <see cref="OrgId" />. For
///     <see cref="AuditActor.User" /> = <c>sigil.users.id</c>; for
///     <see cref="AuditActor.Node" /> = <c>forge.nodes.id</c>;
///     <see cref="Guid.Empty" /> for <see cref="AuditActor.System" />.
/// </param>
/// <param name="Action">
///     Stable action identifier in dotted form
///     (e.g. <c>"cluster.create"</c>, <c>"user.sign_in"</c>,
///     <c>"vms.start"</c>).
/// </param>
/// <param name="ResourceType">
///     Type label of the target resource (e.g. <c>"cluster"</c>,
///     <c>"user"</c>, <c>"vm"</c>). <c>null</c> when no resource
///     was targeted.
/// </param>
/// <param name="ResourceId">Id of the target resource, paired with <see cref="ResourceType" />.</param>
/// <param name="Outcome">Succeeded / Failed / Denied.</param>
/// <param name="ErrorCode">
///     Stable error code on <see cref="AuditOutcome.Failed" /> /
///     <see cref="AuditOutcome.Denied" /> outcomes (e.g.
///     <c>"cluster.not_found"</c>, <c>"vms.timeout"</c>). <c>null</c>
///     on <see cref="AuditOutcome.Succeeded" />.
/// </param>
/// <param name="Metadata">
///     Free-form structured context. Stored as <c>jsonb</c>. <c>null</c>
///     when the action has no structured context.
/// </param>
/// <param name="OccurredAt">Wall-clock instant (UTC) the action was attempted.</param>
public sealed record AuditEntry(
    Guid Id,
    Guid OrgId,
    AuditActor Actor,
    Guid ActorId,
    string Action,
    string? ResourceType,
    Guid? ResourceId,
    AuditOutcome Outcome,
    string? ErrorCode,
    IReadOnlyDictionary<string, object?>? Metadata,
    DateTimeOffset OccurredAt);
