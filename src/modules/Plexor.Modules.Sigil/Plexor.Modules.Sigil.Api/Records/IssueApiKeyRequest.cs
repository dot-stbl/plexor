// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IssueApiKeyRequest — wire shape for POST
// /iam/users/{userId}/api-keys. Extracted from IamControllers.cs
// (Sprint 3, item 4) per anti-patterns.md §2.
// ============================================================================

namespace Plexor.Modules.Sigil.Api.Records;

/// <summary>Wire shape for <c>POST /iam/users/{userId}/api-keys</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="Name"></param>
/// <param name="Permissions"></param>
/// <param name="ExpiresAtUtc"></param>
public sealed record IssueApiKeyRequest(
    Guid OrgId,
    string Name,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset? ExpiresAtUtc);