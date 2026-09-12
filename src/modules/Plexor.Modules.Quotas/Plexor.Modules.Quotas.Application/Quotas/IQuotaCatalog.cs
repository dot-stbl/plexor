using Plexor.Modules.Quotas.Domain.Entities;

namespace Plexor.Modules.Quotas.Application.Quotas;

/// <summary>
///     Read-only queries against the <see cref="QuotaDefinition" />
///     catalog. Consumed by the 4.5.b enforcer to resolve effective
///     defaults when no <c>QuotaAssignment</c> exists for the
///     requested scope, and by the 4.5.g REST endpoints to surface
///     the catalog to operators.
/// </summary>
/// <remarks>
///     <para><b>Why an interface here.</b> The interface lives in
///     <c>Plexor.Modules.Quotas.Application</c> so the enforcer and
///     controllers depend on the contract — not on the concrete
///     EF-backed implementation in <c>Plexor.Modules.Quotas.Infrastructure</c>.
///     Tests can substitute a fake without spinning up Postgres.</para>
/// </remarks>
public interface IQuotaCatalog
{
    /// <summary>Look up a single catalog entry by its stable
    /// <see cref="QuotaDefinition.Key" />.</summary>
    /// <param name="key">Stable catalog identifier
    /// (<c>"compute.vms.count"</c>, ...).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The matching <see cref="QuotaDefinition" />, or
    /// <c>null</c> if no row with that key exists in the catalog.</returns>
    public Task<QuotaDefinition?> FindByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>List every <see cref="QuotaDefinition" /> row in the
    /// catalog (used by the 4.5.g <c>GET /api/v1/quotas/definitions</c>
    /// endpoint).</summary>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>All catalog entries, ordered by
    /// <see cref="QuotaDefinition.Key" />.</returns>
    public Task<IReadOnlyList<QuotaDefinition>> ListAllAsync(CancellationToken cancellationToken = default);
}
