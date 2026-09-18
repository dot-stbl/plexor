// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DefaultFlavorCatalog — v0.1 hard-coded flavor list. Three
// presets matching the OpenStack "small / medium / large" naming
// convention. The bundled image is the operator-facing default for
// a flavor; the request body may override it via Config.ImageRef.
//
// Sizing rationale:
//   - small  : 1 vCPU / 2 GiB RAM / 20 GiB disk — minimum viable
//               for most distros + a few small services.
//   - medium : 2 vCPU / 4 GiB RAM / 40 GiB disk — typical web tier.
//   - large  : 4 vCPU / 8 GiB RAM / 80 GiB disk — DB / cache tier.
//
// Future flavors (xlarge, gpu.*, arm64.*) are admin-editable in
// the v0.2 catalog; the wire shape doesn't change.
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Application.Flavors;

/// <summary>
///     v0.1 <see cref="IFlavorCatalog" /> implementation. The list
///     is sealed in code (not config / DB) — changing the catalog
///     is a code change, reviewed like the rest of the API surface.
/// </summary>
public sealed class DefaultFlavorCatalog : IFlavorCatalog
{
    /// <summary>
    ///     The single, hard-coded list. Cached as a field so a hot
    ///     request loop doesn't reallocate the array on every
    ///     <see cref="List" /> call.
    /// </summary>
    private static readonly IReadOnlyList<Flavor> Flavors = new Flavor[]
    {
        new(
            Name: "small",
            Default: new VmRuntimeConfig(
                Vcpu: 1,
                RamBytes: 2L * 1024 * 1024 * 1024,
                DiskBytes: 20L * 1024 * 1024 * 1024,
                ImageRef: "ubuntu-22.04-cloud",
                NetworkName: null)),
        new(
            Name: "medium",
            Default: new VmRuntimeConfig(
                Vcpu: 2,
                RamBytes: 4L * 1024 * 1024 * 1024,
                DiskBytes: 40L * 1024 * 1024 * 1024,
                ImageRef: "ubuntu-22.04-cloud",
                NetworkName: null)),
        new(
            Name: "large",
            Default: new VmRuntimeConfig(
                Vcpu: 4,
                RamBytes: 8L * 1024 * 1024 * 1024,
                DiskBytes: 80L * 1024 * 1024 * 1024,
                ImageRef: "ubuntu-22.04-cloud",
                NetworkName: null)),
    };

    /// <inheritdoc />
    public IReadOnlyList<Flavor> List()
    {
        return Flavors;
    }

    /// <inheritdoc />
    public Flavor? Get(string name)
    {
        foreach (var flavor in Flavors)
        {
            if (string.Equals(flavor.Name, name, StringComparison.Ordinal))
            {
                return flavor;
            }
        }

        return null;
    }
}