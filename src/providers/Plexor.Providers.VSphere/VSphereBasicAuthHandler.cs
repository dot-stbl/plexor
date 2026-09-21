// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereBasicAuthHandler — DelegatingHandler that injects HTTP Basic
// credentials into every outbound vCenter call. Reads the credentials
// from IOptionsMonitor<VSphereOptions> so an env-var rotation takes
// effect on the next request without a host restart (mirrors the
// bearer-token rotation pattern in http-resilience-refit.md §2).
//
// The handler is intentionally minimal — Refit already attaches the
// base address + the JSON content type; this only adds the
// Authorization header. The Polly resilience handler sits after this
// one in the chain (refit adds it via AddStandardResilienceHandler).
// ============================================================================

using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace Plexor.Providers.VSphere;

/// <summary>
///     <see cref="DelegatingHandler" /> that signs every vCenter
///     request with HTTP Basic credentials sourced from
///     <see cref="VSphereOptions" />. Rotating
///     <c>PLX_PROVIDERS_VSPHERE_PASSWORD</c> at deploy time takes
///     effect on the next outbound call (the monitor reads current
///     values; no host restart required).
/// </summary>
/// <remarks>
///     Construct the handler with the options monitor.
/// </remarks>
/// <param name="options">Live view of the bound
/// <see cref="VSphereOptions" />.</param>
public sealed class VSphereBasicAuthHandler(IOptionsMonitor<VSphereOptions> options) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{current.Username}:{current.Password}")));

        return base.SendAsync(request, cancellationToken);
    }
}
