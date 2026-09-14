// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// FakeClock — minimal TimeProvider for unit tests. Returns the
// supplied UtcNow on every GetUtcNow() so tests can assert against
// a known timestamp.
// ============================================================================

namespace Plexor.Modules.Branding.Unit;

/// <summary>
///     Test double for <see cref="TimeProvider" />. Returns the
///     constructor-supplied <see cref="DateTimeOffset" /> on every
///     <see cref="GetUtcNow" /> call so tests can assert the exact
///     stamps the service writes.
/// </summary>
public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset current;

    public FakeClock(DateTimeOffset now)
    {
        current = now;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return current;
    }

    public void Advance(TimeSpan delta)
    {
        current = current.Add(delta);
    }
}