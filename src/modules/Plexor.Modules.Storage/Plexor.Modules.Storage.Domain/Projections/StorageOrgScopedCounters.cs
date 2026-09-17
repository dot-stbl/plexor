// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StorageOrgScopedCounters — projection returned by IStorageQuotaReader.
// Two counters per (OrgId): volume row count + cumulative GiB. The
// quota enforcer combines these with the new volume's delta to decide
// Allowed / AllowedWithWarning / Denied for both
// storage.volumes.count AND storage.volumes.gb.
//
// Record (not class) because the value is immutable + value-equal:
// the enforcer pattern-matches on the result and would otherwise
// allocate a defensive copy on every read.
// ============================================================================

namespace Plexor.Modules.Storage.Domain.Projections;

/// <summary>
///     Per-org storage counters — the load-bearing projection for the
///     storage.volumes.count and storage.volumes.gb quota keys.
/// </summary>
/// <param name="VolumeCount">Cumulative volume rows in the org.</param>
/// <param name="VolumeGbTotal">Cumulative GiB across all volume rows
/// in the org. Whole-number GiB (matches <see cref="Plexor.Modules.Storage.Domain.Entities.Volume.SizeGb" />).</param>
public sealed record StorageOrgScopedCounters(
    int VolumeCount,
    decimal VolumeGbTotal);
