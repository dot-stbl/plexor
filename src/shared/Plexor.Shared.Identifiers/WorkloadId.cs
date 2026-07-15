// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadId — strongly-typed Plexor workload identifier.
//
// A workload is anything Plexor can run on a node: currently Docker
// containers (Phase D). Future runtimes (KVM/LXC, k3s pods) will
// share this ID space — the prefix denotes "managed entity on a
// node" and is otherwise polymorphic.
// ============================================================================

namespace Plexor.Shared.Identifiers;

/// <summary>
///     Identifies a <c>Workload</c> row in <c>forge.workloads</c>.
///     The string form is <c>wl_</c> + UUIDv7 lowercase no-dashes.
/// </summary>
/// <param name="Value">Raw UUIDv7 bytes.</param>
public readonly partial record struct WorkloadId(Guid Value)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Prefix + IdParse.FormattedUuid(Value);
    }

    /// <summary>Canonical literal prefix.</summary>
    public const string Prefix = "wl_";
}
