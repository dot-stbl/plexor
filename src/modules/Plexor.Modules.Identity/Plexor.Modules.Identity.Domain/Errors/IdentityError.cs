namespace Plexor.Modules.Identity.Domain;

/// <summary>
///     Discriminator for Identity-domain errors. Used by
///     <see cref="IdentityException" /> to make exception handling structured
///     without resorting to message-string matching.
/// </summary>
/// <remarks>
///     <para><b>Surface.</b> The kind is mapped 1:1 to RFC 7807 ProblemDetails
///     <c>type</c> by the controller error handler (Phase 4). For now
///     only the throwers consume it.</para>
/// </remarks>
public enum IdentityExceptionKind
{
    /// <summary>Unknown / unclassified failure.</summary>
    Unknown = 0,

    /// <summary>Email address did not match the validation regex.</summary>
    InvalidEmail,

    /// <summary>Password hash string is not a valid bcrypt at cost 12.</summary>
    InvalidPasswordHash,

    /// <summary>Permission string did not match the RBAC format.</summary>
    InvalidPermission,

    /// <summary>User supplied the wrong password during login.</summary>
    InvalidCredentials,

    /// <summary>User account is locked due to repeated failed logins.</summary>
    AccountLocked,

    /// <summary>User account is suspended (admin action).</summary>
    AccountSuspended,

    /// <summary>Refresh token reuse detected — family revocation triggered.</summary>
    RefreshTokenReplayed,

    /// <summary>API key presented an unknown kid or wrong secret.</summary>
    InvalidApiKey,

    /// <summary>API key's permissions exceeded the owner's permission set.</summary>
    ApiKeyPermissionsExceedOwner,

    /// <summary>SSH key already exists in the tenant (fingerprint collision).</summary>
    SshKeyFingerprintTaken,
}

/// <summary>
///     Domain-level exception raised by Identity entities, value objects,
///     and factories. Carries a discriminator kind so callers can react
///     programmatically instead of parsing the message.
/// </summary>
/// <remarks>
///     <para><b>Catch sites.</b> Application layer catches
///     <see cref="IdentityException" /> and maps each <see cref="Kind" /> to
///     a specific HTTP status (400 / 401 / 403 / 409 / 423). Controllers
///     never catch this directly — they bubble up to the global error
///     handler that converts to ProblemDetails.</para>
/// </remarks>
public sealed class IdentityException : Exception
{
    /// <summary>Discriminator for programmatic handling.</summary>
    public IdentityExceptionKind Kind { get; }

    /// <summary>
    ///     Constructs a domain error with a discriminator kind + message.
    /// </summary>
    /// <param name="kind">Discriminator (see <see cref="IdentityExceptionKind" />).</param>
    /// <param name="message">Human-readable description. Used in ProblemDetails
    /// detail field; never displayed in the UI directly.</param>
    public IdentityException(IdentityExceptionKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>
    ///     Constructs a domain error wrapping an inner exception. Used when
    ///     a wrapping layer (e.g. EF Core) throws and we want to surface
    ///     the underlying cause to higher layers.
    /// </summary>
    /// <param name="kind">Discriminator.</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="innerException">Underlying exception (logged, not displayed).</param>
    public IdentityException(IdentityExceptionKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }
}