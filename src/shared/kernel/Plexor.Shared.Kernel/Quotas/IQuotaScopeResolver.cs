// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IQuotaScopeResolver — walks the Realm hierarchy to resolve the
// effective value for a (scope, definition_key) pair. The walker is
// folder → team → org → default; first non-null hit wins (the spec
// "minimum-of-found" wording is implemented as "first hit" because the
// walker is monotonic — a folder binding is intentionally tighter than
// the org's binding, etc.).
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Resolves the effective limit for a (scope, definition_key) pair
///     by walking the Realm hierarchy: folder → team → org → default.
/// </summary>
/// <remarks>
///     <para><b>Where used.</b> The <c>IQuotaEnforcer</c> implementation
///     delegates the limit lookup to this interface so the walker is
///     testable in isolation and so the 4.5.g REST
///     <c>GET /quotas/effective</c> endpoint can surface the same
///     resolved value without going through the enforcer's reserve path.</para>
///     <para><b>Walk order.</b> Folder first (most specific), then team
///     (only when <see cref="QuotaScope.Kind" /> = <see cref="QuotaScopeKind.Folder" />
///     and a parent team is known), then org, then the catalog
///     <c>DefaultValue</c>. The walker is read-only — it never writes to
///     <c>quota_assignments</c> or <c>quota_usage</c>.</para>
/// </remarks>
public interface IQuotaScopeResolver
{
    /// <summary>
    ///     Resolve the effective limit for <paramref name="scope" /> and
    ///     <paramref name="definitionKey" /> by walking folder → team →
    ///     org → default.
    /// </summary>
    /// <param name="scope">Polymorphic scope the resolver walks from.</param>
    /// <param name="definitionKey">Stable catalog key.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    ///     The effective value and the scope kind that supplied it. Returns
    ///     <see langword="null" /> when no assignment exists at any level
    ///     <em>and</em> the catalog row has no <c>DefaultValue</c> — the
    ///     enforcer interprets that as "unlimited" and skips the reservation.
    /// </returns>
    public Task<EffectiveQuota?> ResolveAsync(
        QuotaScope scope,
        QuotaDefinitionKey definitionKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Pair of (effective value, scope kind that supplied it). The origin
///     is denormalized into the result so audit entries can cite the
///     source of the limit (e.g. <c>"Org, 100"</c> means the org-level
///     assignment of 100 won the walk).
/// </summary>
/// <param name="Value">The resolved limit value (always &gt; 0).</param>
/// <param name="Origin">
///     Which scope kind won the walk (<see cref="QuotaScopeKind.Folder" />,
///     <see cref="QuotaScopeKind.Team" />, or <see cref="QuotaScopeKind.Org" />)
///     or <see langword="null" /> when the catalog's built-in
///     <c>DefaultValue</c> was used. v1 emits
///     <see cref="QuotaScopeKind.Org" />, <see cref="QuotaScopeKind.Folder" />,
///     or <see langword="null" />; <see cref="QuotaScopeKind.Team" /> lands in
///     Phase 2 when the folder/team scope walker carries the team hint.
/// </param>
public sealed record EffectiveQuota(decimal Value, QuotaScopeKind? Origin);
