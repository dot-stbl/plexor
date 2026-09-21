// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TemplateSummary — vCenter /api/vcenter/vm-template wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Inventory.Workloads;

/// <summary>
///     VM-template inventory row — same shape as a regular VM but
///     flagged via the <c>is_template</c> discriminator. The
///     provisioning endpoint accepts either a
///     <see cref="VirtualMachineSummary.Moref" /> of a template or
///     its inventory name.
/// </summary>
public sealed record TemplateSummary
{
    /// <summary>Template mo-ref.</summary>
    public required string Moref { get; init; }

    /// <summary>Template name (e.g. <c>"ubuntu-22.04-base"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Inventory folder path (templates usually live under
    /// a <c>/Templates/</c> folder).</summary>
    public string? FolderPath { get; init; }

    /// <summary>vCenter library name the template was imported from,
    /// when known. Null for ad-hoc / unmanaged templates.</summary>
    public string? LibraryName { get; init; }
}
