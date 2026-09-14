// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IAuthProviderResolver — the dispatch boundary for the dual auth-provider
// layer (Plexor Sigil + external OIDC, Phase 4.6.2c). Picks the right
// IAuthProvider implementation for a raw bearer credential and caches
// the (issuer → provider) mapping so per-request work stays in memory.
//
// Why a resolver, not an `IEnumerable<IAuthProvider>`:
//
//   - ASP.NET's `services.AddScoped<IAuthProvider, X>()` is a last-wins
//     binding — multiple registrations against the same interface collapse
//     to the last registration. Resolving via a typed factory (the
//     resolver receives both concrete providers via primary ctor)
//     side-steps the issue.
//   - The cache layer lives with the resolver, not the providers — the
//     providers stay stateless orchestrators; cache invalidation is the
//     resolver's responsibility.
//   - The bearer handler calls exactly one method, gets back exactly one
//     `AuthResolution` or `null`. No enumeration + iteration; the
//     resolver is the single dispatch point.
//
// Returns `null` for unknown issuers / malformed credentials / tenant
// misconfigured. Does NOT throw on auth failure — the bearer handler
// maps `null` to a 401.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.AuthProviders;

/// <summary>
///     Dispatches a raw bearer credential to the right
///     <see cref="IAuthProvider" /> based on the credential's signature
///     (Sigil JWT vs OIDC JWT) or its <c>iss</c> claim. Caches the
///     provider lookup so per-request work stays in-memory after the
///     first dispatch.
/// </summary>
public interface IAuthProviderResolver
{
    /// <summary>
    ///     Resolve a credential. Returns <c>null</c> when no provider
    ///     can handle it (unknown issuer, malformed credential, tenant
    ///     misconfigured). Does NOT throw — failures translate to a
    ///     <c>null</c> return + structured log.
    /// </summary>
    /// <param name="rawCredential">
    ///     The compact credential extracted from the
    ///     <c>Authorization: Bearer</c> header (already stripped of the
    ///     <c>Bearer </c> prefix).
    /// </param>
    /// <param name="cancellationToken">
    ///     Forwarded to the underlying provider's
    ///     <see cref="IAuthProvider.ResolveAsync" /> call.
    /// </param>
    /// <returns>
    ///     A populated <see cref="AuthResolution" /> when one of the
    ///     registered providers accepts the credential; <c>null</c>
    ///     otherwise. The bearer handler maps <c>null</c> to a 401.
    /// </returns>
    public Task<AuthResolution?> ResolveAsync(
        string rawCredential,
        CancellationToken cancellationToken = default);
}
