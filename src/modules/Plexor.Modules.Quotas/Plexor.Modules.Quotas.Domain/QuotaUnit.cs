namespace Plexor.Modules.Quotas.Domain;

/// <summary>
///     Unit of measurement for a quota's value. Stored as a string in
///     the catalog (<c>quotas.quota_definitions</c>) so a future unit
///     addition does not require a schema migration.
/// </summary>
public enum QuotaUnit
{
    /// <summary>Discrete count of resources (VMs, volumes, IPs, ...).</summary>
    Count = 0,

    /// <summary>Cumulative gibibytes of capacity (RAM, volume size, ...).</summary>
    Gb = 1,

    /// <summary>Cumulative virtual CPU cores.</summary>
    Vcpu = 2,

    /// <summary>Requests per hour (sliding-window rate limit).</summary>
    ReqPerHour = 3,
}
