// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ICommandHandler — single-method command handler contract. Mirrors
// the Clusters module's ICommandHandler<TCommand, TResult>; kept
// local to Sigil for module independence. When a 3rd module needs
// the same shape, extract to Plexor.Shared.Kernel.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Abstractions;

/// <summary>
///     Marker interface shared by every auth command/query handler.
///     The mediator-style dispatch lives in Phase 5; for now callers
///     invoke handlers directly.
/// </summary>
/// <typeparam name="TCommand">Inbound command payload.</typeparam>
/// <typeparam name="TResult">Handler result payload.</typeparam>
public interface ICommandHandler<TCommand, TResult>
{
    /// <summary>Handle the command and return its result.</summary>
    /// <param name="command">The inbound command payload.</param>
    /// <param name="cancellationToken">Forwarded to IO.</param>
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}