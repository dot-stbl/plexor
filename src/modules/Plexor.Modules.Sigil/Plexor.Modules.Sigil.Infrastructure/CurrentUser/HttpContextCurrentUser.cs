using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Plexor.Modules.Sigil.Application.Abstractions;

namespace Plexor.Modules.Sigil.Infrastructure.CurrentUser;

/// <summary>
///     Reads the authenticated principal from <see cref="HttpContext" />
///     and exposes it as <see cref="ICurrentUser" />. Scoped per-request
///     via <c>AddHttpContextAccessor()</c> + <c>AddScoped&lt;ICurrentUser,
///     HttpContextCurrentUser&gt;()</c>.
/// </summary>
/// <remarks>
///     <para><b>Anonymous handling.</b> When the request is
///     unauthenticated (no <c>HttpContext.User</c> principal, or
///     <c>Identity.IsAuthenticated == false</c>), the property
///     getters return the anonymous defaults (empty Guid ids and
///     empty collections) directly. Consumers don't need to
///     null-check.</para>
///     <para><b>Claim reading.</b> Each property reads directly from
///     <see cref="ClaimsPrincipal.FindFirst(string)" /> or
///     <see cref="ClaimsPrincipal.FindAll(string)" />. No caching —
///     reads are O(claim count) and run at most once per
///     property access (per request lifetime).</para>
///     <para><b>Why not <see cref="ClaimsPrincipal" /> directly?</b>
///     Application services shouldn't import
///     <c>System.Security.Claims</c> or
///     <c>Microsoft.AspNetCore.Http</c> for the caller identity —
///     a domain-shaped interface is mockable + testable without
///     an HTTP context.</para>
/// </remarks>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private static readonly IReadOnlyCollection<string> EmptyRoles = [];
    private static readonly IReadOnlyCollection<string> EmptyPermissions = [];

    private readonly IHttpContextAccessor accessor;

    /// <summary>Constructs the per-request reader from an HTTP context
    /// accessor.</summary>
    /// <param name="accessor">ASP.NET Core HTTP context accessor
    /// (registered as singleton via
    /// <c>services.AddHttpContextAccessor()</c>).</param>
    public HttpContextCurrentUser(IHttpContextAccessor accessor)
    {
        this.accessor = accessor;
    }

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    private bool IsAuthenticated =>
        Principal is { Identity.IsAuthenticated: true };

    /// <inheritdoc />
    public Guid UserId
    {
        get
        {
            if (!IsAuthenticated)
            {
                return Guid.Empty;
            }

            var raw = Principal!.FindFirst(IdentityClaims.UserId)?.Value;
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }

    /// <inheritdoc />
    public Guid TenantId
    {
        get
        {
            if (!IsAuthenticated)
            {
                return Guid.Empty;
            }

            var raw = Principal!.FindFirst(IdentityClaims.TenantId)?.Value;
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }

    /// <inheritdoc />
    public Guid? ProjectId
    {
        get
        {
            if (!IsAuthenticated)
            {
                return null;
            }

            var raw = Principal!.FindFirst(IdentityClaims.ProjectId)?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles
    {
        get
        {
            if (!IsAuthenticated)
            {
                return EmptyRoles;
            }

            return Principal!
                .FindAll(IdentityClaims.Roles)
                .Select(static claim => claim.Value)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Permissions
    {
        get
        {
            if (!IsAuthenticated)
            {
                return EmptyPermissions;
            }

            return Principal!
                .FindAll(IdentityClaims.Permission)
                .Select(static claim => claim.Value)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public bool IsService
    {
        get
        {
            if (!IsAuthenticated)
            {
                return false;
            }

            var raw = Principal!.FindFirst(IdentityClaims.IsService)?.Value;
            return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}