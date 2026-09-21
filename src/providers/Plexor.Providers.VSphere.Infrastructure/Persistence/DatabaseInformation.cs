namespace Plexor.Providers.VSphere.Infrastructure.Persistence;

/// <summary>
///     Module-local single source of truth for the vSphere
///     PostgreSQL schema and table names. The schema name follows the
///     Plexor architecture theme (one-word one-token; <c>outpost</c>);
///     the table names follow snake_case. No literal table or schema
///     strings appear in the entity configurations — they all
///     reference the constants here so a typo becomes a compile-time
///     error.
/// </summary>
/// <remarks>
///     <para><b>Why <c>outpost</c>.</b> <c>outpost</c> is the
///     planned node-registry schema; the vSphere provider is its
///     first real consumer (the cached inventory + provisioning
///     audit trail both live here). See AGENTS.md §"Mapping: schema
///     ↔ module ↔ entities".</para>
///     <para><b>Why module-local.</b> The shared
///     Plexor.Shared.Persistence.DatabaseInformation aggregates
///     every schema across the fleet so cross-module FKs can be
///     written down. The vSphere schema is self-contained (no FKs
///     into other modules today), so the names live next to the
///     entities that own them.</para>
/// </remarks>
public static class DatabaseInformation
{
    /// <summary>PostgreSQL schema name for the vSphere provider.</summary>
    public static class Schemes
    {
        /// <summary>Cached vSphere inventory + provisioning audit
        /// trail (Plexor.Providers.VSphere).</summary>
        public const string VSphere = "outpost";
    }

    /// <summary>PostgreSQL table names owned by the vSphere
    /// provider.</summary>
    public static class Tables
    {
        /// <summary>Header row for an inventory refresh — one per
        /// snapshot.</summary>
        public const string InventorySnapshots = "vsphere_inventory_snapshots";

        /// <summary>Per-cluster rows for a snapshot.</summary>
        public const string Clusters = "vsphere_clusters";

        /// <summary>Per-host rows for a snapshot.</summary>
        public const string Hosts = "vsphere_hosts";

        /// <summary>Per-VM rows for a snapshot.</summary>
        public const string VirtualMachines = "vsphere_virtual_machines";

        /// <summary>Provisioning audit trail — one row per clone
        /// attempt.</summary>
        public const string ProvisioningRuns = "vsphere_provisioning_runs";
    }
}
