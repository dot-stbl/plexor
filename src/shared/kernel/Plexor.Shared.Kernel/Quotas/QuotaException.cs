// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaException — base exception for the quotas module. Lives in
// Plexor.Shared.Kernel because resource-create handlers in Compute /
// Storage / Network throw it (4.5.c/d) — placing it in the kernel
// avoids a cross-module dependency from each consumer module to
// Plexor.Modules.Quotas.Domain. Mirrors the
// Plexor.Modules.Sigil.Domain.Errors.IdentityException pattern.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Discriminator string constants for Quotas-domain errors. Flat,
///     lowercase, dot-delimited — same convention as
///     <c>Plexor.Modules.Sigil.Domain.Errors.IdentityExceptions</c>.
///     The string is mapped 1:1 to the RFC 7807 ProblemDetails
///     <c>type</c> field by the controller error handler in 4.5.g.
/// </summary>
public static class QuotaExceptions
{
    /// <summary>A <c>QuotaCheckResult.Denied</c> was returned by the
    /// enforcer — the call would exceed the effective limit.</summary>
    public const string Exceeded = "quotas.exceeded";
}

/// <summary>
///     Domain-level exception raised by the Quotas enforcer and
/// resource-create handlers across modules. Carries a stable
/// discriminator code so callers can react programmatically instead
/// of parsing the message string.
/// </summary>
/// <remarks>
///     <para><b>Catch sites.</b> The Infrastructure error handler in
///     4.5.g maps each <see cref="QuotaExceptions" /> code to a
///     specific HTTP status (429 for <see cref="QuotaExceededException" />).
///     Controllers never catch this directly — they bubble up to the
///     global error handler that converts to ProblemDetails.</para>
/// </remarks>
public class QuotaException : Exception
{
    /// <summary>Stable discriminator code (one of the
    /// <see cref="QuotaExceptions" /> constants). Mapped to
    /// RFC 7807 ProblemDetails <c>type</c>.</summary>
    public string Code { get; }

    /// <summary>Construct a domain error with a discriminator code + message.</summary>
    /// <param name="code">One of the <see cref="QuotaExceptions" /> constants.</param>
    /// <param name="message">Human-readable description. Used in
    /// ProblemDetails <c>detail</c>; never displayed in the UI directly.</param>
    public QuotaException(string code, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Quota exception code cannot be null or whitespace.",
                nameof(code));
        }

        Code = code;
    }

    /// <summary>Construct a domain error wrapping an inner exception.</summary>
    /// <param name="code">Discriminator code (see <see cref="QuotaExceptions" />).</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="innerException">Underlying exception (logged, not displayed).</param>
    public QuotaException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
