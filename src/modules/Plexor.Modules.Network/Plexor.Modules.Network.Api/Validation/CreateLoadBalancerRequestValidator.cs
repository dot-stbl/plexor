// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateLoadBalancerRequestValidator — FluentValidation chain for the
// create-load-balancer body. Runs in the endpoint via [FromServices]
// before any service call; failures surface as 400
// ValidationProblemDetails.
// ============================================================================

using FluentValidation;
using Plexor.Modules.Network.Api.Models.Requests;
using Plexor.Modules.Network.Domain.Entities;

namespace Plexor.Modules.Network.Api.Validation;

/// <summary>
///     Validates the create-load-balancer request body. Rules:
///     <list type="bullet">
///         <item><see cref="CreateLoadBalancerRequest.ClusterId" /> is non-empty.</item>
///         <item><see cref="CreateLoadBalancerRequest.Name" /> is non-empty + ≤ 128 chars.</item>
///         <item><see cref="CreateLoadBalancerRequest.Type" /> parses as <see cref="LoadBalancerType" />.</item>
///         <item><see cref="CreateLoadBalancerRequest.Algorithm" /> parses as <see cref="LoadBalancerAlgorithm" />.</item>
///     </list>
/// </summary>
public sealed class CreateLoadBalancerRequestValidator
    : AbstractValidator<CreateLoadBalancerRequest>
{
    /// <summary>Load balancer name cap. 128 chars leaves headroom for
    /// operator-assigned labels (<c>prod-edge-lb-001</c>, etc.) without a
    /// schema migration.</summary>
    public const int NameMaxLength = 128;

    /// <summary>
    ///     Construct the validator with the standard rule chain.
    /// </summary>
    public CreateLoadBalancerRequestValidator()
    {
        RuleFor(static request => request.ClusterId)
            .NotEqual(Guid.Empty)
                .WithMessage("ClusterId is required.");

        RuleFor(static request => request.Name)
            .NotEmpty()
                .WithMessage("Name is required.")
            .MaximumLength(NameMaxLength)
                .WithMessage($"Name must be {NameMaxLength} characters or fewer.");

        RuleFor(static request => request.Type)
            .NotEmpty()
                .WithMessage("Type is required.")
            .Must(static value => Enum.TryParse<LoadBalancerType>(value, ignoreCase: false, out _))
                .WithMessage("Type must be one of: L4, L7.");

        RuleFor(static request => request.Algorithm)
            .NotEmpty()
                .WithMessage("Algorithm is required.")
            .Must(static value => Enum.TryParse<LoadBalancerAlgorithm>(value, ignoreCase: false, out _))
                .WithMessage("Algorithm must be one of: RoundRobin, LeastConnections, SourceIpHash, WeightedRoundRobin.");
    }
}
