// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ICommandHandler — single-method command handler contract. Mirrors
// the Sigil module's ICommandHandler<TCommand, TResult>; kept local
// to Clusters for module independence. When a 3rd module needs the
// same shape, extract to Plexor.Shared.Kernel.
// ============================================================================

namespace Plexor.Modules.Clusters.Application.Abstractions;

/// <summary>
///     Handles a command of type <typeparamref name="TCommand" /> and
///     returns a result of type <typeparamref name="TResult" />.
///     Implemented in Infrastructure; resolved by the Api layer's
///     controller via constructor DI.
/// </summary>
/// <typeparam name="TCommand">Command payload (immutable record).</typeparam>
/// <typeparam name="TResult">Result payload (immutable record).</typeparam>
public interface ICommandHandler<TCommand, TResult>
{
    /// <summary>Handle the command and return its result.</summary>
    /// <param name="command">The inbound command payload.</param>
    /// <param name="cancellationToken">Forwarded to every IO call.</param>
    /// <returns>The command's result.</returns>
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
