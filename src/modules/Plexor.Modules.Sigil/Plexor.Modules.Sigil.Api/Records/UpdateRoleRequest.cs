// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpdateRoleRequest — wire shape for PATCH /iam/roles/{id}.
// Extracted from IamControllers.cs (Sprint 3, item 4) per
// anti-patterns.md §2.
// ============================================================================

namespace Plexor.Modules.Sigil.Api.Records;

/// <summary>Wire shape for <c>PATCH /iam/roles/{id}</c>.</summary>
/// <param name="Description">New description (null = leave unchanged).</param>
/// <param name="Permissions">New permissions list (null = leave unchanged).</param>
public sealed record UpdateRoleRequest(string? Description, IReadOnlyCollection<string>? Permissions);