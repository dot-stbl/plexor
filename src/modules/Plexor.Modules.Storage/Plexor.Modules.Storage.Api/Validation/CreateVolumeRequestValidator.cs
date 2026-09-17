// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVolumeRequestValidator — FluentValidation chain for the
// create-volume body. Runs in the controller via [FromServices] before
// any service call; failures surface as 400 ValidationProblemDetails.
// ============================================================================

using FluentValidation;
using Plexor.Modules.Storage.Api.Models.Requests;

namespace Plexor.Modules.Storage.Api.Validation;

/// <summary>
///     Validates the create-volume request body. Rules:
///     <list type="bullet">
///         <item><see cref="CreateVolumeRequest.ClusterId" /> is non-empty
///         (an empty GUID would route the row to a non-existent
///         cluster; the API layer validates the cluster exists at
///         write time).</item>
///         <item><see cref="CreateVolumeRequest.Name" /> is non-empty
///         and ≤ 128 characters (matches the column max length).</item>
///         <item><see cref="CreateVolumeRequest.SizeGb" /> is in the
///         1..65536 range — single-digit GiB is too small for any
///         real workload, and 64 TiB is the upper bound the spec
///         documents.</item>
///     </list>
/// </summary>
public sealed class CreateVolumeRequestValidator
    : AbstractValidator<CreateVolumeRequest>
{
    /// <summary>Maximum size in GiB — 64 TiB (matches the spec's
    /// documented upper bound).</summary>
    public const int MaxSizeGb = 65_536;

    /// <summary>
    ///     Construct the validator with the standard rule chain.
    /// </summary>
    public CreateVolumeRequestValidator()
    {
        RuleFor(static request => request.ClusterId)
            .NotEqual(Guid.Empty)
                .WithMessage("ClusterId is required.");

        RuleFor(static request => request.Name)
            .NotEmpty()
                .WithMessage("Name is required.")
            .MaximumLength(128)
                .WithMessage("Name must be 128 characters or fewer.");

        RuleFor(static request => request.SizeGb)
            .InclusiveBetween(1, MaxSizeGb)
                .WithMessage($"SizeGb must be between 1 and {MaxSizeGb} GiB.");
    }
}
