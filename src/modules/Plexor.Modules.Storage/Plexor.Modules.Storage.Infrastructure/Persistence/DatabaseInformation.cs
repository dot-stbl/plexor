// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Module-local DatabaseInformation for Plexor.Modules.Storage. The
// schema name `storage` follows the Plexor architecture theme (one
// word, one token — see AGENTS.md). Lives next to the entities that
// own the schema; the shared Plexor.Shared.Persistence.DatabaseInformation
// is reserved for cross-module aggregation.
// ============================================================================

namespace Plexor.Modules.Storage.Infrastructure.Persistence;

/// <summary>
///     Module-local single source of truth for the Storage PostgreSQL
///     schema + table names. The schema follows the architecture theme
///     (<c>storage</c>); tables follow snake_case. Constants are
///     referenced by every entity configuration so a typo becomes a
///     compile-time error.
/// </summary>
public static class DatabaseInformation
{
    /// <summary>PostgreSQL schema names — one per module.</summary>
    public static class Schemes
    {
        /// <summary>Volumes + buckets + future object-store rows
        /// (Plexor.Modules.Storage).</summary>
        public const string Storage = "storage";
    }

    /// <summary>PostgreSQL table names owned by the Storage module.</summary>
    public static class Tables
    {
        /// <summary>One row per disk volume
        /// (<c>storage.volumes</c>). Drives the
        /// <c>storage.volumes.count</c> and
        /// <c>storage.volumes.gb</c> quota keys.</summary>
        public const string Volumes = "volumes";

        /// <summary>One row per S3-compatible bucket
        /// (<c>storage.buckets</c>). Not quota-affecting in v0.1.</summary>
        public const string Buckets = "buckets";
    }
}
