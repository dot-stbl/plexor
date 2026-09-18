// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ImageFormat — disk image formats the fetcher may produce. Mirrors
// VolumeFormat in Plexor.Shared.Compute but lives in the fetcher
// because the fetcher is the only layer that needs to convert
// between them (e.g. raw → qcow2). VolumeFormat stays at the
// storage boundary so the downstream VM provider doesn't have to
// reason about upstream formats.
// ==========================================================================

namespace Plexor.NodeAgent.Providers.Image.Fetch;

/// <summary>
///     Disk image format recognised by the fetcher. Matches the
///     wire-side catalogue entry <c>Format</c> field; the fetcher
///     only knows how to convert to/from <see cref="Qcow2" />.
/// </summary>
public enum ImageFormat
{
    /// <summary>qcow2 — qemu copy-on-write (sparse, supports backing files).</summary>
    Qcow2 = 0,

    /// <summary>raw — fixed-size, no metadata.</summary>
    Raw = 1
}

