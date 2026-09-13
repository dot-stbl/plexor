// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DatabaseExtensions — small helper that opens a transaction only when
// the underlying provider supports one (Postgres, SQL Server, etc.).
// Lets handler unit tests run against the InMemory provider without
// hitting the "transactions not supported" InvalidOperationException
// while keeping the production transactional path intact.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Plexor.Shared.Persistence;

/// <summary>
///     Helpers around <see cref="DatabaseFacade" /> for handlers that
///     need a transaction in production but run cleanly against the
///     InMemory provider in unit tests.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    ///     Open a transaction if the provider supports one, else return
    ///     <c>null</c>. The caller checks the returned value before
    ///     committing — the InMemory path skips the commit because no
    ///     transaction was opened.
    /// </summary>
    /// <param name="database">EF Core <see cref="DatabaseFacade" />.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    ///     An open <see cref="IDbContextTransaction" />, or <c>null</c>
    ///     if the provider does not support transactions (e.g.
    ///     InMemory).
    /// </returns>
    public static async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(
        this DatabaseFacade database,
        CancellationToken cancellationToken = default)
    {
        if (database.IsRelational())
        {
            return await database.BeginTransactionAsync(cancellationToken);
        }

        return null;
    }
}
