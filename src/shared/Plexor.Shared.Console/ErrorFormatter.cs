// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ErrorFormatter — uniform error formatting for every Plexor CLI.
// All error rendering goes through this class so the user sees the
// same "[err] prefix · reason" pattern no matter which command or
// binary threw the failure.
//
// Style rules:
//   - One line.
//   - First token is the bracketed severity marker (in Plexor DS Error color).
//   - Then a primary message in bold.
//   - Then an optional separator and a "because" detail in muted gray.
//
// Example:
//
//     [err] cluster-name: empty    because: --cluster-name is required
//     [warn] stale config          because: last fetched 2d ago
//     [ok]  workload vm-prod-01    because: running for 12m
//
// Implementation note: do NOT pass already-wrapped markup strings
// (like `MarkupExtensions.Err("err")`) into Render — Render builds
// the wrap itself, so a wrapped input would produce double tags.
// Pass the bare severity label and let Render apply the color.
// ============================================================================

using Spectre.Console;

namespace Plexor.Shared.Console;

/// <summary>
///     One-line, color-coded error / warning / ok formatter. Keeps
///     every Plexor CLI's failure output visually consistent.
/// </summary>
public static class ErrorFormatter
{
    /// <summary>
    ///     Format an error. <paramref name="primary" /> is
    ///     the main message; <paramref name="because" /> is the
    ///     optional detail that comes after the separator.
    /// </summary>
    public static string Error(string primary, string? because = null)
    {
        return Render(ColorPalette.Error, "err", primary, because);
    }

    /// <summary>Format a warning.</summary>
    public static string Warn(string primary, string? because = null)
    {
        return Render(ColorPalette.Warn, "warn", primary, because);
    }

    /// <summary>
    ///     Format a successful outcome (rare — usually you'd
    ///     just print the result, but some workflows benefit from a
    ///     one-line "ok" stamp).
    /// </summary>
    public static string Ok(string primary, string? because = null)
    {
        return Render(ColorPalette.Ok, "ok", primary, because);
    }

    /// <summary>Format an informational note (no severity).</summary>
    public static string Info(string primary, string? because = null)
    {
        return Render(ColorPalette.Idle, "info", primary, because);
    }

    private static string Render(Color severityColor, string severityLabel, string primary, string? because)
    {
        var severity = $"[{severityColor.ToMarkup()}]{severityLabel}[/]";
        var head = $"{severity} {MarkupExtensions.B(primary)}";
        return string.IsNullOrWhiteSpace(because)
                ? head
                : $"{head}  {MarkupExtensions.Muted("because:")} {MarkupExtensions.Muted(because)}";
    }
}
