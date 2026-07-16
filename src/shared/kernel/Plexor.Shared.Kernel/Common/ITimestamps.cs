namespace Plexor.Shared.Kernel.Common;

/// <summary>
///     Marker for entities that carry a <see cref="CreatedAt" /> timestamp.
///     Split out from <see cref="IUpdatedAt" /> because some entities
///     (audit events, refresh tokens, signing keys, role bindings) are
///     append-only — they have a creation time but no meaningful
///     modification concept.
/// </summary>
/// <remarks>
///     <para><b>Where the timestamp comes from.</b> The Application
///     layer (handlers, factories) sets <c>CreatedAt = clock.UtcNow</c>
///     on construction. The Infrastructure layer does not override it;
///     it relies on the value the Application layer wrote. This keeps
///     domain logic testable without a clock dependency.</para>
///     <para><b>Indexing.</b> EF Core indexes
///     <c>(tenant_id, created_at desc)</c> automatically when the
///     entity is configured with <c>.HasIndex(...).IsDescending(false)</c>.
///     The convention is <c>ix_{schema}_{table}_tenant_id_created_at</c>.</para>
/// </remarks>
public interface ICreatedAt
{
    /// <summary>Wall-clock time the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; }
}

/// <summary>
///     Marker for entities that track a last-modification timestamp.
///     Bumped by the Application layer on every field write (not on
///     every read); EF Core does not auto-bump — the handler is
///     responsible.
/// </summary>
/// <remarks>
///     <para><b>Pairing with <see cref="ICreatedAt" />.</b> Mutable
///     entities (User, Role, ApiKey, Tenant) implement both. Append-only
///     entities (AuditEntry, RefreshToken, SigningKey, RoleBinding)
///     implement <see cref="ICreatedAt" /> only.</para>
/// </remarks>
public interface IUpdatedAt
{
    /// <summary>Wall-clock time of the last write (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; }
}
