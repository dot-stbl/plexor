// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AddSshKeyRequest — wire shape for POST
// /iam/users/{userId}/ssh-keys. Extracted from IamControllers.cs
// (Sprint 3, item 4) per anti-patterns.md §2.
// ============================================================================

namespace Plexor.Modules.Sigil.Api.Records;

/// <summary>Wire shape for <c>POST /iam/users/{userId}/ssh-keys</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="Name"></param>
/// <param name="PublicKey"></param>
public sealed record AddSshKeyRequest(Guid OrgId, string Name, string PublicKey);