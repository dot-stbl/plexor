// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MtlsAuthMiddlewareShould — exercises the host-side mTLS gate that
// sits in front of the NodeAgent-facing surface (/node-agent/* and
// /api/v1/compute/clusters/{id}/heartbeat).
//
// Each test wires a real DefaultHttpContext + a recording
// RequestDelegate, generates real X.509 certs through X509Authority,
// and verifies the middleware either:
//   - lets the request through and publishes the nodeId claim, or
//   - rejects with 401 + WWW-Authenticate: mTLS.
//
// The verifier is an in-memory ICertificateAuthority that mirrors
// PlexorCertificateIssuer's chain + revoke + CN-prefix checks. No DB,
// no filesystem — pure crypto.
// ============================================================================

using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Plexor.Host.NodeAgent;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Mtls;
using Shouldly;
using Xunit;

namespace Plexor.Host.UnitTests.NodeAgent;

/// <summary>
///     Behavioural tests for <see cref="MtlsAuthMiddleware" />: the
///     five core mTLS outcomes — happy path, missing cert, malformed
///     CN, wrong CA, and revoked cert.
/// </summary>
public sealed class MtlsAuthMiddlewareShould
{
    /// <summary>
    ///     When the client cert is a valid Plexor CA-issued NodeAgent
    ///     cert with a <c>node_&lt;uuid&gt;</c> CN, the middleware must
    ///     pass through and surface the parsed <c>nodeId</c> claim on
    ///     <see cref="HttpContext.User" /> so downstream controllers
    ///     can authorise.
    /// </summary>
    [Fact(DisplayName = "Given a valid Plexor client cert, when the middleware runs, then next is called and HttpContext.User carries the nodeId claim")]
    public async Task ValidClientCertCallsNextAndSetsNodeIdClaimAsync()
    {
        var nodeId = new NodeId(Guid.NewGuid());
        var ca = CreatePlexorCa();
        var authority = new InMemoryCertificateAuthority(ca);
        var cert = authority.IssueClientCert(
            X509Authority.BuildDn($"node_{IdParse.FormattedUuid(nodeId.Value)}"),
            TimeSpan.FromDays(1));
        using (cert)
        {
            var ctx = NewMtlsRequest();
            ctx.Connection.ClientCertificate = cert;
            var recorder = new NextRecorder();

            var middleware = new MtlsAuthMiddleware(recorder.InvokeAsync, authority, NullLogger<MtlsAuthMiddleware>.Instance);

            await middleware.InvokeAsync(ctx);

            recorder.Called.ShouldBeTrue();
            ctx.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
            ctx.User.ShouldNotBeNull();
            ctx.User.Identity?.IsAuthenticated.ShouldBeTrue();
            var claim = ctx.User.FindFirst("nodeId");
            claim.ShouldNotBeNull();
            claim.Value.ShouldBe(nodeId.ToString());
        }
    }

    /// <summary>
    ///     A request that reaches the mTLS-protected path with no
    ///     client cert at all must be rejected with 401 and the
    ///     downstream pipeline must never see it.
    /// </summary>
    [Fact(DisplayName = "Given a request with no client cert, when the middleware runs, then 401 is returned and next is not called")]
    public async Task MissingClientCertReturns401Async()
    {
        var ca = CreatePlexorCa();
        var authority = new InMemoryCertificateAuthority(ca);
        var ctx = NewMtlsRequest();
        var recorder = new NextRecorder();

        var middleware = new MtlsAuthMiddleware(recorder.InvokeAsync, authority, NullLogger<MtlsAuthMiddleware>.Instance);

        await middleware.InvokeAsync(ctx);

        recorder.Called.ShouldBeFalse();
        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        ctx.Response.Headers.WWWAuthenticate.ToString().ShouldBe("mTLS");
    }

    /// <summary>
    ///     A Plexor CA-signed cert whose CN is not a valid Plexor
    ///     <c>node_&lt;32-hex&gt;</c> must be rejected at the
    ///     <see cref="IdParse.ParseNodeId" /> step, with 401 — defence
    ///     against a leaked CA cert whose subject got malformed.
    /// </summary>
    [Fact(DisplayName = "Given a Plexor-signed cert whose CN is not a valid NodeId, when the middleware runs, then 401 is returned")]
    public async Task CertWithInvalidCnFormatReturns401Async()
    {
        var ca = CreatePlexorCa();
        var authority = new InMemoryCertificateAuthority(ca);
        var cert = authority.IssueClientCert(
            X509Authority.BuildDn("node_not-a-valid-uuid-hex"),
            TimeSpan.FromDays(1));
        using (cert)
        {
            var ctx = NewMtlsRequest();
            ctx.Connection.ClientCertificate = cert;
            var recorder = new NextRecorder();

            var middleware = new MtlsAuthMiddleware(recorder.InvokeAsync, authority, NullLogger<MtlsAuthMiddleware>.Instance);

            await middleware.InvokeAsync(ctx);

            recorder.Called.ShouldBeFalse();
            ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        }
    }

    /// <summary>
    ///     A client cert signed by some other CA must fail the chain
    ///     build under our Plexor CA and be rejected with 401 — the
    ///     cert content is irrelevant; only the chain matters.
    /// </summary>
    [Fact(DisplayName = "Given a cert signed by a foreign CA, when the middleware runs, then 401 is returned")]
    public async Task CertSignedByWrongCaReturns401Async()
    {
        var plexorCa = CreatePlexorCa();
        var foreignCa = X509Authority.CreateRoot(
            X509Authority.BuildDn("Foreign Test CA"),
            TimeSpan.FromDays(1));
        using (foreignCa)
        {
            var authority = new InMemoryCertificateAuthority(plexorCa);
            var nodeId = new NodeId(Guid.NewGuid());
            var cert = X509Authority.IssueLeaf(
                X509Authority.BuildDn($"node_{IdParse.FormattedUuid(nodeId.Value)}"),
                foreignCa,
                TimeSpan.FromDays(1),
                X509Authority.LeafKind.Client);
            using (cert)
            {
                var ctx = NewMtlsRequest();
                ctx.Connection.ClientCertificate = cert;
                var recorder = new NextRecorder();

                var middleware = new MtlsAuthMiddleware(recorder.InvokeAsync, authority, NullLogger<MtlsAuthMiddleware>.Instance);

                await middleware.InvokeAsync(ctx);

                recorder.Called.ShouldBeFalse();
                ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
            }
        }
    }

    /// <summary>
    ///     A Plexor CA-signed cert whose serial has been marked
    ///     revoked (cluster deleted, operator manual revoke, etc.)
    ///     must be rejected at the verifier's revocation check before
    ///     the CN is even parsed — 401.
    /// </summary>
    [Fact(DisplayName = "Given a Plexor-signed cert whose serial is revoked, when the middleware runs, then 401 is returned")]
    public async Task CertSignedByPlexorCaButRevokedReturns401Async()
    {
        var ca = CreatePlexorCa();
        var authority = new InMemoryCertificateAuthority(ca);
        var nodeId = new NodeId(Guid.NewGuid());
        var cert = authority.IssueClientCert(
            X509Authority.BuildDn($"node_{IdParse.FormattedUuid(nodeId.Value)}"),
            TimeSpan.FromDays(1));
        using (cert)
        {
            authority.Revoke(cert);

            var ctx = NewMtlsRequest();
            ctx.Connection.ClientCertificate = cert;
            var recorder = new NextRecorder();

            var middleware = new MtlsAuthMiddleware(recorder.InvokeAsync, authority, NullLogger<MtlsAuthMiddleware>.Instance);

            await middleware.InvokeAsync(ctx);

            recorder.Called.ShouldBeFalse();
            ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        }
    }

    private static X509Certificate2 CreatePlexorCa()
    {
        return X509Authority.CreateRoot(
            X509Authority.BuildDn("Plexor Test CA"),
            TimeSpan.FromDays(1));
    }

    private static DefaultHttpContext NewMtlsRequest()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/node-agent/heartbeat";
        return ctx;
    }

    private sealed class NextRecorder
    {
        public bool Called { get; private set; }

        public Task InvokeAsync(HttpContext context)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }
}
