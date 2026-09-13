// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertQuotaAssignmentRequestValidatorShould — exercise the 4.5.g.3
// FluentValidation chain in isolation. No DB, no controller — the
// validator is a pure function over the request DTO. Each test
// constructs a fresh request + validator so concurrent tests don't
// share state.
// ============================================================================

using FluentValidation.TestHelper;
using Plexor.Modules.Quotas.Api.Models;
using Plexor.Modules.Quotas.Api.Validation;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Quotas.Unit;

/// <summary>
///     Behavioural tests for <see cref="UpsertQuotaAssignmentRequestValidator" />.
///     Covers the rule chain documented on the validator's class remarks:
///     non-empty catalog key, non-empty <c>ScopeKind</c> in
///     {org, team, folder}, non-empty <c>ScopeId</c>, positive
///     <c>Value</c>, and either empty or a known <c>Period</c> name.
/// </summary>
public sealed class UpsertQuotaAssignmentRequestValidatorShould
{
    private static UpsertQuotaAssignmentRequest BuildRequest(
        string definitionKey = "compute.vms.count",
        string scopeKind = "org",
        Guid? scopeId = null,
        decimal value = 100m,
        string period = "")
    {
        return new UpsertQuotaAssignmentRequest
        {
            DefinitionKey = definitionKey,
            ScopeKind = scopeKind,
            ScopeId = scopeId ?? Guid.NewGuid(),
            Value = value,
            Period = period,
        };
    }

    /// <summary>Given every field valid, when the validator runs, then
    /// no errors are reported.</summary>
    [Fact(DisplayName = "Given all valid fields, when ValidateAsync runs, then no errors are reported")]
    public async Task Validate_WithAllValidFields_PassesAsync()
    {
        var validator = new UpsertQuotaAssignmentRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest());

        result.IsValid.ShouldBeTrue();
    }

    /// <summary>Given an empty <c>DefinitionKey</c>, when the validator
    /// runs, then the <c>DefinitionKey</c> rule fails.</summary>
    [Fact(DisplayName = "Given empty DefinitionKey, when ValidateAsync runs, then DefinitionKey rule fails")]
    public async Task Validate_WithEmptyDefinitionKey_ReturnsErrorAsync()
    {
        var validator = new UpsertQuotaAssignmentRequestValidator();
        var request = BuildRequest(definitionKey: string.Empty);

        var result = await validator.TestValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.DefinitionKey);
    }

    /// <summary>Given a zero <c>Value</c>, when the validator runs, then
    /// the <c>Value</c> rule fails.</summary>
    [Fact(DisplayName = "Given zero Value, when ValidateAsync runs, then Value rule fails")]
    public async Task Validate_WithZeroValue_ReturnsErrorAsync()
    {
        var validator = new UpsertQuotaAssignmentRequestValidator();
        var request = BuildRequest(value: 0m);

        var result = await validator.TestValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.Value);
    }

    /// <summary>Given an empty <c>ScopeId</c> (<see cref="Guid.Empty" />),
    /// when the validator runs, then the <c>ScopeId</c> rule fails.</summary>
    [Fact(DisplayName = "Given empty ScopeId, when ValidateAsync runs, then ScopeId rule fails")]
    public async Task Validate_WithEmptyScopeId_ReturnsErrorAsync()
    {
        var validator = new UpsertQuotaAssignmentRequestValidator();
        var request = BuildRequest(scopeId: Guid.Empty);

        var result = await validator.TestValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.ScopeId);
    }

    /// <summary>Given a <c>ScopeKind</c> outside {org, team, folder},
    /// when the validator runs, then the <c>ScopeKind</c> rule fails.</summary>
    [Fact(DisplayName = "Given unknown ScopeKind, when ValidateAsync runs, then ScopeKind rule fails")]
    public async Task Validate_WithUnknownScopeKind_ReturnsErrorAsync()
    {
        var validator = new UpsertQuotaAssignmentRequestValidator();
        var request = BuildRequest(scopeKind: "workspace");

        var result = await validator.TestValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.ScopeKind);
    }

    /// <summary>Given an explicit <c>"None"</c> <c>Period</c>, when the
    /// validator runs, then no <c>Period</c> error fires. The
    /// controller will <c>Enum.Parse</c> the value into
    /// <c>QuotaPeriod.None</c>; this test asserts the validator
    /// doesn't pre-empt that parse by rejecting the string.</summary>
    [Fact(DisplayName = "Given explicit Period 'None', when ValidateAsync runs, then no errors are reported")]
    public async Task Validate_WithExplicitPeriodNone_PassesAsync()
    {
        var validator = new UpsertQuotaAssignmentRequestValidator();
        var request = BuildRequest(period: "None");

        var result = await validator.TestValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    /// <summary>Given an explicit <c>"Foo"</c> <c>Period</c>, when the
    /// validator runs, then the <c>Period</c> rule fails (and the
    /// controller never reaches <c>Enum.Parse</c>).</summary>
    [Fact(DisplayName = "Given explicit Period 'Foo', when ValidateAsync runs, then Period rule fails")]
    public async Task Validate_WithExplicitPeriodFoo_ReturnsErrorAsync()
    {
        var validator = new UpsertQuotaAssignmentRequestValidator();
        var request = BuildRequest(period: "Foo");

        var result = await validator.TestValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.Period);
    }
}
