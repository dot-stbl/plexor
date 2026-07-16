// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload request DTOs — wire shapes for the Workloads HTTP API.
// Separate from controllers per coding/anti-patterns.md §2.
// ==========================================================================

namespace Plexor.Modules.Clusters.Api.Models;

/// <summary>
///     Wire shape for <c>POST /api/v1/compute/clusters/{id}/workloads</c>.
///     <c>SpecJson</c> is opaque to the control plane — it's
///     forwarded to the assigned NodeAgent's runtime provider,
///     which interprets it per kind (vm / lxc / k8s.pod /
///     container). Stored verbatim.
/// </summary>
/// <param name="Name">Workload name (unique per cluster).</param>
/// <param name="Kind">Runtime identifier — "vm" / "lxc" / "k8s.pod" / "container".</param>
/// <param name="SpecJson">Operator-supplied spec (image, env, ports, volumes) as raw JSON.</param>
public sealed record CreateWorkloadRequest(
    string Name,
    string Kind,
    string SpecJson);