// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BannerArt — hand-crafted ASCII / Unicode art for Plexor CLI
// banners. NO Figlet, NO font loading, NO binary resources —
// every glyph is a raw string literal. Designed for monospace
// terminals with UTF-8 + Unicode box-drawing support (Windows
// Terminal, iTerm2, GNOME Terminal, Konsole, modern PuTTY).
#pragma warning disable CA1834 // Prefer char overload: Box/Icon use const string for inline-call clarity; perf is negligible for art rendering.
//
// Why raw strings instead of a font library:
//   - Zero external dependency, AOT-friendly.
//   - Total control over the visual — change a single character
//     and the whole wordmark updates.
//   - No "unknown FIGlet character" failures when fonts drift
//     out of sync with Spectre.Console's parser.
//   - Brand identity stays ours, not a public font's.
//
// The Plexor logo (4-petal interlocking spiral) is hand-rendered
// from the master SVG (plexor.s2.svg) using Unicode block
// characters at four density levels (░▒▓█). The spirals are
// approximated as concentric octagons — close enough to the
// source shape that it reads as the same mark.
//
// Layout notes:
//   - Box drawing: ╭─╮ ╰╯ ┌┐└┘ ━ ┃  for frames and tables.
//   - Status icons: ✓ ⚠ ✗ ● ○ →  for state and progress.
//   - Command icons: ⛁ ▲ ▼ ◐  per command (init / upgrade /
//     destroy / version) — chosen to feel mechanical, not
//     emoji-fluffy.
//   - Progress blocks: █ ░  for filled/empty — single width so
//     they always render in monospace fonts.
// ============================================================================

using System.Text;

namespace Plexor.Shared.Console;

/// <summary>
/// Static catalog of hand-crafted banner art and primitives used
/// by every Plexor CLI binary.
/// </summary>
public static class BannerArt
{
    /// <summary>Plexor logo — hand-rendered 4-petal spiral
    /// approximation of the master SVG. 9 lines tall, ~22 chars
    /// wide. The center is intentionally empty so callers can
    /// overlay the tagline / version on top.</summary>
    public const string PlexorLogo = """
               ░▒▓▓▒░
             ░▒▓██████▓▒░
           ░▒▓██████████▓▒░
         ░▒▓███▀    ▀███▓▒░
        ░▒▓██▀        ▀██▓▒░
        ░▒▓██          ██▓▒░
         ░▒▓███▄    ▄███▓▒░
           ░▒▓██████████▓▒░
             ░▒▓██████▓▒░
    """;

    /// <summary>Compact version of the logo (no outer spiral
    /// ring). Used when the logo is rendered inline with text
    /// rather than as a free-floating mark.</summary>
    public const string PlexorLogoCompact = """
             ░▒▓▓▒░
           ░▒▓██▓▒░
          ░▒▓█  █▓▒░
          ░▒▓█  █▓▒░
          ░▒▓█  █▓▒░
           ░▒▓██▓▒░
             ░▒▓▓▒░
    """;

    /// <summary>Box drawing characters used for frames, tables,
    /// and decorative borders.</summary>
    public static class Box
    {
        /// <summary>Upper-left corner of a rounded frame: <c>╭</c>.</summary>
        public const string TopLeft = "╭";

        /// <summary>Upper-right corner of a rounded frame: <c>╮</c>.</summary>
        public const string TopRight = "╮";

        /// <summary>Lower-left corner of a rounded frame: <c>╰</c>.</summary>
        public const string BottomLeft = "╰";

        /// <summary>Lower-right corner of a rounded frame: <c>╯</c>.</summary>
        public const string BottomRight = "╯";

        /// <summary>Horizontal line of a frame: <c>─</c>.</summary>
        public const string Horizontal = "─";

        /// <summary>Vertical line of a frame: <c>│</c>.</summary>
        public const string Vertical = "│";

        /// <summary>T-junction with horizontal line above: <c>┬</c>.</summary>
        public const string TeeDown = "┬";

        /// <summary>T-junction with horizontal line below: <c>┴</c>.</summary>
        public const string TeeUp = "┴";

        /// <summary>T-junction with vertical line on the right: <c>├</c>.</summary>
        public const string TeeRight = "├";

        /// <summary>T-junction with vertical line on the left: <c>┤</c>.</summary>
        public const string TeeLeft = "┤";

        /// <summary>Cross-junction: <c>┼</c>.</summary>
        public const string Cross = "┼";
    }

    /// <summary>Status / category icons. Single-character
    /// Unicode glyphs; chosen for visual readability in
    /// monospace terminals (not emoji that need color fonts).</summary>
    public static class Icon
    {
        /// <summary>Provision / ignite — used for the <c>init</c> command: <c>⛁</c>.</summary>
        public const string Init = "⛁";

        /// <summary>Upward arrow — used for the <c>upgrade</c> command: <c>▲</c>.</summary>
        public const string Upgrade = "▲";

        /// <summary>Downward arrow — used for the <c>destroy</c> command: <c>▼</c>.</summary>
        public const string Destroy = "▼";

        /// <summary>Solid circle — used for state / status rows: <c>●</c>.</summary>
        public const string Status = "●";

        /// <summary>Half-filled circle — used for the <c>version</c> command: <c>◐</c>.</summary>
        public const string Version = "◐";

        /// <summary>Check mark — success outcome: <c>✓</c>.</summary>
        public const string Ok = "✓";

        /// <summary>Warning triangle — caution outcome: <c>⚠</c>.</summary>
        public const string Warn = "⚠";

        /// <summary>Cross mark — failure outcome: <c>✗</c>.</summary>
        public const string Error = "✗";

        /// <summary>Empty circle — pending / not-started: <c>○</c>.</summary>
        public const string Pending = "○";

        /// <summary>Right arrow — in-progress / running: <c>→</c>.</summary>
        public const string Running = "→";
    }

    /// <summary>Progress bar characters.</summary>
    public const string ProgressFull = "█";

    /// <summary>Empty progress block: <c>░</c>.</summary>
    public const string ProgressEmpty = "░";

    /// <summary>Render a horizontal progress bar at the given
    /// fraction in the given width. <paramref name="fraction"/>
    /// is clamped to [0, 1]. The string contains exactly
    /// <paramref name="width"/> characters.</summary>
    public static string ProgressBar(double fraction, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var clamped = Math.Clamp(fraction, 0.0, 1.0);
        var filledWidth = (int)Math.Round(clamped * width);
        var emptyWidth = width - filledWidth;
        return new string(ProgressFull[0], filledWidth) + new string(ProgressEmpty[0], emptyWidth);
    }

    /// <summary>Render the full help banner — boxed frame
    /// containing the Plexor logo on the left and the wordmark
    /// text on the right, with the tagline below. Used for
    /// help-like invocations.</summary>
    public static string FullHelpBanner(
        string toolName,
        string version,
        string tagline,
        IReadOnlyList<CommandSpec>? commands = null)
    {
        var logoLines = PlexorLogo.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        const int logoWidth = 26;
        const int innerWidth = 64;
        const int totalWidth = innerWidth + 2;

        var sb = new StringBuilder();

        // Top border
        sb.Append(Box.TopLeft)
          .Append(new string('─', innerWidth))
          .AppendLine(Box.TopRight);

        // Logo + tagline lines, joined with a vertical bar separator
        // in the middle of the innerWidth.
        var taglineLine = toolName + " v" + version;

        for (var i = 0; i < logoLines.Length; i++)
        {
            var logo = PadLine(logoLines[i], logoWidth);
            string right;
            if (i == logoLines.Length / 2)
            {
                // Center row — write the tool name + version
                right = PadLine(taglineLine, innerWidth - logoWidth - 2);
            }
            else if (i == logoLines.Length / 2 + 1)
            {
                // Next row — write the tagline
                right = PadLine(tagline, innerWidth - logoWidth - 2);
            }
            else
            {
                right = new string(' ', innerWidth - logoWidth - 2);
            }

            sb.Append(Box.Vertical)
              .Append(logo)
              .Append(Box.Vertical)
              .Append(right)
              .AppendLine(Box.Vertical);
        }

        // Empty spacer
        AppendBoxedLine(sb, totalWidth, string.Empty);

        // Command list
        if (commands is { Count: > 0 })
        {
            AppendBoxedLine(sb, totalWidth, "COMMANDS");
            for (var i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];
                var branch = i == commands.Count - 1 ? "└─" : "├─";
                var line = $"  {branch} {cmd.Icon}  {cmd.Name,-10} {cmd.Description ?? string.Empty}";
                AppendBoxedLine(sb, totalWidth, line);
            }
        }

        // Bottom border
        sb.Append(Box.BottomLeft)
          .Append(new string('─', innerWidth))
          .AppendLine(Box.BottomRight);

        return sb.ToString();
    }

    /// <summary>One-line compact mark for real command
    /// invocations. Logo glyph + tagline joined with separators.</summary>
    public static string CompactMark(string toolName, string version, string tagline)
    {
        return $"  {Icon.Version} {toolName} v{version} · {tagline}";
    }

    /// <summary>Append a single line of text inside the boxed
    /// banner. Left-aligned, padded to the inner width.</summary>
    private static void AppendBoxedLine(StringBuilder sb, int totalWidth, string text)
    {
        var innerWidth = totalWidth - 2;
        var display = text.Length > innerWidth ? text[..innerWidth] : text;
        var padding = innerWidth - display.Length;
        sb.Append(Box.Vertical)
          .Append(display)
          .Append(' ', padding)
          .AppendLine(Box.Vertical);
    }

    /// <summary>Pad a line to the given width (right-side
    /// padding). Truncates if the line is longer than
    /// <paramref name="width"/>.</summary>
    private static string PadLine(string text, int width)
    {
        if (text.Length > width)
        {
            return text[..width];
        }

        return text + new string(' ', width - text.Length);
    }
}

/// <summary>Metadata for a single registered command, used to
/// render the help-banner command list. Stored alongside the
/// Spectre.Console configuration lambda in
/// <c>PlexorCliContent.RegisteredCommands</c>.</summary>
/// <param name="Icon">Single Unicode glyph shown next to the
/// command name (e.g. <c>"⛁"</c> for init).</param>
/// <param name="Name">Command name as it appears on the CLI
/// (e.g. <c>"init"</c>, <c>"version"</c>).</param>
/// <param name="Description">One-line description shown in the
/// help table and help banner.</param>
/// <param name="Aliases">Optional short forms (e.g. <c>["v"]</c>
/// for <c>version</c>). Empty list if none.</param>
public sealed record CommandSpec(
    string Icon,
    string Name,
    string? Description,
    IReadOnlyList<string> Aliases)
{
    /// <summary>Convenience constructor for commands without
    /// aliases.</summary>
    public CommandSpec(string icon, string name, string? description)
        : this(icon, name, description, [])
    {
    }
}