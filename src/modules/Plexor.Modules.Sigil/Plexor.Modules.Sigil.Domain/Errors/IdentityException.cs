namespace Plexor.Modules.Sigil.Domain.Errors;

/// <summary>
///     Discriminator string constants for Identity-domain errors. Strings
///     not enum — same approach as
///     <see cref="Plexor.Modules.Sigil.Domain.ValueObjects.PermissionScope" />:
///     flat, lowercase, dot-delimited. The string is mapped 1:1 to the
///     RFC 7807 ProblemDetails <c>type</c> by the controller error
///     handler (Phase 4). For now only the throwers consume the code.
/// </summary>
/// <remarks>
///     <para><b>Why strings not enum.</b> A new error category needs to
///     reach the FE (kubb-generated client) without a C# rebuild —
///     openapi-typescript + kubb codegen read the ProblemDetails
///     <c>type</c> field and produce a typed union. Strings stay
///     stable across renames; enums force the client to redeploy on
///     every enum-value addition.</para>
///     <para><b>Naming convention.</b> <c>identity.&lt;entity&gt;.&lt;condition&gt;</c>
///     — three lowercase segments, no trailing context. The
///     ProblemDetails <c>type</c> field becomes a URI like
///     <c>/errors/identity.email.invalid</c> (prefix added at the
///     ASP.NET Core error handler).</para>
/// </remarks>
public static class IdentityExceptions
{
    /// <summary>Unclassified failure. Don't use unless nothing else fits.</summary>
    public const string Unknown = "identity.unknown";

    /// <summary>Email address did not match the validation regex.</summary>
    public const string InvalidEmail = "identity.email.invalid";

    /// <summary>Password hash string is not a valid bcrypt at the configured cost factor.</summary>
    public const string InvalidPasswordHash = "identity.password_hash.invalid";

    /// <summary>Permission string did not match the RBAC format.</summary>
    public const string InvalidPermission = "identity.permission.invalid";

    /// <summary>User supplied the wrong password during login.</summary>
    public const string InvalidCredentials = "identity.credentials.invalid";

    /// <summary>User account is locked due to repeated failed logins.</summary>
    public const string AccountLocked = "identity.account.locked";

    /// <summary>User account is suspended (admin action).</summary>
    public const string AccountSuspended = "identity.account.suspended";

    /// <summary>Refresh token reuse detected — family revocation triggered.</summary>
    public const string RefreshTokenReplayed = "identity.refresh_token.replayed";

    /// <summary>API key presented an unknown kid or wrong secret.</summary>
    public const string InvalidApiKey = "identity.api_key.invalid";

    /// <summary>API key's permissions exceeded the owner's permission set.</summary>
    public const string ApiKeyPermissionsExceedOwner = "identity.api_key.permissions_exceed_owner";

    /// <summary>SSH key already exists in the tenant (fingerprint collision).</summary>
    public const string SshKeyFingerprintTaken = "identity.ssh_key.fingerprint_taken";

    /// <summary>
    ///     Login attempted with email+password against a tenant configured
    ///     for external OIDC. The console surfaces the <c>redirect</c>
    ///     extension to drive the operator through
    ///     <c>POST /auth/oidc/authorize</c> instead.
    /// </summary>
    public const string CredentialsProviderMismatch = "identity.credentials.provider_mismatch";

    /// <summary>Refresh token could not be decoded as a JWT (truly
    /// malformed — not just an opaque token, which is normal).</summary>
    public const string RefreshMalformed = "identity.refresh.malformed";

    /// <summary>OIDC id_token is missing required claims (email or
    /// preferred_username / name) needed to provision a Plexor User row.</summary>
    public const string OidcUserMissingClaims = "oidc.user.missing_claims";

    /// <summary>OIDC user provisioning failed for a non-claims reason
    /// (DB error, schema mismatch, etc.). The inner exception is
    /// logged but never surfaced to the response body.</summary>
    public const string OidcUserProvisioningFailed = "oidc.user.provisioning_failed";
}

/// <summary>
///     Domain-level exception raised by Identity entities, value objects,
///     and factories. Carries a discriminator code so callers can react
///     programmatically instead of parsing the message string.
/// </summary>
/// <remarks>
///     <para><b>Catch sites.</b> Application layer catches
///     <see cref="IdentityException" /> and maps each
///     <see cref="IdentityExceptions" /> code to a specific HTTP status
///     (400 / 401 / 403 / 409 / 423). Controllers never catch this
///     directly — they bubble up to the global error handler that
///     converts to ProblemDetails.</para>
///     <para><b>Why a typed Code property.</b> Using a string field on
///     the exception (not the type) lets you catch a single exception
///     type and dispatch on <c>ex.Code</c> without switch-on-string
///     typo-safety. Compare to <see cref="System.ArgumentException.ParamName" />.</para>
///     <para><b>Extensions.</b> Some errors carry structured
///     additional fields that must reach the client (e.g. the
///     <c>redirect</c> URL when the login endpoint rejects an email
///     +password attempt against an OIDC tenant — see
///     <see cref="IdentityExceptions.CredentialsProviderMismatch" />).
///     These are surfaced as RFC 7807 <c>extensions</c> by the
///     exception handler.</para>
/// </remarks>
public sealed class IdentityException : Exception
{
    /// <summary>
    ///     Stable discriminator code (one of the
    ///     <see cref="IdentityExceptions" /> constants). Mapped to
    ///     RFC 7807 ProblemDetails <c>type</c> field by the controller
    ///     error handler.
    /// </summary>
    public string Code { get; }

    /// <summary>
    ///     Optional structured payload copied into the ProblemDetails
    ///     <c>extensions</c> dictionary by the exception handler.
    ///     <c>null</c> when the error has no extra wire fields.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Extensions { get; }

    /// <summary>
    ///     Constructs a domain error with a discriminator code + message.
    /// </summary>
    /// <param name="code">One of the <see cref="IdentityExceptions" />
    /// constants. Validated non-empty but not to a known set — let the
    /// thrower pick the precise string.</param>
    /// <param name="message">Human-readable description. Used in
    /// ProblemDetails <c>detail</c> field; never displayed in the UI
    /// directly.</param>
    public IdentityException(string code, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Identity exception code cannot be null or whitespace.",
                nameof(code));
        }

        Code = code;
    }

    /// <summary>
    ///     Constructs a domain error wrapping an inner exception. Used
    ///     when a wrapping layer (e.g. EF Core) throws and we want to
    ///     surface the underlying cause to higher layers.
    /// </summary>
    /// <param name="code">Discriminator code (see <see cref="IdentityExceptions" />).</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="innerException">Underlying exception (logged, not displayed).</param>
    public IdentityException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Identity exception code cannot be null or whitespace.",
                nameof(code));
        }

        Code = code;
    }

    /// <summary>
    ///     Constructs a domain error with a discriminator code, message,
    ///     and a structured payload that the exception handler copies
    ///     into the ProblemDetails <c>extensions</c> dictionary. Used
    ///     when the error carries wire fields the client must read
    ///     (e.g. <c>redirect</c> for the provider-mismatch response).
    /// </summary>
    /// <param name="code">Discriminator code (see <see cref="IdentityExceptions" />).</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="extensions">Wire payload. Keys are extension
    /// names; values are serialized verbatim into the response JSON.
    /// Compiler-enforced non-null by signature; no runtime check
    /// (project bans <c>ThrowIf*</c> — see code-shape.md §11).</param>
    public IdentityException(
        string code,
        string message,
        IReadOnlyDictionary<string, object?> extensions)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Identity exception code cannot be null or whitespace.",
                nameof(code));
        }

        Code = code;
        Extensions = extensions;
    }
}
