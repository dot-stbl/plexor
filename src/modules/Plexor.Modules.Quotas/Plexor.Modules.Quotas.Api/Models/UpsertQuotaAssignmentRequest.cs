// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertQuotaAssignmentRequest — wire shape for PUT /api/v1/quotas/assignments
// (4.5.g.3). Body carries the catalog key, scope, value, and optional
// period override. Validated by UpsertQuotaAssignmentRequestValidator
// (FluentValidation) before the controller resolves the definition id
// and dispatches to IQuotaAssignmentRepository.UpsertAsync.
// ============================================================================

namespace Plexor.Modules.Quotas.Api.Models;

/// <summary>
///     Wire shape for the upsert quota-assignment body. The controller
///     accepts this DTO and the <c>UpsertQuotaAssignmentRequestValidator</c>
///     runs before any catalog lookup.
/// </summary>
/// <remarks>
///     <para><b>Why a string for <see cref="ScopeKind" />.</b>
///     Mirrors the <c>?scope=</c> convention on the GET endpoints
///     (<c>"org"</c>, <c>"team"</c>, <c>"folder"</c>). The validator
///     rejects anything else; the controller parses the lowercase value
///     via the existing <c>QuotasControllerHelpers.TryParseScope</c>.</para>
///     <para><b>Why a string for <see cref="Period" />.</b>
///     Matches the <c>QuotaPeriod</c> enum name (<c>"None"</c>,
///     <c>"Hour"</c>, ...). An empty string means "inherit the
///     catalog row's period" — the controller reads the value from the
///     resolved <c>QuotaDefinition</c> in that case. The validator
///     rejects anything that is neither empty nor a known enum name.</para>
///     <para><b>Why <see cref="decimal" /> for <see cref="Value" />.</b>
///     Matches the entity field (<c>QuotaAssignment.Value</c>) and the
///     catalog default (<c>QuotaDefinition.DefaultValue</c>); the
///     validator rejects zero or negative values.</para>
/// </remarks>
public sealed class UpsertQuotaAssignmentRequest
{
    /// <summary>Stable catalog identifier
    /// (<c>"compute.vms.count"</c>, <c>"api.requests.per_hour.org"</c>,
    /// ...). The controller resolves this to a <c>QuotaDefinition.Id</c>
    /// via <c>IQuotaCatalog.FindByKeyAsync</c>; a missing catalog row
    /// yields 404.</summary>
    public string DefinitionKey { get; init; } = string.Empty;

    /// <summary>Scope discriminator — <c>"org"</c>, <c>"team"</c>, or
    /// <c>"folder"</c>. Validated by the rule chain; the controller
    /// parses it via the existing <c>TryParseScope</c> helper.</summary>
    public string ScopeKind { get; init; } = string.Empty;

    /// <summary>Id of the matching Realm entity (Organization, Team,
    /// or Folder). Must not be <see cref="Guid.Empty" />.</summary>
    public Guid ScopeId { get; init; }

    /// <summary>The limit value for this assignment. Must be positive
    /// (<c>&gt; 0</c>). Matches the <c>QuotaAssignment.Value</c>
    /// column type.</summary>
    public decimal Value { get; init; }

    /// <summary>Period override — empty means "inherit from the
    /// catalog row's <c>Period</c>". Otherwise one of <c>"None"</c>,
    /// <c>"Hour"</c>, <c>"Day"</c>, <c>"Month"</c> (case-sensitive
    /// enum name). v1 only ships <c>None</c> and <c>Hour</c>;
    /// <c>Day</c> / <c>Month</c> are reserved for Phase 5+ billing
    /// windows.</summary>
    public string Period { get; init; } = string.Empty;
}
