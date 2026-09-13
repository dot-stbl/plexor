// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RateLimitPrincipalKind — subject of a RateLimitEvent row, and the
// discriminator the action filter uses when constructing a
// RateLimitPrincipal for the limiter. Lives in Plexor.Shared.Kernel
// because the IRateLimiter contract and the RateLimitEvent entity
// both reference it; the entity lives in Plexor.Modules.Quotas.Domain,
// the contract lives in Plexor.Shared.Kernel.Quotas, and the
// Plexor.Modules.Quotas.Domain assembly already references the kernel,
// so the enum sits in the kernel to keep one source of truth.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Subject of a <c>RateLimitEvent</c> row. Stored as a string in
///     the database so a future principal kind (e.g. <c>"service_account"</c>)
///     does not require a schema migration.
/// </summary>
public enum RateLimitPrincipalKind
{
    /// <summary>Authenticated human user (JWT auth).</summary>
    User = 0,

    /// <summary>Long-lived service API key.</summary>
    ApiKey = 1,
}
