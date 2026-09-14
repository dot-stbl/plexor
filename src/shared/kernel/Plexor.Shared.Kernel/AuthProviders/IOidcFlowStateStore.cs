// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IOidcFlowStateStore — server-side state for the OIDC
// authorization-code + PKCE flow (RFC 6749 + RFC 7636). Phase 4.6.3b.
//
// On the authorize leg the endpoint mints:
//   - a fresh PKCE pair (code_verifier + code_challenge, see
//     IPkceGenerator), and
//   - a cryptographically-random opaque "state" token (RFC 6749 §10.12 —
//     CSRF protection).
//
// Both halves travel together to the callback leg:
//   - state → returned by the IDP in the callback redirect → looked up
//     server-side to recover the PKCE verifier + redirect metadata.
//   - code_verifier → POSTed to the IDP's token endpoint as proof that
//     the caller that started the flow is the same caller finishing it.
//
// The state bundle lives ~10 minutes in an in-memory store keyed by the
// state value. Consume is one-shot — replay protection per RFC 6749
// §10.12 ("ensure that the value of the 'state' parameter is unique
// per request"). v1 keeps the store process-local; a multi-pod deploy
// needs a shared store (Phase 5+).
// ============================================================================

namespace Plexor.Shared.Kernel.AuthProviders;

/// <summary>
///     Server-side state for the OIDC authorization-code + PKCE flow.
///     Maps <c>state</c> → <see cref="OidcFlowContext" /> for the
///     lifetime of one outbound authorization request.
/// </summary>
/// <remarks>
///     <para><b>One-shot semantics.</b> <see cref="Consume" /> removes
///     the entry on a successful read so the same <c>state</c> cannot
///     be replayed (RFC 6749 §10.12). Unknown / expired / already-
///     consumed states all return <c>null</c> — the discriminator
///     (unknown vs expired vs replay) is in the structured log line
///     for diagnostics.</para>
///     <para><b>Lifetime.</b> Entries auto-expire after
///     <see cref="DefaultTtl" /> (10 minutes). Long enough for a
///     human-driven login; short enough that abandoned flows don't
///     accumulate state indefinitely.</para>
///     <para><b>Process-local.</b> v1 ships an in-memory store;
///     a multi-pod host has a per-pod store and a state issued on pod
///     A cannot be consumed on pod B. Acceptable for v0.1; Phase 5+
///     swaps to a Redis-backed distributed store.</para>
/// </remarks>
public interface IOidcFlowStateStore
{
    /// <summary>
    ///     Default TTL for issued <see cref="OidcFlowContext" />
    ///     entries. Long enough for a human-driven IDP redirect +
    ///     login form; short enough that abandoned flows don't
    ///     accumulate state. Tests construct the implementation
    ///     with a shorter TTL to exercise the expiry path.
    /// </summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     Persist a state value → context pair. Auto-expires after
    ///     <see cref="DefaultTtl" />. Calling <see cref="Issue" />
    ///     twice with the same <paramref name="state" /> overwrites
    ///     the prior entry — the state value itself is
    ///     cryptographically random so collisions are vanishingly
    ///     unlikely (256 bits of entropy).
    /// </summary>
    /// <param name="state">Opaque random token sent on the authorize
    /// redirect and echoed back by the IDP. 43-char URL-safe base64
    /// (256 bits of entropy) per RFC 6749 §10.12.</param>
    /// <param name="context">The PKCE verifier + redirect metadata
    /// the callback leg needs to finish the flow.</param>
    public void Issue(string state, OidcFlowContext context);

    /// <summary>
    ///     Look up <paramref name="state" /> and remove the entry in
    ///     one atomic step. Returns <c>null</c> when the state is
    ///     unknown, expired, or already consumed (the three are
    ///     indistinguishable from the caller's view; the structured
    ///     log line carries the discriminator).
    /// </summary>
    /// <param name="state">State value from the IDP's callback
    /// redirect's <c>state</c> query parameter.</param>
    /// <returns>The previously-issued context on success;
    /// <c>null</c> on every failure path.</returns>
    public OidcFlowContext? Consume(string state);
}

/// <summary>
///     Bundle carried by the OIDC flow's <c>state</c> parameter
///     between the authorize and callback legs.
/// </summary>
/// <param name="OrgId">
///     Tenant the flow was started for. The callback leg uses this to
///     look up the per-tenant <c>OrgAuthProviderConfig</c> via
///     <c>IOrgAuthProviderConfigReader.GetForOrgAsync</c> when
///     exchanging the code for tokens.
/// </param>
/// <param name="CodeVerifier">
///     PKCE <c>code_verifier</c> (RFC 7636 §4.1) minted on the
///     authorize leg. Sent in the token-exchange POST as proof that
///     the caller that started the flow is the same caller finishing
///     it (RFC 7636 §4.6).
/// </param>
/// <param name="RedirectUri">
///     The Plexor-side callback URL registered at the IDP — must
///     match the authorize leg's <c>redirect_uri</c> exactly
///     (RFC 6749 §4.1.3, §10.6). The callback leg passes this to
///     <c>IOidcTokenClient.ExchangeCodeAsync</c>.
/// </param>
/// <param name="OriginalRedirect">
///     The operator's deep-link that initiated the OIDC flow
///     (e.g. <c>/console/vms/new</c>). The callback leg redirects
///     here — with the minted Plexor access token attached — once
///     the IDP code exchange + user provisioning succeed.
/// </param>
public sealed record OidcFlowContext(
    Guid OrgId,
    string CodeVerifier,
    string RedirectUri,
    string OriginalRedirect);