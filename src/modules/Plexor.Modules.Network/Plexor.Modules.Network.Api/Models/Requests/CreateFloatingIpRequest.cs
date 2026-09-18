// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateFloatingIpRequest — wire shape for POST
// /api/v1/network/floating-ips.
// ============================================================================

namespace Plexor.Modules.Network.Api.Models.Requests;

/// <summary>
///     Wire shape for the create-floating-ip request body. ClusterId +
///     Address are required; Status defaults to Pending.
/// </summary>
public sealed class CreateFloatingIpRequest
{
    /// <summary>Target cluster id. The API layer validates the
    /// cluster exists before INSERT — the network module does not FK
    /// into Plexor.Modules.Clusters.</summary>
    public Guid ClusterId { get; init; }

    /// <summary>IP address (IPv4 or IPv6). Validated by
    /// <see cref="Plexor.Modules.Network.Api.Validation.CreateFloatingIpRequestValidator" />
    /// (must parse as <see cref="System.Net.IPAddress" />).</summary>
    public string Address { get; init; } = string.Empty;
}
