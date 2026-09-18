// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IFlavorCatalog — read-only catalog of VM flavor presets served by
// the control plane. The v0.1 implementation is a hard-coded list
// (DefaultFlavorCatalog); v0.2+ may swap to a DB-backed admin-
// managed catalog without touching the call sites.
// ============================================================================

namespace Plexor.Modules.Clusters.Application.Flavors;

/// <summary>
///     Catalog of <see cref="Flavor" /> presets the operator can
///     pick from. Registered as a singleton in DI (catalogs are
///     immutable during the process lifetime).
/// </summary>
public interface IFlavorCatalog
{
    /// <summary>
    ///     List every flavor the catalog knows about. Order is
    ///     catalog-defined (typically ascending resource size).
    /// </summary>
    public IReadOnlyList<Flavor> List();

    /// <summary>
    ///     Look up a flavor by its <see cref="Flavor.Name" />.
    ///     Returns null when the catalog has no entry — callers
    ///     surface this as 400 Bad Request with the available
    ///     names listed in the response body.
    /// </summary>
    /// <param name="name">Catalog id to resolve.</param>
    public Flavor? Get(string name);
}
