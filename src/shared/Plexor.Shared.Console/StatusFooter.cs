// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StatusFooter — one-line status strip appended to command output
// (or printed at the end of a CLI session). Pattern:
//
//     plexor v0.2.1 · node prod-eu-1 · cluster prod-cluster · 3s ago
//
// Built from a parameter object so call sites only set what they
// have. Rendered as Spectre.Console Markup so the colors carry over.
// ============================================================================

using Spectre.Console;

namespace Plexor.Shared.Console;

/// <summary>
/// One-line footer summarizing the CLI session: tool name, version,
/// optionally a cluster or node name, and a duration label. Used
/// after the last table or rule so users get a "what just happened"
/// summary without scrolling back.
/// </summary>
/// <param name="Duration">How long the command took. Optional —
/// null means don't render the timing line at all.</param>
public sealed record StatusFooter(
    string ToolName,
    string Version,
    string? ClusterName = null,
    string? NodeName = null,
    TimeSpan? Duration = null)
{
    /// <summary>Render the footer as a single line of markup.
    /// Pass to <c>AnsiConsole.MarkupLine</c>.</summary>
    public string Render()
    {
        var parts = new List<string>
        {
            MarkupExtensions.Accent(ToolName),
            MarkupExtensions.Muted($"v{Version}"),
        };

        if (!string.IsNullOrWhiteSpace(ClusterName))
        {
            parts.Add(MarkupExtensions.Muted("·"));
            parts.Add(MarkupExtensions.Ok(ClusterName));
        }

        if (!string.IsNullOrWhiteSpace(NodeName))
        {
            parts.Add(MarkupExtensions.Muted("·"));
            parts.Add(MarkupExtensions.B(NodeName));
        }

        if (Duration is { } d)
        {
            parts.Add(MarkupExtensions.Muted("·"));
            parts.Add(MarkupExtensions.Muted(FormatDuration(d)));
        }

        return string.Join(' ', parts);
    }

    private static string FormatDuration(TimeSpan d)
    {
        return d.TotalSeconds switch
        {
            < 1 => $"{d.TotalMilliseconds:F0}ms",
            < 60 => $"{d.TotalSeconds:F1}s",
            < 3600 => $"{d.TotalMinutes:F0}m {d.Seconds}s",
            _ => $"{d.TotalHours:F0}h {d.Minutes}m",
        };
    }
}