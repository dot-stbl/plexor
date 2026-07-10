// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AsciiBanner — large ASCII headers rendered via Spectre.Console
// FigletText. Two built-in presets: "PLEXOR" (default) and
// "plexor" (lower-case monogram for compact contexts). Callers can
// also pass a custom string for project-specific greetings.
// ============================================================================

using Spectre.Console;

namespace Plexor.Shared.Console;

/// <summary>
/// ASCII header renderer. Used at the top of <c>plx</c> startup and
/// as the title of any <c>--help</c> output.
/// </summary>
public static class AsciiBanner
{
    /// <summary>Render the uppercase "PLEXOR" banner. Used by
    /// every CLI in the Plexor family — keeps the visual identity
    /// consistent across <c>plx init</c>, <c>plx cluster ls</c>,
    /// future <c>plx admin</c>, etc.</summary>
    /// <param name="style">Color tag applied to every glyph.
    /// Defaults to <see cref="ColorPalette.Accent"/>.</param>
    public static FigletText Plexor(Color? style = null)
    {
        return RenderFiglet("PLEXOR", style ?? ColorPalette.Accent);
    }

    /// <summary>Render the lowercase "plexor" banner. Tighter
    /// than the uppercase one — fits a single-line command list
    /// or a compact log header.</summary>
    public static FigletText PlexorCompact(Color? style = null)
    {
        return RenderFiglet("plexor", style ?? ColorPalette.Accent);
    }

    /// <summary>Render a caller-supplied string. Used by sibling
    /// tools (<c>plx cluster</c>, <c>plx admin</c>) that want
    /// their own banner while staying visually consistent.</summary>
    public static FigletText Custom(string text, Color? style = null)
    {
        return RenderFiglet(text, style ?? ColorPalette.Accent);
    }

    /// <summary>Render the brand glyph as plain (colorless) text.
    /// Useful for piped output where ANSI codes would corrupt
    /// downstream parsers.</summary>
    public static FigletText PlainFiglet(string text)
    {
        return new FigletText(text);
    }

    private static FigletText RenderFiglet(string text, Color style)
    {
        return new FigletText(text)
            .Color(style);
    }
}