// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MarkupExtensions — short alias helpers for Spectre.Console markup so
// the call sites read like tagged text (B("foo"), I("bar"), K("baz"))
// rather than a wall of color tags.
//
// Rule: methods use block body { return ...; } — expression-bodied
// methods are forbidden in this codebase (IDE0022, see engineering-
// process.md rule 3). Static helper methods that read like Markdown
// do not get a "single expression" exception.
// ============================================================================

namespace Plexor.Shared.Console;

/// <summary>
///     Short aliases for Spectre.Console markup tags. <c>B("foo")</c>
///     produces the same string as <c>"[bold]foo[/]"</c> but reads like
///     tagged text in call sites. The helpers return strings, not
///     renderables — pass them to <c>AnsiConsole.MarkupLine</c>.
/// </summary>
public static class MarkupExtensions
{
    /// <summary>Bold markup alias: <c>[bold]text[/]</c>.</summary>
    public static string B(string text)
    {
        return $"[bold]{text}[/]";
    }

    /// <summary>Italic markup alias: <c>[italic]text[/]</c>.</summary>
    public static string I(string text)
    {
        return $"[italic]{text}[/]";
    }

    /// <summary>Underline markup alias: <c>[underline]text[/]</c>.</summary>
    public static string U(string text)
    {
        return $"[underline]{text}[/]";
    }

    /// <summary>Dim / muted markup alias: <c>[grey]text[/]</c>.</summary>
    public static string D(string text)
    {
        return $"[grey]{text}[/]";
    }

    /// <summary>Strikethrough markup alias: <c>[strikethrough]text[/]</c>.</summary>
    public static string S(string text)
    {
        return $"[strikethrough]{text}[/]";
    }

    /// <summary>Invert / highlight markup alias: <c>[invert]text[/]</c>.</summary>
    public static string K(string text)
    {
        return $"[invert]{text}[/]";
    }

    /// <summary>Wrap text in a Plexor DS semantic color tag.</summary>
    public static string Ok(string text)
    {
        return $"[{ColorPalette.Ok.ToMarkup()}]{text}[/]";
    }

    /// <summary>Wrap text in a Plexor DS semantic color tag.</summary>
    public static string Err(string text)
    {
        return $"[{ColorPalette.Error.ToMarkup()}]{text}[/]";
    }

    /// <summary>Wrap text in a Plexor DS semantic color tag.</summary>
    public static string Warn(string text)
    {
        return $"[{ColorPalette.Warn.ToMarkup()}]{text}[/]";
    }

    /// <summary>Wrap text in a Plexor DS semantic color tag.</summary>
    public static string Idl(string text)
    {
        return $"[{ColorPalette.Idle.ToMarkup()}]{text}[/]";
    }

    /// <summary>Wrap text in a Plexor DS semantic color tag.</summary>
    public static string Accent(string text)
    {
        return $"[{ColorPalette.Accent.ToMarkup()}]{text}[/]";
    }

    /// <summary>Wrap text in a Plexor DS semantic color tag.</summary>
    public static string Muted(string text)
    {
        return $"[{ColorPalette.Muted.ToMarkup()}]{text}[/]";
    }
}
