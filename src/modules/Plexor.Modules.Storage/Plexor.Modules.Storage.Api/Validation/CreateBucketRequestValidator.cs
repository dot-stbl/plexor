// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateBucketRequestValidator — FluentValidation chain for the
// create-bucket body. Runs in the controller via [FromServices] before
// any service call; failures surface as 400 ValidationProblemDetails.
// ============================================================================

using FluentValidation;
using Plexor.Modules.Storage.Api.Models.Requests;

namespace Plexor.Modules.Storage.Api.Validation;

/// <summary>
///     Validates the create-bucket request body. Rules:
///     <list type="bullet">
///         <item><see cref="CreateBucketRequest.Name" /> matches the
///         S3 DNS-label rule: 3-63 chars, lowercase alphanumeric +
///         hyphen, cannot start or end with a hyphen.</item>
///         <item><see cref="CreateBucketRequest.Region" /> is
///         non-empty and ≤ 64 chars (matches the column max
///         length).</item>
///     </list>
/// </summary>
public sealed class CreateBucketRequestValidator
    : AbstractValidator<CreateBucketRequest>
{
    /// <summary>S3 bucket-name pattern. Matches the AWS S3 naming
    /// rules: lowercase alphanumeric + hyphen, cannot start/end
    /// with a hyphen, 3-63 chars.</summary>
    public const string BucketNamePattern = "^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$";

    /// <summary>
    ///     Construct the validator with the standard rule chain.
    /// </summary>
    public CreateBucketRequestValidator()
    {
        RuleFor(static request => request.Name)
            .NotEmpty()
                .WithMessage("Name is required.")
            .MaximumLength(63)
                .WithMessage("Name must be 63 characters or fewer.")
            .MinimumLength(3)
                .WithMessage("Name must be at least 3 characters.")
            .Matches(BucketNamePattern)
                .WithMessage("Name must be a valid S3 bucket DNS label (lowercase alphanumeric + hyphen, 3-63 chars).");

        RuleFor(static request => request.Region)
            .NotEmpty()
                .WithMessage("Region is required.")
            .MaximumLength(64)
                .WithMessage("Region must be 64 characters or fewer.");
    }
}
