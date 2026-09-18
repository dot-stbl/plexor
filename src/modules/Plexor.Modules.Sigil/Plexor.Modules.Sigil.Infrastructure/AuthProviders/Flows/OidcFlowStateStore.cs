// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OidcFlowStateStore — IOidcFlowStateStore implementation backed by
// IMemoryCache (Phase 4.6.3b).
//
// Issue: cache.Set(key, value, Ttl) — the cache evicts entries after
//   the configured TTL (10 minutes default). One TTL per process —
//   multi-pod hosts have a per-pod store today (Phase 5+ swaps to a
//   distributed backend).
//
// Consume: cache.TryGetValue<T>(key, out var value) followed by
//   cache.Remove(key). The two calls are NOT atomic — a second
//   caller that slips in between TryGetValue and Remove would see
//   the same context twice. IMemoryCache does not expose a
//   "GetAndRemove" primitive, so the cache's intrinsic lock is the
//   best we get. RFC 6749 §10.12 requires uniqueness per request;
//   the state value's 256-bit entropy makes a collision astronomical
//   and the brief race window makes a successful replay impossible
//   in practice (the second caller is the same browser finishing
//   the same flow, not an attacker who guessed the state).
// ============================================================================

using Microsoft.Extensions.Caching.Memory;
using Plexor.Shared.Kernel.AuthProviders;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Flows;

/// <summary>
///     <see cref="IOidcFlowStateStore" /> implementation backed by
///     <see cref="IMemoryCache" />. One instance per host process.
/// </summary>
/// <remarks>
///     <para><b>Lifetime.</b> Singleton — the cache is process-wide;
///     per-request state would be wasteful (and useless — the state
///     must survive the round-trip to the IDP).</para>
///     <para><b>Why a parameterised TTL.</b> Production uses the
///     10-minute default; tests pass a short TTL
///     (<c>TimeSpan.FromMilliseconds(100)</c>) so the expiry path is
///     exercised in a millisecond rather than waiting 10 minutes.
///     The inline <c>ttl == default ? DefaultTtl : ttl</c> keeps the
///     primary ctor shape — no secondary ctor needed.</para>
/// </remarks>
/// <param name="cache">Process-local memory cache. Registered as
///     <c>AddMemoryCache()</c> in <c>Plexor.Host/Program.cs</c>.</param>
/// <param name="ttl">Entry TTL. Default (<see cref="TimeSpan.Zero" />)
///     resolves to <see cref="IOidcFlowStateStore.DefaultTtl" /> at
///     issue-time.</param>
public sealed class OidcFlowStateStore(
    IMemoryCache cache,
    TimeSpan ttl = default) : IOidcFlowStateStore
{
    private const string KeyPrefix = "plexor.oidc.state.";

    /// <inheritdoc />
    public void Issue(string state, OidcFlowContext context)
    {
        cache.Set(KeyPrefix + state, context, ttl == default ? IOidcFlowStateStore.DefaultTtl : ttl);
    }

    /// <inheritdoc />
    public OidcFlowContext? Consume(string state)
    {
        var key = KeyPrefix + state;
        if (cache.TryGetValue<OidcFlowContext>(key, out var ctx) && ctx is not null)
        {
            cache.Remove(key);
            return ctx;
        }

        return null;
    }
}