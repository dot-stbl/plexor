// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AsciiBanner — large ASCII headers rendered via Spectre.Console
// FigletText. Uses the standard Figlet font (Spectre.Console's
// bundled default) for portability. Custom font files were tried
// (Big, Banner from xero/figlet-fonts) but they raise
// 'Unknown index for FIGlet character' under Spectre.Console 0.49
// because the font's character table isn't compatible with
// Spectre's parser. Sticking with the standard font keeps the
// build working and the visual consistent with what users get from
// other .NET CLIs that use Spectre.Console.
// ============================================================================

using Spectre.Console;

namespace Plexor.Shared.Console;

/// <summary>
/// Renderable ASCII header. Used at the top of <c>plx</c> startup and
/// as the title of any <c>--help</c> output.
/// </summary>
public static class AsciiBanner
{
    /// <summary>Render the Plexor brand mark in the standard
    /// Figlet font. Used for help-like invocations (--help,
    /// --version, no args) where the user is reading the visual
    /// header.</summary>
    /// <param name="style">Color applied to every glyph.
    /// Defaults to <see cref="ColorPalette.Accent"/>.</param>
    public static FigletText Plexor(Color? style = null)
    {
        return RenderFiglet("plexor", style ?? ColorPalette.Accent);
    }

    /// <summary>Render a caller-supplied string in the standard
    /// Figlet font. Used by sibling tools (<c>plx cluster</c>,
    /// <c>plx admin</c>) that want their own banner while staying
    /// visually consistent.</summary>
    public static FigletText Custom(string text, Color? style = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return RenderFiglet(text, style ?? ColorPalette.Accent);
    }

    /// <summary>Render the brand glyph as plain (colorless) text.
    /// Useful for piped output where ANSI codes would corrupt
    /// downstream parsers.</summary>
    public static FigletText PlainFiglet(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new FigletText(text);
    }

    private static FigletText RenderFiglet(string text, Color style)
    {
        return new FigletText(text)
            .Color(style);
    }
}