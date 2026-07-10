// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BannerArt — hand-crafted ASCII / Unicode art for Plexor CLI
// banners. NO Figlet, NO font loading, NO binary resources —
// every glyph is a raw string literal. Designed for monospace
// terminals with UTF-8 + Unicode box-drawing support (Windows
// Terminal, iTerm2, GNOME Terminal, Konsole, modern PuTTY).
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
    /// <summary>Plexor spiral logo — hand-rendered 4-petal approximation
    /// of the master SVG (plexor.s2.svg). 9 lines tall, raw shape
    /// only (no manual leading or trailing whitespace). <see
    /// cref="FullHelpBanner"/> centers each line via <see
    /// cref="CenterLine"/> so all shape centers land on the same
    /// column regardless of per-line width variation.</summary>
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

    /// <summary>Hex color codes used in the Plexor logo
    /// gradient. Applied per density char (░ lightest → █
    /// darkest) to give the "purple and black" black-hole
    /// effect — outer rings glow accent, inner core fades to
    /// near-black.</summary>
    public static class LogoColor
    {
        /// <summary>Lightest density (░): light purple glow.</summary>
        public const string Outer = "#a988ee";

        /// <summary>Mid density (▒): accent purple.</summary>
        public const string Mid = "#8c6ee6";

        /// <summary>Inner density (▓): dark purple.</summary>
        public const string Inner = "#523990";

        /// <summary>Densest core (█): near-black, matches the
        /// SVG fill <c>#1C1B1F</c>.</summary>
        public const string Core = "#1c1b1f";
    }

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

    /// <summary>Render the full help banner — the Plexor logo
    /// centered at the top with a purple-to-black density
    /// gradient, the version + tagline stacked underneath, and
    /// the command list at the bottom. No frame or borders;
    /// sections are separated by blank lines. Returns markup
    /// (consume with <c>AnsiConsole.MarkupLine</c>).</summary>
    public static string FullHelpBanner(
        string toolName,
        string version,
        string tagline,
        IReadOnlyList<CommandSpec>? commands = null)
    {
        var logoLines = PlexorLogo.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Reference width for centering. The widest logo line is
        // 25 chars; the longest command line is ~50 chars with
        // icon + branch + name + description. 56 fits both with
        // breathing room and avoids horizontal scroll in 80-col
        // terminals.
        const int width = 56;

        var sb = new StringBuilder();

        // Logo — center each line FIRST (visible width), then apply
        // the per-density gradient via per-char markup tags. Doing
        // the operations in this order keeps the shape's visual
        // center on a consistent column.
        foreach (var line in logoLines)
        {
            sb.AppendLine(ColorizeLogoLine(CenterLine(line, width)));
        }

        // Spacer between logo and tagline.
        sb.AppendLine();

        // Tool + version (centered). Tool name in accent, version muted.
        var mutedMarkup = ColorPalette.Muted.ToMarkup();
        sb.AppendLine(CenterLine(
            "[" + LogoColor.Mid + " bold]" + toolName + "[/] ["
            + mutedMarkup + "]v" + version + "[/]",
            width));

        // Tagline (centered, muted).
        sb.AppendLine(CenterLine(
            "[" + mutedMarkup + "]" + tagline + "[/]",
            width));

        // Commands section.
        if (commands is { Count: > 0 })
        {
            sb.AppendLine();

            // COMMANDS header — bold accent, centered.
            sb.AppendLine(CenterLine(
                "[" + LogoColor.Mid + " bold]COMMANDS[/]",
                width));

            for (var i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];
                var branch = i == commands.Count - 1 ? "└─" : "├─";
                // Branch muted, icon accent, name + bold accent,
                // description muted. Every element has a color so
                // the command list isn't monochrome.
                var line = "  ["
                    + mutedMarkup + "]" + branch + "[/] ["
                    + LogoColor.Mid + "]" + cmd.Icon + "[/]  ["
                    + LogoColor.Mid + " bold]" + cmd.Name + "[/] ["
                    + mutedMarkup + "]" + cmd.Description + "[/]";
                sb.AppendLine(line);
            }
        }

        return sb.ToString();
    }

    /// <summary>One-line compact mark for real command
    /// invocations. Logo glyph + tagline joined with separators.
    /// Returns markup (consume with <c>AnsiConsole.MarkupLine</c>).</summary>
    public static string CompactMark(string toolName, string version, string tagline)
    {
        var muted = ColorPalette.Muted.ToMarkup();
        return "  [" + LogoColor.Mid + "]" + Icon.Version + "[/] ["
            + LogoColor.Mid + " bold]" + toolName + "[/] ["
            + muted + "]v" + version + "[/] ["
            + muted + "]\u00b7[/] ["
            + muted + "]" + tagline + "[/]";
    }

    /// <summary>Apply the purple-to-black gradient to a single
    /// line of the Plexor logo. The four density levels map to
    /// four colors: <c>░</c> outer glow, <c>▒</c> accent,
    /// <c>▓</c> dark purple, <c>█</c> near-black core. Spaces are
    /// left uncolored (transparent background).</summary>
    private static string ColorizeLogoLine(string line)
    {
        var sb = new StringBuilder(line.Length + 64);
        var inTag = false;
        foreach (var c in line)
        {
            var color = c switch
            {
                '░' => LogoColor.Outer,
                '▒' => LogoColor.Mid,
                '▓' => LogoColor.Inner,
                '█' => LogoColor.Core,
                _ => null,
            };

            if (color is not null)
            {
                if (!inTag)
                {
                    sb.Append('[').Append(color).Append(']');
                    inTag = true;
                }

                sb.Append(c);
            }
            else
            {
                if (inTag)
                {
                    sb.Append("[/]");
                    inTag = false;
                }

                sb.Append(c);
            }
        }

        if (inTag)
        {
            sb.Append("[/]");
        }

        return sb.ToString();
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

    /// <summary>Center a line within the given width by adding
    /// equal padding on both sides. Truncates if the line is
    /// longer than the width.</summary>
    private static string CenterLine(string text, int width)
    {
        if (text.Length >= width)
        {
            return text[..width];
        }

        var padding = width - text.Length;
        var leftPad = padding / 2;
        var rightPad = padding - leftPad;
        return new string(' ', leftPad) + text + new string(' ', rightPad);
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