// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmRequests — wire shapes for the POST /api/v1/vms endpoint.
// Separate from controllers per coding/anti-patterns.md §2
// (records DTO → Application/Models, never inline in controllers).
// ============================================================================

using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Api.Models;

/// <summary>
///     Wire shape for <c>POST /api/v1/vms</c>. The operator picks a
///     Flavor (vCPU/RAM/disk preset), an Image (base image ref), and
///     may overlay individual <see cref="VmRuntimeConfig" /> fields
///     on the flavor's defaults. Validation lives in the handler
///     (FluentValidation isn't wired yet for v0.1 — the VmRuntimeConfig
///     validator surfaces all errors at once via
///     <see cref="Plexor.Shared.NodeApi.VmRuntimeConfigValidator" />).
/// </summary>
/// <param name="ClusterId">Target cluster.</param>
/// <param name="Name">Operator-facing VM name (unique per cluster).</param>
/// <param name="Flavor">
///     Optional flavor id (e.g. <c>"small"</c>). Null = the
///     handler resolves the catalog's first entry as the default.
/// </param>
/// <param name="Image">
///     Optional image id (e.g. <c>"ubuntu-22.04-cloud"</c>). Null =
///     the flavor's bundled default image.
/// </param>
/// <param name="Config">
///     Optional overlay over the flavor's default
///     <see cref="VmRuntimeConfig" />. Null fields fall through
///     to the flavor's value.
/// </param>
/// <param name="TargetNodeId">
///     Optional manual pin. Null = "pick for me" (currently maps to
///     "stay unassigned" under the v0.1 manual scheduler).
/// </param>
public sealed record CreateVmRequest(
    ClusterId ClusterId,
    string Name,
    string? Flavor = null,
    string? Image = null,
    VmRuntimeConfig? Config = null,
    NodeId? TargetNodeId = null);
