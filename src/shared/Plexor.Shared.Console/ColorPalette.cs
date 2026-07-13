// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ColorPalette — Plexor DS status colors mapped to Spectre.Console.Color.
// Use these instead of hard-coded Color.Green / Color.Red / Color.Yellow —
// keeps the CLI output consistent with the web console (which uses the
// same tokens via :root CSS custom properties).
// ============================================================================

using Spectre.Console;

namespace Plexor.Shared.Console;

/// <summary>
///     Status semantics from the Plexor Design System, mapped to terminal
///     colors. Use these instead of inline <see cref="Color.Yellow" /> etc.
///     so output stays consistent with the web console.
/// </summary>
public static class ColorPalette
{
    /// <summary>
    ///     Positive outcome — operation succeeded, resource is
    ///     healthy, count is in normal range.
    /// </summary>
    public static Color Ok { get; } = new(60, 180, 110); // matches --ok

    /// <summary>
    ///     Negative outcome — operation failed, resource is
    ///     in error state, validation rejected.
    /// </summary>
    public static Color Error { get; } = new(220, 80, 80); // matches --err

    /// <summary>
    ///     Caution — non-fatal warning, retry, degraded state,
    ///     "this might bite you if you ignore it".
    /// </summary>
    public static Color Warn { get; } = new(220, 170, 60); // matches --warn

    /// <summary>
    ///     Neutral / pending — the resource is idle, queued,
    ///     or no data is available yet.
    /// </summary>
    public static Color Idle { get; } = new(140, 150, 165); // matches --idle

    /// <summary>
    ///     Accent / brand — section headers, primary action
    ///     highlights. Matches Plexor's accent tone.
    /// </summary>
    public static Color Accent { get; } = new(140, 110, 230);

    /// <summary>
    ///     Deemphasized metadata — version strings, secondary
    ///     info, "modified 3 days ago".
    /// </summary>
    public static Color Muted { get; } = new(140, 150, 165);
}
