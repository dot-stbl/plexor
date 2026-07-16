// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RevokedCertCache — in-memory cache of revoked X.509 serials.
//
// The per-request cert verification cannot hit the DB on every TLS
// handshake — that's 50+ ms of latency on the auth path. We cache
// the revoked-serial set in memory with a 5-second TTL. A cache miss
// triggers a single SELECT; the result is memoised for the next
// 5 seconds across all callers.
//
// On DELETE cluster / DELETE node, the cascade handler calls
// MarkRevoked(hex) to seed the cache immediately — the next request
// from that node's CN is rejected without waiting for the TTL.
// ============================================================================

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plexor.Shared.Mtls.Persistence;

namespace Plexor.Shared.Mtls;

/// <summary>
///     In-memory cache of revoked cert serials. Backed by
///     <see cref="RevokedCertsDbContext" /> for periodic refresh.
///     Thread-safe for concurrent reads + occasional writes.
/// </summary>
public sealed class RevokedCertCache(
    IServiceProvider services,
    ILogger<RevokedCertCache> logger)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> cache = new(StringComparer.Ordinal);
    private DateTimeOffset cacheLoadedAt = DateTimeOffset.MinValue;
    private readonly TimeSpan cacheTtl = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     True iff the cert serial is in the revoked set. Negative
    ///     cache (within TTL) — a SELECT once per TTL window is enough
    ///     for the hot path.
    /// </summary>
    public bool IsRevoked(string serialHex)
    {
        if (cache.ContainsKey(serialHex))
        {
            return true;
        }

        if (DateTimeOffset.UtcNow - cacheLoadedAt < cacheTtl)
        {
            return false;
        }

        RefreshFromDatabase();
        return cache.ContainsKey(serialHex);
    }

    /// <summary>
    ///     Seed the cache immediately. Called by the cascade-revoke
    ///     path on cluster / node delete so the next request from
    ///     that node is rejected without waiting for the TTL.
    /// </summary>
    public void MarkRevoked(string serialHex)
    {
        cache[serialHex] = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Force the next <see cref="IsRevoked" /> call to refresh
    ///     from the database. Useful in tests + after a manual
    ///     revoke-insert.
    /// </summary>
    public void Invalidate()
    {
        cacheLoadedAt = DateTimeOffset.MinValue;
    }

    private void RefreshFromDatabase()
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RevokedCertsDbContext>();
            var revoked = db.RevokedCerts.Select(static r => r.Serial).ToList();

            cache.Clear();
            foreach (var serial in revoked)
            {
                cache[serial] = DateTimeOffset.UtcNow;
            }
            cacheLoadedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            // If the DB is unreachable, fail open — the cert is
            // accepted. A failing-closed policy here would lock out
            // the whole cluster on a transient DB blip.
            logger.LogWarning(ex, "Failed to refresh revoked-cert cache; allowing cert.");
        }
    }
}