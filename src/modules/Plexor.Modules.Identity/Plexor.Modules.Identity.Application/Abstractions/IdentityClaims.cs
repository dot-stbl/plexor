namespace Plexor.Modules.Identity.Application.Abstractions;

/// <summary>
///     Claim type names used by the JWT signer (Phase 3.4) and the
///     bearer authentication handler (Phase 3.6). The signer writes
///     claims using these names; the bearer handler reads them back
///     to populate <see cref="ICurrentUser" />. Both sides reference
///     the same constants so a typo on one side fails the build, not
///     production auth.
/// </summary>
/// <remarks>
///     <para><b>Standard claims (no prefix).</b> <c>sub</c> and
///     <c>aud</c> are JWT RFC 7519 registered names. <c>tid</c> is
///     de-facto standard for "tenant id" in multi-tenant systems.
///     <c>role</c> is the <c>System.Security.Claims.ClaimTypes.Role</c>
///     value used by ASP.NET Core's authorization policy.</para>
///     <para><b>Plexor-specific claims.</b> <c>permission</c> and
///     <c>service</c> are our additions. The <c>service</c> flag
///     discriminates API-key auth from JWT auth so authorization
///     rules can refuse JWT-only operations to service accounts.</para>
///     <para><b>Why constants, not magic strings.</b> A typo in a claim
///     name produces a silent authorization failure (claim is never
///     matched). Centralized constants make the typo a compile error.</para>
/// </remarks>
public static class IdentityClaims
{
    /// <summary>
    ///     Standard JWT <c>sub</c> claim. Caller's user id (for JWT
    ///     auth) or service-account owner id (for API key auth).
    /// </summary>
    public const string UserId = "sub";

    /// <summary>
    ///     Tenant the caller operates in. Standard across multi-tenant
    ///     systems; not a JWT RFC 7519 registered claim.
    /// </summary>
    public const string TenantId = "tid";

    /// <summary>
    ///     Optional project scope. <c>null</c> / missing = tenant-wide
    ///     operation. Phase 2+ when the Project entity lands.
    /// </summary>
    public const string ProjectId = "pid";

    /// <summary>
    ///     Role name. The standard
    ///     <see cref="System.Security.Claims.ClaimTypes.Role" />
    ///     value; ASP.NET Core authorization reads it as the role
    ///     claim by default.
    /// </summary>
    public const string Roles = "role";

    /// <summary>
    ///     Permission string. Multiple <c>permission</c> claims may be
    ///     present on a single principal (one per permission).
    ///     Authorization checks compare against this collection.
    /// </summary>
    public const string Permission = "permission";

    /// <summary>
    ///     Boolean flag. <c>"true"</c> when the caller authenticated
    ///     via API key, <c>"false"</c> or missing when authenticated
    ///     via JWT. Drives <see cref="ICurrentUser.IsService" />.
    /// </summary>
    public const string IsService = "service";

    /// <summary>
    ///     Issuer claim. The <c>iss</c> registered claim identifying
    ///     the principal-issuing auth server. Validated by the bearer
    ///     handler; signer always sets this to <see cref="Issuer" />.
    /// </summary>
    public const string Issuer = "iss";

    /// <summary>
    ///     JWT issuer value — constant string identifying the Plexor
    ///     auth server. The bearer handler validates the incoming
    ///     token's <c>iss</c> against this value.
    /// </summary>
    public const string IssuerValue = "plexor";
}