// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Module-local DatabaseInformation for Plexor.Modules.Network. The
// schema name `network` follows the Plexor architecture theme (one
// word, one token — see AGENTS.md). Lives next to the entities that
// own the schema; the shared Plexor.Shared.Persistence.DatabaseInformation
// is reserved for cross-module aggregation.
// ============================================================================

namespace Plexor.Modules.Network.Infrastructure.Persistence;

/// <summary>
///     Module-local single source of truth for the Network PostgreSQL
///     schema + table names. The schema follows the architecture
///     theme (<c>network</c>); tables follow snake_case. Constants
///     are referenced by every entity configuration so a typo becomes
///     a compile-time error.
/// </summary>
public static class DatabaseInformation
{
    /// <summary>PostgreSQL schema names — one per module.</summary>
    public static class Schemes
    {
        /// <summary>Floating IPs + load balancers
        /// (Plexor.Modules.Network).</summary>
        public const string Network = "network";
    }

    /// <summary>PostgreSQL table names owned by the Network module.</summary>
    public static class Tables
    {
        /// <summary>One row per floating IP
        /// (<c>network.floating_ips</c>). Drives the
        /// <c>network.floating_ips.count</c> quota key.</summary>
        public const string FloatingIps = "floating_ips";

        /// <summary>One row per load balancer
        /// (<c>network.load_balancers</c>). Drives the
        /// <c>network.load_balancers.count</c> quota key.</summary>
        public const string LoadBalancers = "load_balancers";
    }
}
