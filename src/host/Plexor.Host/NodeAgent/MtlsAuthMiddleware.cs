// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MtlsAuthMiddleware — runs after Kestrel has accepted the client cert
// on the TLS handshake. Verifies the cert chain under the Plexor CA,
// checks the revoked-certs cache, parses the CN into a strongly-typed
// NodeId claim, and propagates it via HttpContext.User so downstream
// controllers can [Authorize] against the "mTLS-NodeAgent" policy.
//
// Failure modes:
//   - Missing client cert        → 401 (WWW-Authenticate: mTLS)
//   - Bad chain / wrong CA        → 401
//   - Revoked serial              → 401
//   - CN not prefixed "node_"     → 401
// ============================================================================

using System.Security.Claims;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Mtls;

namespace Plexor.Host.NodeAgent;

/// <summary>
///     ASP.NET Core middleware that enforces mTLS auth on the
///     NodeAgent-facing surface. Pulled into the pipeline via
///     <c>app.UseMiddleware&lt;MtlsAuthMiddleware&gt;()</c> before
///     the controllers route map.
/// </summary>
public sealed class MtlsAuthMiddleware(
    RequestDelegate next,
    ICertificateAuthority caAuthority,
    ILogger<MtlsAuthMiddleware> logger)
{
    private const string NodeAgentPolicy = "mTLS-NodeAgent";

    /// <summary>
    ///     Sub-prefix within /api/v1/compute/clusters/ that is BROWSER-
    ///     facing (cluster CRUD) — those routes use JWT auth and must
    ///     skip mTLS.
    /// </summary>
    private const string BrowserClusterCrudPath = "/api/v1/compute/clusters/";

    /// <summary>
    ///     Inspects the request's client cert (mTLS handshake already
    ///     done by Kestrel). Rejects requests with no cert, bad chain,
    ///     revoked serial, or non-Plexor CN. On success, sets
    ///     <c>HttpContext.User</c> to a principal carrying the
    ///     <c>nodeId</c> claim parsed off the cert subject.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!RequiresMtls(path))
        {
            await next(context);
            return;
        }

        if (await context.Connection.GetClientCertificateAsync(context.RequestAborted) is not { } cert)
        {
            await RejectMtlsAsync(context, "client certificate required", context.RequestAborted);
            return;
        }

        if (!caAuthority.VerifyClientCert(cert))
        {
            await RejectMtlsAsync(context, "client certificate rejected (chain, revocation, or CN)", context.RequestAborted);
            return;
        }

        var cn = X509Authority.ExtractCommonName(cert.Subject);
        NodeId nodeId;
        try
        {
            nodeId = IdParse.ParseNodeId(cn);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Client cert CN '{Cn}' is not a valid Plexor NodeId.", cn);
            await RejectMtlsAsync(context, "CN is not a Plexor NodeId", context.RequestAborted);
            return;
        }

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("nodeId", nodeId.ToString()),
                new Claim(ClaimTypes.Role, "node"),
            },
            authenticationType: NodeAgentPolicy);

        context.User = new ClaimsPrincipal(identity);
        await next(context);
    }

    private static bool RequiresMtls(string path)
    {
        // /node-agent/* is always mTLS-only.
        if (path.StartsWith("/node-agent/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // /api/v1/compute/clusters/{anything}/heartbeat is the
        // NodeAgent-facing call. Other cluster routes are browser-
        // facing (JWT).
        if (path.StartsWith(BrowserClusterCrudPath, StringComparison.OrdinalIgnoreCase))
        {
            return path.Contains("/heartbeat", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static async Task RejectMtlsAsync(
        HttpContext context,
        string reason,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = "mTLS";
        await context.Response.WriteAsync(reason, cancellationToken);
    }
}