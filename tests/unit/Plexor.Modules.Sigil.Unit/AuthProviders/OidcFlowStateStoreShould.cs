// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OidcFlowStateStoreShould — exercise the OIDC state store (Phase
// 4.6.3b). Covers:
//   1. Issue → Consume round-trip returns the same context.
//   2. One-shot semantics — second Consume after a successful read
//      returns null.
//   3. Unknown state → null.
//   4. Different states do not interfere.
//   5. Expired state → null (TTL boundary).
// ============================================================================

using Microsoft.Extensions.Caching.Memory;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders;
using Plexor.Shared.Kernel.AuthProviders;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for <see cref="OidcFlowStateStore" />.
/// </summary>
public sealed class OidcFlowStateStoreShould
{
    /// <summary>Given an issued state, when Consume is called for the
    /// first time, then returns the same context.</summary>
    [Fact(DisplayName = "Given an issued state, when Consume is called, then returns the original context")]
    public void Issue_Then_Consume_ReturnsSameContext()
    {
        var store = new OidcFlowStateStore(new MemoryCache(new MemoryCacheOptions()));
        var context = BuildContext();

        store.Issue("state-1", context);

        var consumed = store.Consume("state-1");
        consumed.ShouldBe(context);
    }

    /// <summary>Given a consumed state, when Consume is called a second
    /// time, then returns null (one-shot semantics — RFC 6749 §10.12).</summary>
    [Fact(DisplayName = "Given a consumed state, when Consume is called twice, then the second call returns null")]
    public void Consume_AfterIssued_Twice_SecondCallReturnsNull()
    {
        var store = new OidcFlowStateStore(new MemoryCache(new MemoryCacheOptions()));
        var context = BuildContext();

        store.Issue("state-1", context);

        var first = store.Consume("state-1");
        var second = store.Consume("state-1");

        first.ShouldNotBeNull();
        second.ShouldBeNull();
    }

    /// <summary>Given an unknown state, when Consume is called, then
    /// returns null.</summary>
    [Fact(DisplayName = "Given an unknown state, when Consume is called, then returns null")]
    public void Consume_UnknownState_ReturnsNull()
    {
        var store = new OidcFlowStateStore(new MemoryCache(new MemoryCacheOptions()));

        var consumed = store.Consume("never-issued");

        consumed.ShouldBeNull();
    }

    /// <summary>Given two distinct issued states, when Consume is
    /// called for each, then the right context is returned for the
    /// right state (no cross-talk between keys).</summary>
    [Fact(DisplayName = "Given two distinct issued states, when Consume is called for each, then each returns the right context")]
    public void Issue_DifferentStates_DoNotInterfere()
    {
        var store = new OidcFlowStateStore(new MemoryCache(new MemoryCacheOptions()));
        var first = BuildContext(orgId: Guid.NewGuid(), originalRedirect: "/console/vms/new");
        var second = BuildContext(orgId: Guid.NewGuid(), originalRedirect: "/console/volumes");

        store.Issue("state-a", first);
        store.Issue("state-b", second);

        store.Consume("state-a").ShouldBe(first);
        store.Consume("state-b").ShouldBe(second);
    }

    /// <summary>Given an issued state whose TTL has elapsed, when
    /// Consume is called, then returns null. The TTL is parameterised
    /// via the primary ctor so the test runs in milliseconds rather
    /// than waiting the production 10 minutes.</summary>
    [Fact(DisplayName = "Given an issued state whose TTL has elapsed, when Consume is called, then returns null")]
    public async Task Consume_ExpiredState_ReturnsNullAsync()
    {
        var store = new OidcFlowStateStore(
            new MemoryCache(new MemoryCacheOptions()),
            TimeSpan.FromMilliseconds(50));
        var context = BuildContext();

        store.Issue("state-1", context);

        await Task.Delay(150);

        var consumed = store.Consume("state-1");
        consumed.ShouldBeNull();
    }

    private static OidcFlowContext BuildContext(
        Guid? orgId = null,
        string originalRedirect = "/console")
    {
        return new OidcFlowContext(
            OrgId: orgId ?? Guid.NewGuid(),
            CodeVerifier: "test-code-verifier-43-chars-aaaaaaaaaaaaaaaaa",
            RedirectUri: "https://plexor.example.com/api/v1/auth/oidc/callback",
            OriginalRedirect: originalRedirect);
    }
}