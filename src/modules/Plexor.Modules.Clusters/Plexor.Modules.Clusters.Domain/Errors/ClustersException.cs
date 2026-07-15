// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClustersException — domain-level errors for the Clusters module.
// Mirrors the Sigil IdentityException pattern: a typed Code field
// (string discriminator, not enum) so handlers can dispatch on ex.Code
// without switch-on-string typo-safety. Mapped 1:1 to RFC 7807
// ProblemDetails type by the Api layer's exception handler.
// ============================================================================

namespace Plexor.Modules.Clusters.Domain.Errors;

/// <summary>
///     Discriminator string constants for Clusters-domain errors. Three
///     lowercase dot-delimited segments (<c>clusters.&lt;entity&gt;.&lt;condition&gt;</c>);
///     the ProblemDetails <c>type</c> becomes
///     <c>/errors/clusters.cluster.not_found</c> at the Api error handler.
/// </summary>
public static class ClustersExceptions
{
    /// <summary>Cluster id did not match any row.</summary>
    public const string ClusterNotFound = "clusters.cluster.not_found";

    /// <summary>Cluster name already taken in this org (unique index violation).</summary>
    public const string ClusterNameTaken = "clusters.cluster.name_taken";

    /// <summary>Join token did not match, is revoked, or is expired.</summary>
    public const string InvalidJoinToken = "clusters.join_token.invalid";

    /// <summary>Join token was already redeemed (one-time use).</summary>
    public const string JoinTokenConsumed = "clusters.join_token.consumed";

    /// <summary>Hostname already taken by another node in the cluster.</summary>
    public const string NodeHostnameTaken = "clusters.node.hostname_taken";

    /// <summary>Node id did not match any row.</summary>
    public const string NodeNotFound = "clusters.node.not_found";

    /// <summary>Status transition not allowed (e.g. Offline → Pending).</summary>
    public const string IllegalStatusTransition = "clusters.status.illegal_transition";
}

/// <summary>
///     Domain-level exception raised by Clusters entities + handlers.
/// Carries a discriminator code so callers can react programmatically
/// instead of parsing the message string.
/// </summary>
public sealed class ClustersException : Exception
{
    /// <summary>
    ///     Stable discriminator code (one of the
    ///     <see cref="ClustersExceptions" /> constants). Mapped to
    ///     RFC 7807 ProblemDetails <c>type</c> field by the Api layer.
    /// </summary>
    public string Code { get; }

    /// <summary>
    ///     Constructs a domain error with a discriminator code + message.
    /// </summary>
    /// <param name="code">One of the <see cref="ClustersExceptions" />
    /// constants.</param>
    /// <param name="message">Human-readable description; goes in the
    /// ProblemDetails <c>detail</c> field.</param>
    public ClustersException(string code, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Clusters exception code cannot be null or whitespace.",
                nameof(code));
        }

        Code = code;
    }

    /// <summary>
    ///     Constructs a domain error wrapping an inner exception. Used
    ///     when a wrapping layer (e.g. EF Core) throws and we want to
    ///     surface the underlying cause.
    /// </summary>
    /// <param name="code">Discriminator code (see <see cref="ClustersExceptions" />).</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="innerException">Underlying exception (logged, not displayed).</param>
    public ClustersException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
