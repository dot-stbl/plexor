// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Flavor — a named VM size preset. Bundles the vCPU / RAM / disk
// triple an operator typically picks from ("small" / "medium" /
// "large") plus the default image the workload lands on. The
// control plane serves the catalog over the API so the dashboard
// can render a flavor selector; the NodeAgent consumes the resolved
// VmRuntimeConfig verbatim.
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Application.Flavors;

/// <summary>
///     A VM size preset the operator can pick from by name.
/// </summary>
/// <param name="Name">
///     Catalog id (e.g. <c>"small"</c>, <c>"medium"</c>,
///     <c>"large"</c>). Unique per
///     <see cref="IFlavorCatalog" /> instance.
/// </param>
/// <param name="Default">
///     The <see cref="VmRuntimeConfig" /> the workload inherits
///     when the operator picks this flavor. The operator may
///     override individual fields via the request's
///     <c>Config</c> overlay; the flavor seeds the rest.
/// </param>
public sealed record Flavor(
    string Name,
    VmRuntimeConfig Default);