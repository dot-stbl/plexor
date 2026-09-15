// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateRoleBindingRequest — wire shape for POST /iam/role-bindings.
// Extracted from IamControllers.cs (Sprint 3, item 4) per
// anti-patterns.md §2.
// ============================================================================

namespace Plexor.Modules.Sigil.Api.Records;

/// <summary>Wire shape for <c>POST /iam/role-bindings</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="UserId"></param>
/// <param name="RoleId"></param>
public sealed record CreateRoleBindingRequest(Guid OrgId, Guid UserId, Guid RoleId);
