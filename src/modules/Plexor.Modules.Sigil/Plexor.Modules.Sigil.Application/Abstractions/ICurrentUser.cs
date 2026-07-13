namespace Plexor.Modules.Sigil.Application.Abstractions;

/// <summary>
///     Per-request information about the authenticated principal — user
///     or service account. Application services inject this interface to
///     read the caller identity without depending on
///     <c>HttpContext</c> directly.
/// </summary>
/// <remarks>
///     <para><b>Same shape, both bearer schemes.</b> Whether the
///     caller authenticated with a JWT (user) or an API key (service
///     account), the property shape is the same. <see cref="IsService" />
///     distinguishes the two — useful for audit + authorization rules
///     (e.g. "API keys cannot mutate their own user").</para>
///     <para><b>Anonymous default.</b> When the request is unauthenticated
///     (e.g. <c>/auth/login</c>), <see cref="UserId" /> and
///     <see cref="TenantId" /> are <c>Guid.Empty</c> and
///     <see cref="Roles" />/<see cref="Permissions" /> are empty
///     collections. Consumers check <c>UserId == Guid.Empty</c> to detect
///     the anonymous case.</para>
///     <para><b>Why a typed interface.</b> Application services shouldn't
///     import <c>Microsoft.AspNetCore.Http</c> or
///     <c>System.Security.Claims</c> just to read the caller identity.
///     A domain-shaped interface is testable (mock with NSubstitute)
///     and decouples the service from the auth pipeline.</para>
/// </remarks>
public interface ICurrentUser
{
    /// <summary>
    ///     Caller's user id (for JWT auth) or service-account owner id
    ///     (for API key auth). <c>Guid.Empty</c> when the request is
    ///     unauthenticated.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    ///     Tenant the caller operates in. Every authenticated request
    ///     carries exactly one tenant — cross-tenant users are Phase 2+.
    ///     <c>Guid.Empty</c> when unauthenticated.
    /// </summary>
    public Guid TenantId { get; }

    /// <summary>
    ///     Optional project scope. <c>null</c> = tenant-wide operation.
    ///     Set when the request is scoped to a specific project
    ///     (Phase 2+ when Project entity lands).
    /// </summary>
    public Guid? ProjectId { get; }

    /// <summary>
    ///     Role names bound to the caller (denormalized at sign time).
    ///     Empty collection for anonymous or service-account callers
    ///     (service accounts have no roles, only permissions).
    /// </summary>
    public IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    ///     Effective permissions for the caller — union of all bound
    ///     roles' permissions + (for API keys) the key's own
    ///     permissions subset. Authorization checks compare against this
    ///     collection.
    /// </summary>
    public IReadOnlyCollection<string> Permissions { get; }

    /// <summary>
    ///     <c>true</c> when the caller authenticated via API key
    ///     (service-to-service), <c>false</c> when authenticated via
    ///     JWT (human user). Used to gate service-only operations.
    /// </summary>
    public bool IsService { get; }
}
