// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RevokedCertCache unit tests — the in-memory revoked-cert set that
// fronts every TLS-handshake cert verification. The whole point is to
// keep a single SELECT per TTL window out of the per-request auth path;
// a bug here either re-introduces 50+ ms of DB latency on every node
// call or silently lets revoked certs through after a TTL window.
//
// TimeProvider is injected so the TTL window is deterministically
// advanceable — no real-time sleeps. The test ships a tiny
// FakeTimeProvider inline (no NSubstitute dependency needed for these
// three tests).
// ============================================================================

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Plexor.Shared.Mtls.Unit;

/// <summary>
///     Unit tests for <see cref="RevokedCertCache" /> — the negative +
///     positive cache that fronts cert revocation checks. Locks down
///     mark/unmark semantics and the injected-clock contract that the
///     TTL window depends on.
/// </summary>
public sealed class RevokedCertCacheShould
{
    /// <summary>
    ///     MarkRevoked must seed the cache immediately so the cascade
    ///     revoke path can reject the next request from that node's
    ///     CN without waiting for the TTL refresh.
    /// </summary>
    [Fact(DisplayName = "Given a freshly marked serial, when IsRevoked is called, then returns true")]
    public void MarkedSerialIsReportedRevoked()
    {
        var clock = new FakeTimeProvider();
        var cache = RevokedCacheTestHelpers.CreateCache(clock);

        cache.MarkRevoked("ABCD1234");

        cache.IsRevoked("ABCD1234").ShouldBeTrue(
            "MarkRevoked must seed the cache immediately — the cascade " +
            "revoke path depends on the next request from that node's " +
            "CN being rejected without waiting for the TTL.");
    }

    /// <summary>
    ///     Unmarked serials must report not-revoked — false positives
    ///     would lock every node out of the cluster on every restart
    ///     before the cache primes from the DB.
    /// </summary>
    [Fact(DisplayName = "Given a serial that was never marked, when IsRevoked is called, then returns false")]
    public void UnmarkedSerialIsNotReportedRevoked()
    {
        var clock = new FakeTimeProvider();
        var cache = RevokedCacheTestHelpers.CreateCache(clock);

        cache.IsRevoked("NEVER_MARKED").ShouldBeFalse(
            "unmarked serials must report not-revoked — false positives " +
            "would lock every node out of the cluster on every restart " +
            "before the cache primes from the DB.");
    }

    /// <summary>
    ///     The injected <see cref="TimeProvider" /> must drive the
    ///     MarkRevoked timestamp — DateTimeOffset.UtcNow in
    ///     production code would defeat every test and break TTL
    ///     semantics under a frozen clock.
    /// </summary>
    [Fact(DisplayName = "Given the clock advances before read, the stored timestamp reflects the clock at MarkRevoked (not after)")]
    public void MarkRevokedUsesInjectedClock()
    {
        var clock = new FakeTimeProvider();
        var cache = RevokedCacheTestHelpers.CreateCache(clock);

        var t0 = clock.GetUtcNow();
        cache.MarkRevoked("CLOCK_TEST");

        // Advance AFTER the mark — the stored timestamp must reflect
        // the moment of MarkRevoked. If production code reached
        // DateTimeOffset.UtcNow instead of timeProvider.GetUtcNow(),
        // this assertion would observe the post-advance wall clock.
        clock.Advance(TimeSpan.FromHours(1));
        var t1 = clock.GetUtcNow();

        var storedAt = RevokedCacheTestHelpers.GetStoredTimestamp(cache, "CLOCK_TEST");
        storedAt.ShouldBe(t0);
        storedAt.ShouldNotBe(t1);
    }
}

/// <summary>
///     File-local setup helpers for the RevokedCertCache tests —
///     cache construction (empty service provider → DB refresh fails
///     silently, IsRevoked returns false) and reflection-based reads
///     of the cache's internal state. File-scoped so it cannot leak
///     to other test files via a shared <c>Helpers/</c> folder.
/// </summary>
file static class RevokedCacheTestHelpers
{
    /// <summary>
    ///     Builds a cache with the given <see cref="TimeProvider" />
    ///     and an empty service provider. The DB lookup inside
    ///     RefreshFromDatabase throws (no RevokedCertsDbContext
    ///     registered), the catch swallows it, and IsRevoked returns
    ///     false — the fail-open path the production code already
    ///     documents.
    /// </summary>
    /// <param name="clock"></param>
    public static RevokedCertCache CreateCache(TimeProvider clock)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new RevokedCertCache(
            clock,
            services,
            NullLogger<RevokedCertCache>.Instance);
    }

    /// <summary>
    ///     Reads a marked serial's timestamp from the cache's
    ///     internal dictionary via reflection so we can assert that
    ///     the injected clock drove the MarkRevoked timestamp. The
    ///     production surface exposes only IsRevoked / MarkRevoked /
    ///     Invalidate, not the timestamp itself.
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="serialHex"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static DateTimeOffset GetStoredTimestamp(RevokedCertCache cache, string serialHex)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var field = typeof(RevokedCertCache).GetField("cache", flags)
            ?? throw new InvalidOperationException(
                "RevokedCertCache.cache field not found — " +
                "did the production field name change?");

        var dict = (ConcurrentDictionary<string, DateTimeOffset>)field.GetValue(cache)!;
        return dict[serialHex];
    }
}

/// <summary>
///     Mutable clock for advancing the cache's TTL window in tests.
///     Lives next to the test class — one file, no production
///     dependency, no NSubstitute overhead.
/// </summary>
file sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset current = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return current;
    }

    public void Advance(TimeSpan delta)
    {
        current = current.Add(delta);
    }
}
