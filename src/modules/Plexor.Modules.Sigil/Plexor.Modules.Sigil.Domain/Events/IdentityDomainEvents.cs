using Plexor.Shared.Kernel.Events;

namespace Plexor.Modules.Sigil.Domain.Events;

/// <summary>
///     Raised when a new user is provisioned by an admin. The audit log
///     records "actor X created user Y in tenant Z" without re-querying.
/// </summary>
/// <param name="EventId">Unique event id (UUID v7).</param>
/// <param name="OccurredAt">When the user was created (UTC).</param>
/// <param name="TenantId">Tenant the new user belongs to.</param>
/// <param name="ActorId">Admin who provisioned the user.</param>
/// <param name="UserId">New user's id.</param>
public sealed record UserCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid ActorId,
    Guid UserId) : IDomainEvent;

/// <summary>
///     Raised when a user successfully authenticates via /auth/login.
///     The audit log records this with <c>outcome = succeeded</c>.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Login time (UTC).</param>
/// <param name="TenantId">Tenant the user logged into.</param>
/// <param name="UserId">User id.</param>
/// <param name="SourceIp">Client IP (best-effort; behind reverse proxies
/// this is the proxy IP, not the client — log warnings).</param>
public sealed record LoginSucceeded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    string? SourceIp) : IDomainEvent;

/// <summary>
///     Raised when a /auth/login attempt fails. The audit log records
///     this with <c>outcome = failed</c> + the reason in metadata.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Failure time (UTC).</param>
/// <param name="TenantId">Tenant the attempt was scoped to (resolved
/// from <c>tenant_slug</c> in the request).</param>
/// <param name="UserId">User id when the email matched an existing row,
/// or <c>null</c> for "no such user" failures.</param>
/// <param name="EmailSubmitted">The email the caller submitted (may be
/// malformed or non-existent).</param>
/// <param name="SourceIp">Client IP, same caveat as LoginSucceeded.</param>
public sealed record LoginFailed(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid? UserId,
    string EmailSubmitted,
    string? SourceIp) : IDomainEvent;

/// <summary>
///     Raised when a user account is locked out due to repeated failed
///     logins. Audit log records <c>outcome = failed</c>; lockout
///     duration is in the metadata.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Lockout time (UTC).</param>
/// <param name="TenantId">Tenant the user belongs to.</param>
/// <param name="UserId">Locked user.</param>
/// <param name="LockedUntil">When the lockout expires (UTC).</param>
public sealed record AccountLocked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    DateTimeOffset LockedUntil) : IDomainEvent;

/// <summary>
///     Raised when a user changes their password (self-service or
///     admin-reset). Audit log records the action + actor (self or admin).
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Change time (UTC).</param>
/// <param name="TenantId">Tenant the user belongs to.</param>
/// <param name="ActorId">Who changed the password — usually the user
/// themselves, or an admin for reset.</param>
/// <param name="UserId">Whose password changed.</param>
public sealed record PasswordChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid ActorId,
    Guid UserId) : IDomainEvent;

/// <summary>
///     Raised when a refresh token rotates (old token revoked, new one
///     issued). Audit log records this with the family id in metadata
///     so investigators can correlate the chain.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Rotation time (UTC).</param>
/// <param name="TenantId">Tenant the user belongs to.</param>
/// <param name="UserId">User whose token rotated.</param>
/// <param name="FamilyId">Refresh-token family id.</param>
public sealed record RefreshTokenRotated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    Guid FamilyId) : IDomainEvent;

/// <summary>
///     Raised when a refresh-token replay is detected — caller presented
///     a revoked token from a family that still has another active token.
///     Audit log records <c>outcome = failed</c>; the family is
///     immediately revoked across the user session.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Detection time (UTC).</param>
/// <param name="TenantId">Tenant the user belongs to.</param>
/// <param name="UserId">User whose token was replayed.</param>
/// <param name="FamilyId">Family id that was fully revoked.</param>
public sealed record RefreshTokenReplayed(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    Guid FamilyId) : IDomainEvent;

/// <summary>
///     Raised when a user logs out. Audit log records the action; the
///     access JWT continues to work until expiry (15 min) — there's
///     no global blacklist in v0.1.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Logout time (UTC).</param>
/// <param name="TenantId">Tenant the user belongs to.</param>
/// <param name="UserId">User id.</param>
public sealed record UserLoggedOut(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId) : IDomainEvent;

/// <summary>
///     Raised when an API key is issued. Audit log records the
///     permissions granted + owner. The raw secret is never recorded —
///     only the key id is exposed.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Issue time (UTC).</param>
/// <param name="TenantId">Tenant the key belongs to.</param>
/// <param name="ActorId">User who issued the key.</param>
/// <param name="ApiKeyId">New key id (UUID; becomes <c>kid_xxx</c>).</param>
public sealed record ApiKeyIssued(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid ActorId,
    Guid ApiKeyId) : IDomainEvent;

/// <summary>
///     Raised when an API key is revoked. Audit log records the actor.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Revocation time (UTC).</param>
/// <param name="TenantId">Tenant the key belongs to.</param>
/// <param name="ActorId">User who revoked the key.</param>
/// <param name="ApiKeyId">Revoked key id.</param>
public sealed record ApiKeyRevoked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid ActorId,
    Guid ApiKeyId) : IDomainEvent;

/// <summary>
///     Raised when an SSH key is registered. Audit log records the
///     fingerprint (not the public key itself — key material is not
///     logged).
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Registration time (UTC).</param>
/// <param name="TenantId">Tenant the user belongs to.</param>
/// <param name="ActorId">User who registered the key.</param>
/// <param name="SshKeyId">New key id.</param>
public sealed record SshKeyAdded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid ActorId,
    Guid SshKeyId) : IDomainEvent;

/// <summary>
///     Raised when an SSH key is revoked. The key remains in the DB
///     with <c>revoked_at</c> set for audit trail.
/// </summary>
/// <param name="EventId">Unique event id.</param>
/// <param name="OccurredAt">Revocation time (UTC).</param>
/// <param name="TenantId">Tenant the key belongs to.</param>
/// <param name="ActorId">User who revoked the key.</param>
/// <param name="SshKeyId">Revoked key id.</param>
public sealed record SshKeyRevoked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid ActorId,
    Guid SshKeyId) : IDomainEvent;