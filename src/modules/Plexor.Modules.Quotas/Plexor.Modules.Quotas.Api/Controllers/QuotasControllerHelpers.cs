// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotasControllerHelpers — file-static helpers shared between
// QuotasController.cs (read surface) and QuotaAssignmentsController.cs
// (write surface). Pulled out to satisfy the no-private-methods
// convention (class-layout-and-tooling.md §1a / §9.4 — Controller /
// minimal API endpoint). Each controller is a thin orchestration
// layer; the scope parsing, mapping, and ProblemDetails construction
// live here.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Quotas.Api.Models;
using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Api.Controllers;

/// <summary>
///     Helpers shared by <see cref="QuotasController" /> and
///     <see cref="QuotaAssignmentsController" />. Each public method
///     on those controllers is a one-line orchestration call into this
///     file; the per-method logic lives here.
/// </summary>
internal static class QuotasControllerHelpers
{
    /// <summary>
    ///     Parse the <c>?scope=</c> query value into the matching
    ///     <see cref="QuotaScopeKind" />. Returns <see langword="false" />
    ///     when the value is not one of <c>"org"</c>, <c>"team"</c>,
    ///     or <c>"folder"</c> (case-insensitive).
    /// </summary>
    /// <param name="scope">Raw query value supplied by the caller.</param>
    /// <param name="kind">Resolved scope kind when the method returns
    /// <see langword="true" />; <c>default</c> otherwise.</param>
    /// <returns><see langword="true" /> when the value matched a
    /// known scope kind.</returns>
    public static bool TryParseScope(string scope, out QuotaScopeKind kind)
    {
        kind = scope.ToLowerInvariant() switch
        {
            "org" => QuotaScopeKind.Org,
            "team" => QuotaScopeKind.Team,
            "folder" => QuotaScopeKind.Folder,
            _ => default,
        };
        return kind != default;
    }

    /// <summary>
    ///     400 ProblemDetails for the case where <see cref="TryParseScope" />
    ///     rejects the input. Caller surfaces this verbatim; the helper
    ///     is kept here so the error shape stays consistent across the
    ///     three endpoints that take <c>?scope=</c>.
    /// </summary>
    /// <param name="scope">Raw query value the controller received.</param>
    /// <returns>A typed <see cref="BadRequestObjectResult" /> wrapping
    /// the ProblemDetails.</returns>
    public static BadRequestObjectResult InvalidScopeProblem(string scope)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid scope",
            Detail = $"Scope must be 'org', 'team', or 'folder'; got '{scope}'.",
        };
        return new BadRequestObjectResult(problem);
    }

    /// <summary>
    ///     400 <see cref="ValidationProblemDetails" /> for the PUT
    ///     endpoint when the FluentValidation chain rejects the body.
    ///     The dictionary comes from
    ///     <c>ValidationResult.ToDictionary()</c> (property-name →
    ///     error messages); ASP.NET Core binds it into the standard
    ///     <c>errors</c> shape per RFC 9457.
    /// </summary>
    /// <param name="errors">Dictionary keyed by property name,
    /// value = the error message(s) from the validator (one per
    /// failure on that property).</param>
    /// <returns>A typed <see cref="BadRequestObjectResult" /> wrapping
    /// the <see cref="ValidationProblemDetails" />.</returns>
    public static BadRequestObjectResult InvalidRequestResponse(
        IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more request fields failed validation.",
        };
        return new BadRequestObjectResult(problem);
    }

    /// <summary>
    ///     Build a <c>definition id → key</c> map for the catalog. The
    ///     map is consumed by the <c>MapToSummary(QuotaAssignment,
    ///     IReadOnlyDictionary&lt;Guid, string&gt;)</c> overload and
    ///     by <see cref="BuildUsageEntriesAsync" /> to denormalise the
    ///     stable catalog key into the response shape without a join.
    /// </summary>
    /// <param name="definitions">Catalog rows from
    /// <see cref="IQuotaCatalog.ListAllAsync" />.</param>
    /// <returns>Dictionary keyed by <see cref="QuotaDefinition.Id" />,
    /// value = the catalog key string.</returns>
    public static IReadOnlyDictionary<Guid, string> BuildDefinitionKeyMap(
        IReadOnlyList<QuotaDefinition> definitions)
    {
        return definitions.ToDictionary(
            static definition => definition.Id,
            static definition => definition.Key);
    }

    /// <summary>
    ///     Map one <see cref="QuotaAssignment" /> row to its
    ///     <see cref="QuotaAssignmentSummary" /> projection, resolving
    ///     the catalog key from the supplied <paramref name="keyById" />
    ///     map. Rows whose definition is missing from the catalog are
    ///     emitted with an empty <c>DefinitionKey</c> — defensive
    ///     against the FK being intact while the catalog row is in the
    ///     process of being removed (shouldn't happen but the shape
    ///     stays valid).
    /// </summary>
    /// <param name="row">EF-tracked (read-only after read) assignment row.</param>
    /// <param name="keyById">Catalog id → key map.</param>
    /// <returns>The projection.</returns>
    public static QuotaAssignmentSummary MapToSummary(
        QuotaAssignment row,
        IReadOnlyDictionary<Guid, string> keyById)
    {
        return new QuotaAssignmentSummary
        {
            Id = row.Id,
            DefinitionId = row.DefinitionId,
            DefinitionKey = keyById.GetValueOrDefault(row.DefinitionId, string.Empty),
            ScopeKind = row.ScopeKind.ToString(),
            ScopeId = row.ScopeId,
            OrgId = row.OrgId,
            Value = row.Value,
            Period = row.Period.ToString(),
            CreatedBy = row.CreatedBy,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
    }

    /// <summary>
    ///     Map one <see cref="QuotaAssignment" /> row to its
    ///     <see cref="QuotaAssignmentSummary" /> projection using the
    ///     already-resolved <paramref name="definition" />. Used by the
    ///     4.5.g.3 PUT path — the controller fetches the
    ///     <c>QuotaDefinition</c> via <c>IQuotaCatalog.FindByKeyAsync</c>
    ///     to validate the request, so it has the row in hand at
    ///     upsert time and a second <c>ListAllAsync</c> round-trip
    ///     would be redundant. Rows whose definition is
    ///     <see langword="null" /> fall back to an empty
    ///     <c>DefinitionKey</c> for parity with the list-path
    ///     overload.
    /// </summary>
    /// <param name="row">Persisted <see cref="QuotaAssignment" />
    /// returned by <c>IQuotaAssignmentRepository.UpsertAsync</c>.</param>
    /// <param name="definition">Resolved catalog row, or
    /// <see langword="null" /> when the FK is intact but the catalog
    /// row is missing.</param>
    /// <returns>The projection.</returns>
    public static QuotaAssignmentSummary MapToSummary(
        QuotaAssignment row,
        QuotaDefinition? definition)
    {
        return new QuotaAssignmentSummary
        {
            Id = row.Id,
            DefinitionId = row.DefinitionId,
            DefinitionKey = definition?.Key ?? string.Empty,
            ScopeKind = row.ScopeKind.ToString(),
            ScopeId = row.ScopeId,
            OrgId = row.OrgId,
            Value = row.Value,
            Period = row.Period.ToString(),
            CreatedBy = row.CreatedBy,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
    }

    /// <summary>
    ///     Build the per-usage-row response: pair each
    ///     <see cref="QuotaUsage" /> snapshot with the resolved
    ///     effective limit and a threshold percentage when usage is at
    ///     or above the 80% warning line. Rows whose catalog key is
    ///     missing are silently skipped (defensive — should not happen
    ///     given the FK).
    /// </summary>
    /// <param name="rows">Snapshots from <see cref="IQuotaUsageReader" />.</param>
    /// <param name="keyById">Catalog id → key map.</param>
    /// <param name="scope">Scope the controller asked about.</param>
    /// <param name="resolver">Scope walker — resolves the effective
    /// limit per row.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The flattened projection list.</returns>
    public static async Task<IReadOnlyList<QuotaUsageEntry>> BuildUsageEntriesAsync(
        IReadOnlyList<QuotaUsage> rows,
        IReadOnlyDictionary<Guid, string> keyById,
        QuotaScope scope,
        IQuotaScopeResolver resolver,
        CancellationToken cancellationToken)
    {
        var result = new List<QuotaUsageEntry>(rows.Count);

        foreach (var row in rows)
        {
            if (!keyById.TryGetValue(row.DefinitionId, out var key))
            {
                continue;
            }

            decimal? effectiveLimit = null;
            decimal? thresholdPct = null;

            var effective = await resolver.ResolveAsync(
                scope,
                new QuotaDefinitionKey(key),
                cancellationToken);

            if (effective is { } resolved)
            {
                effectiveLimit = resolved.Value;
                // 80% warning threshold; same as the enforcer's
                // AllowedWithWarning signal (4.5.b).
                const decimal WarningThresholdPct = 0.8m;
                if (resolved.Value > 0m)
                {
                    var warnAt = resolved.Value * WarningThresholdPct;
                    if (row.CurrentValue >= warnAt)
                    {
                        thresholdPct = decimal.Round(
                            row.CurrentValue / resolved.Value * 100m,
                            decimals: 1);
                    }
                }
            }

            result.Add(new QuotaUsageEntry
            {
                DefinitionKey = key,
                Used = row.CurrentValue,
                EffectiveLimit = effectiveLimit,
                ThresholdPct = thresholdPct,
            });
        }

        return result;
    }

    /// <summary>
    ///     Build the per-definition effective response: one row per
    ///     catalog entry holding the resolved value + origin. The
    ///     origin serialisation follows the same vocabulary as the
    ///     <c>EffectiveQuotaEntry.Origin</c> XML doc: <c>"Folder"</c>,
    ///     <c>"Team"</c>, <c>"Org"</c>, <c>"Definition"</c> (catalog
    ///     default), or <c>"Unlimited"</c>.
    /// </summary>
    /// <param name="definitions">Catalog rows.</param>
    /// <param name="scope">Scope the controller asked about.</param>
    /// <param name="resolver">Scope walker.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The flattened projection list, in catalog key order.</returns>
    public static async Task<IReadOnlyList<EffectiveQuotaEntry>> BuildEffectiveEntriesAsync(
        IReadOnlyList<QuotaDefinition> definitions,
        QuotaScope scope,
        IQuotaScopeResolver resolver,
        CancellationToken cancellationToken)
    {
        var entries = new List<EffectiveQuotaEntry>(definitions.Count);

        foreach (var definition in definitions)
        {
            var effective = await resolver.ResolveAsync(
                scope,
                new QuotaDefinitionKey(definition.Key),
                cancellationToken);

            var originString = effective is null
                ? "Unlimited"
                : effective.Origin?.ToString() ?? "Definition";

            entries.Add(new EffectiveQuotaEntry
            {
                DefinitionKey = definition.Key,
                Value = effective?.Value ?? 0m,
                Origin = originString,
            });
        }

        return entries;
    }
}
