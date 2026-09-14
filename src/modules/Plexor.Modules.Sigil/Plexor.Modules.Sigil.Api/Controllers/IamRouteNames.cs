// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamRouteNames — route names referenced by [HttpGet/Post/Patch/Delete(..., Name = ...)]
// and CreatedAtAction(...). CreatedAtAction looks up the action by its
// routing name (the value of `Name =`), NOT by the C# method name;
// `nameof(GetAsync)` fails at runtime with 'Cannot resolve action'. The
// class keeps the strings in one place so refactors are safe and the
// compiler verifies both call sites match.
//
// Was two file-static classes inside IamControllers.cs (Sprint 3,
// item 4 — moved here when IamRolesController + IamBindingsController
// split into separate files; file-static scope prevented
// IamBindingsController from seeing IamRolesRouteNames).
// ============================================================================

namespace Plexor.Modules.Sigil.Api.Controllers;

/// <summary>
///     Route-name constants for the /iam/* controllers. Grouped here
///     so refactors stay safe across controllers.
/// </summary>
public static class IamRouteNames
{
    /// <summary>POST /iam/roles — create a custom role.</summary>
    public const string RolesCreate = "iam-roles-create";

    /// <summary>GET /iam/roles/{roleId} — fetch one role.</summary>
    public const string RolesGet = "iam-roles-get";

    /// <summary>GET /iam/roles — list roles.</summary>
    public const string RolesList = "iam-roles-list";

    /// <summary>PATCH /iam/roles/{roleId} — update a role.</summary>
    public const string RolesUpdate = "iam-roles-update";

    /// <summary>DELETE /iam/roles/{roleId} — delete a role.</summary>
    public const string RolesDelete = "iam-roles-delete";

    /// <summary>POST /iam/role-bindings — create a binding.</summary>
    public const string RoleBindingsCreate = "iam-role-bindings-create";

    /// <summary>GET /iam/role-bindings — list bindings.</summary>
    public const string RoleBindingsList = "iam-role-bindings-list";

    /// <summary>DELETE /iam/role-bindings/{bindingId} — remove a binding.</summary>
    public const string RoleBindingsDelete = "iam-role-bindings-delete";

    /// <summary>POST /iam/users/{userId}/api-keys — issue an API key.</summary>
    public const string ApiKeysIssue = "iam-api-keys-issue";

    /// <summary>GET /iam/users/{userId}/api-keys — list API keys.</summary>
    public const string ApiKeysList = "iam-api-keys-list";

    /// <summary>DELETE /iam/users/{userId}/api-keys/{keyId} — revoke an API key.</summary>
    public const string ApiKeysRevoke = "iam-api-keys-revoke";

    /// <summary>POST /iam/users/{userId}/ssh-keys — add an SSH public key.</summary>
    public const string SshKeysAdd = "iam-ssh-keys-add";

    /// <summary>GET /iam/users/{userId}/ssh-keys — list SSH keys.</summary>
    public const string SshKeysList = "iam-ssh-keys-list";

    /// <summary>DELETE /iam/users/{userId}/ssh-keys/{keyId} — revoke an SSH key.</summary>
    public const string SshKeysRevoke = "iam-ssh-keys-revoke";
}