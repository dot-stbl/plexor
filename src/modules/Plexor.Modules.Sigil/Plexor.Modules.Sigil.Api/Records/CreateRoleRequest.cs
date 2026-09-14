// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateRoleRequest — wire shape for POST /iam/roles. Extracted
// from IamControllers.cs (Sprint 3, item 4) per anti-patterns.md §2
// (records DTO — separate file, not in controller).
// ============================================================================

namespace Plexor.Modules.Sigil.Api.Records;

/// <summary>Wire shape for <c>POST /iam/roles</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="Name"></param>
/// <param name="Description"></param>
/// <param name="Permissions"></param>
public sealed record CreateRoleRequest(
    Guid OrgId,
    string Name,
    string? Description,
    IReadOnlyCollection<string> Permissions);