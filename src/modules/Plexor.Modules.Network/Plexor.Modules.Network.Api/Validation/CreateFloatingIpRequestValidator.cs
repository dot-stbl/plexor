// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateFloatingIpRequestValidator — FluentValidation chain for the
// create-floating-ip body. Runs in the endpoint via [FromServices]
// before any service call; failures surface as 400
// ValidationProblemDetails.
// ============================================================================

using System.Net;
using FluentValidation;
using Plexor.Modules.Network.Api.Models.Requests;

namespace Plexor.Modules.Network.Api.Validation;

/// <summary>
///     Validates the create-floating-ip request body. Rules:
///     <list type="bullet">
///         <item><see cref="CreateFloatingIpRequest.ClusterId" /> is
///         non-empty (an empty GUID would route the row to a
///         non-existent cluster; the API layer validates the cluster
///         exists at write time).</item>
///         <item><see cref="CreateFloatingIpRequest.Address" /> parses
///         as a <see cref="System.Net.IPAddress" />. IPv4 + IPv6 are
///         both accepted.</item>
///     </list>
/// </summary>
public sealed class CreateFloatingIpRequestValidator
    : AbstractValidator<CreateFloatingIpRequest>
{
    /// <summary>Max address length — 45 chars (matches the column
    /// max length and covers the longest legal textual IPv6
    /// including zone identifiers).</summary>
    public const int AddressMaxLength = 45;

    /// <summary>
    ///     Construct the validator with the standard rule chain.
    /// </summary>
    public CreateFloatingIpRequestValidator()
    {
        RuleFor(static request => request.ClusterId)
            .NotEqual(Guid.Empty)
                .WithMessage("ClusterId is required.");

        RuleFor(static request => request.Address)
            .NotEmpty()
                .WithMessage("Address is required.")
            .MaximumLength(AddressMaxLength)
                .WithMessage($"Address must be {AddressMaxLength} characters or fewer.")
            .Must(static address => IPAddress.TryParse(address, out _))
                .WithMessage("Address must be a valid IPv4 or IPv6 address.");
    }
}
