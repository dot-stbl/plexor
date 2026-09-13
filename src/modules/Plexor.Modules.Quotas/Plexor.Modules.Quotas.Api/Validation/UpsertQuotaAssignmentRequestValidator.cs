// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertQuotaAssignmentRequestValidator — FluentValidation rule chain for
// the PUT /api/v1/quotas/assignments body (4.5.g.3). Runs in the
// controller via IValidator<UpsertQuotaAssignmentRequest> before any
// catalog lookup or repository call; failures surface as 400
// ValidationProblemDetails via the helper
// QuotasControllerHelpers.InvalidRequestResponse below.
//
// Lives in the Api project (not Application) so it can reference the
// request DTO without creating an Application -> Api reference cycle.
// Registered in QuotasApiInstaller alongside the controller.
// ============================================================================

using FluentValidation;
using Plexor.Modules.Quotas.Api.Models;

namespace Plexor.Modules.Quotas.Api.Validation;

/// <summary>
///     Validates the <see cref="UpsertQuotaAssignmentRequest" /> body
///     before the controller dispatches the upsert. Rules:
///     <list type="bullet">
///       <item><see cref="UpsertQuotaAssignmentRequest.DefinitionKey" />
///       is non-empty.</item>
///       <item><see cref="UpsertQuotaAssignmentRequest.ScopeKind" />
///       is one of <c>"org"</c>, <c>"team"</c>, <c>"folder"</c>
///       (lowercase).</item>
///       <item><see cref="UpsertQuotaAssignmentRequest.ScopeId" />
///       is not <see cref="Guid.Empty" />.</item>
///       <item><see cref="UpsertQuotaAssignmentRequest.Value" /> is
///       strictly positive (<c>&gt; 0</c>).</item>
///       <item><see cref="UpsertQuotaAssignmentRequest.Period" /> is
///       either empty (inherit from the catalog row) or one of the
///       <c>QuotaPeriod</c> member names.</item>
///     </list>
/// </summary>
/// <remarks>
///     <para><b>Why "inherit" via empty string.</b> The
///     <see cref="UpsertQuotaAssignmentRequest.Period" /> is
///     optional — when the caller wants to mirror the catalog row's
///     <c>Period</c>, they send an empty string and the controller
///     reads the value off the resolved <c>QuotaDefinition</c>.</para>
///     <para><b>Why <c>must</c> on <see cref="UpsertQuotaAssignmentRequest.Period" />.</b>
///     A non-empty unknown value would otherwise propagate through to
///     <c>Enum.Parse</c> and surface as a 500 from
///     <c>ArgumentException</c>; failing fast at the boundary gives the
///     caller a stable 400.</para>
///     <para><b>Why not validate <c>DefinitionKey</c> against the
///     catalog here.</b> The validator runs in DI scope with no DB
///     access; "definition exists" is enforced by the controller
///     after the validator passes, surfacing as 404.</para>
/// </remarks>
public sealed class UpsertQuotaAssignmentRequestValidator : AbstractValidator<UpsertQuotaAssignmentRequest>
{
    /// <summary>
    ///     The set of valid <see cref="UpsertQuotaAssignmentRequest.ScopeKind" />
    ///     values, kept as a single source of truth so the test project
    ///     and the controller parser stay aligned.
    /// </summary>
    public static readonly IReadOnlyCollection<string> AllowedScopeKinds =
        ["org", "team", "folder"];

    /// <summary>
    ///     The set of valid non-empty <see cref="UpsertQuotaAssignmentRequest.Period" />
    ///     values, matching the <c>QuotaPeriod</c> enum member names.
    /// </summary>
    public static readonly IReadOnlyCollection<string> AllowedPeriods =
        ["None", "Hour", "Day", "Month"];

    /// <summary>Construct the validator with the standard rule chain.</summary>
    public UpsertQuotaAssignmentRequestValidator()
    {

        RuleFor(static request => request.DefinitionKey)
            .NotEmpty()
                .WithMessage("DefinitionKey is required.");

        RuleFor(static request => request.Value)
            .GreaterThan(0m)
                .WithMessage("Value must be greater than zero.");

        RuleFor(static request => request.ScopeId)
            .NotEqual(Guid.Empty)
                .WithMessage("ScopeId must not be empty.");

        RuleFor(static request => request.ScopeKind)
            .NotEmpty()
                .Must(static kind => AllowedScopeKinds.Contains(kind))
                    .WithMessage("ScopeKind must be 'org', 'team', or 'folder'.");

        RuleFor(static request => request.Period)
            .Must(static period => string.IsNullOrEmpty(period) || AllowedPeriods.Contains(period))
                .WithMessage("Period must be empty (inherit) or one of None/Hour/Day/Month.");
    }
}
