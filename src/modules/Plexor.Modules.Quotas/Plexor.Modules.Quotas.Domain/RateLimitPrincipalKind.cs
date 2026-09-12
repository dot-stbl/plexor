namespace Plexor.Modules.Quotas.Domain;

/// <summary>
///     Subject of a <c>RateLimitEvent</c> row. Stored as a string in
///     the database so a future principal kind (e.g. <c>"service_account"</c>)
///     does not require a schema migration.
/// </summary>
public enum RateLimitPrincipalKind
{
    /// <summary>Authenticated human user (JWT auth).</summary>
    User = 0,

    /// <summary>Long-lived service API key.</summary>
    ApiKey = 1,
}
