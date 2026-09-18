// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VolumeStatus — lifecycle states for a Plexor storage volume. The
// status transitions reflect the operational phases of a disk volume
// attached to a Node: a new volume is Pending; the runtime begins the
// attach dance (Attaching); on success the volume is Attached; the
// operator may detach (Detached); failures land in Error.
//
// The enum is stored as a varchar in the storage.volumes.status column
// via HasConversion<string> so a future member addition does not require
// a schema migration.
// ============================================================================

namespace Plexor.Modules.Storage.Domain.Entities;

/// <summary>
///     Lifecycle states for a <see cref="Volume" />. Transitions:
///     <c>Pending</c> → <c>Attaching</c> → <c>Attached</c> ↔ <c>Detached</c>;
///     any state may transition to <c>Error</c>.
/// </summary>
public enum VolumeStatus
{
    /// <summary>Volume row exists but no attach attempt has run yet.</summary>
    Pending = 0,

    /// <summary>The runtime is currently attaching the volume to its node.</summary>
    Attaching = 1,

    /// <summary>Volume is mounted on the target node.</summary>
    Attached = 2,

    /// <summary>Volume exists but is detached from the node (kept for re-attach).</summary>
    Detached = 3,

    /// <summary>The last attach / detach attempt failed; row is retained for operator triage.</summary>
    Error = 4,
}
