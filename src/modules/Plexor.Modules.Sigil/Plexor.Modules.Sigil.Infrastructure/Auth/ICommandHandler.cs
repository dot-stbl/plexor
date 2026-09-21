// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ICommandHandler — marker interface shared by every auth command /
// query handler in this module. The mediator-style dispatch lives in
// Phase 5; for now callers invoke handlers directly.
//
// Extracted from AuthCommandHandlers.cs (issue #81 / M2) so the
// interface lives in its own file per folder-organization.md §1.
//
// Note: this is the Sigil-module-local ICommandHandler, not the
// one in Plexor.Modules.Clusters.Application.Abstractions. Each
// module owns its own contract surface; cross-module sharing would
// be a deliberate Shared.Kernel decision.
// ============================================================================

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Marker interface shared by every auth command/query handler. The
///     mediator-style dispatch lives in Phase 5; for now callers
///     invoke handlers directly.
/// </summary>
/// <typeparam name="TCommand"></typeparam>
/// <typeparam name="TResult"></typeparam>
public interface ICommandHandler<TCommand, TResult>
{
    /// <summary>Handle the command and return its result.</summary>
    /// <param name="command">The inbound command payload.</param>
    /// <param name="cancellationToken">Forwarded to IO.</param>
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
