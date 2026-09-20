// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// FakeClock — TimeProvider stand-in. Returns the same wall-clock instant
// for every GetUtcNow() call (matches the in-memory DbContext tests in
// Plexor.Modules.Audit.Unit).
// ============================================================================

namespace Plexor.Providers.VSphere.Unit;

/// <summary>
///     TimeProvider that always returns a fixed
///     <see cref="DateTimeOffset" />. Lets tests pin the timestamps
///     the inventory refresher / provisioning service write into
///     the audit-trail rows.
/// </summary>
/// <remarks>
///     Construct the clock with a fixed wall-clock instant.
/// </remarks>
/// <param name="now">The instant every <c>GetUtcNow</c> call
/// should return.</param>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>The fixed instant returned by every
    /// <c>GetUtcNow</c> call.</summary>
    private readonly DateTimeOffset now = now;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        return now;
    }
}
