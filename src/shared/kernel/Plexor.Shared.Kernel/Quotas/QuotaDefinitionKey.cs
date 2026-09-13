// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaDefinitionKey — strongly-typed wrapper around a stable catalog key
// (e.g. "compute.vms.count"). Prevents typo-driven silent-limit bugs in
// callers; the IQuotaEnforcer contract types it instead of taking a raw
// string. Lives in Plexor.Shared.Kernel so modules that don't depend on
// Plexor.Modules.Quotas.Application can still construct an enforcer call
// against the well-known built-in keys.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Strongly-typed wrapper around a catalog key string. v1 ships the
///     built-in keys as static properties; future operator-added keys
///     construct from raw <see cref="string" />.
/// </summary>
/// <param name="Value">Stable catalog identifier (e.g. <c>"compute.vms.count"</c>).</param>
/// <remarks>
///     <para><b>Why typed.</b> The catalog row is the source of truth for
///     the string; wrapping in <see langword="readonly" /><see cref="Value" />
///     <see langword="record" /><see langword="struct" /> keeps the
///     allocation-free call site (<c>CheckAndReserveAsync(scope,
///     QuotaDefinitionKey.VmsCount, 1, ct)</c>) and gives the compiler a
///     chance to catch typos at the call site.</para>
///     <para><b>Operator-added keys.</b> The built-in list covers every
///     <c>compute.* / storage.* / network.* / api.*</c> key shipped with
///     the platform. A future admin-added key (via 4.5.g PUT to the
///     catalog) constructs a fresh <see cref="QuotaDefinitionKey" />
///     from the raw string.</para>
/// </remarks>
public readonly record struct QuotaDefinitionKey(string Value)
{
    /// <summary>Number of VMs across the org/team/folder.</summary>
    public static QuotaDefinitionKey VmsCount { get; } = new("compute.vms.count");

    /// <summary>Cumulative vCPU across VMs.</summary>
    public static QuotaDefinitionKey VmsVcpu { get; } = new("compute.vms.vcpu");

    /// <summary>Cumulative RAM GiB across VMs.</summary>
    public static QuotaDefinitionKey VmsRamGb { get; } = new("compute.vms.ram_gb");

    /// <summary>Number of clusters in the org (the Plexor control-plane
    /// + joined NodeAgent fleet). Reserved by <c>CreateClusterCommandHandler</c>
    /// inside the resource-create transaction.</summary>
    public static QuotaDefinitionKey ClustersCount { get; } = new("compute.clusters.count");

    /// <summary>Number of workloads across all clusters in the org
    /// (control-plane view of every deployed workload). Reserved by
    /// <c>CreateWorkloadCommandHandler</c> inside the resource-create
    /// transaction.</summary>
    public static QuotaDefinitionKey WorkloadsCount { get; } = new("compute.workloads.count");

    /// <summary>Number of volumes across the scope.</summary>
    public static QuotaDefinitionKey VolumesCount { get; } = new("storage.volumes.count");

    /// <summary>Cumulative volume GiB.</summary>
    public static QuotaDefinitionKey VolumesGb { get; } = new("storage.volumes.gb");

    /// <summary>Number of floating IPs across the scope.</summary>
    public static QuotaDefinitionKey FloatingIpsCount { get; } = new("network.floating_ips.count");

    /// <summary>Number of load balancers across the scope.</summary>
    public static QuotaDefinitionKey LoadBalancersCount { get; } = new("network.load_balancers.count");

    /// <summary>Per-user sliding-window request count limit (1h).</summary>
    public static QuotaDefinitionKey ApiRequestsPerHourUser { get; } = new("api.requests.per_hour.user");

    /// <summary>Per-org sliding-window request count limit (1h).</summary>
    public static QuotaDefinitionKey ApiRequestsPerHourOrg { get; } = new("api.requests.per_hour.org");

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
