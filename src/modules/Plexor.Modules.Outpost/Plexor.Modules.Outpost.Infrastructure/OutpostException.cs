// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostException — typed exception for Outpost-raised errors. Carries
// a stable machine-readable Code that the IExceptionHandler maps to a
// ProblemDetails `code` extension. Mirrors ClustersException + Audit
// + Quota — one hierarchy per module, codes dot.case.
// ============================================================================

namespace Plexor.Modules.Outpost.Infrastructure;

/// <summary>
///     Outpost-raised exception. Carries a stable
///     <see cref="Code" /> + a human-readable message. Callers
///     branch on <see cref="Code" />, never on the message text.
/// </summary>
public sealed class OutpostException : Exception
{
    /// <summary>
    ///     Construct a new Outpost exception with a stable code +
    ///     human-readable message.
    /// </summary>
    /// <param name="code">Stable machine code (dot.case).</param>
    /// <param name="message">Human-readable description.</param>
    public OutpostException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>
    ///     Stable machine-readable code (dot.case). Maps to the
    ///     ProblemDetails <c>code</c> extension on the wire.
    /// </summary>
    public string Code { get; }
}