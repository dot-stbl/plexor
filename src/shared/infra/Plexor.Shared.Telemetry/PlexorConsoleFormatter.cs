// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorConsoleFormatter — custom ILogger console formatter for Plexor.
//
// Color-coded by log level (info dim cyan, warn yellow, error red bold,
// critical red bold + magenta background) via Spectre.Console. Format:
//   HH:mm:ss.fff [LEVEL] {category}: message
//   {exception indented, dim, on a new line}
//
// Registered via PlexorHostExtensions.AddPlexorConsole (see below).
// ============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Spectre.Console;

namespace Plexor.Shared.Telemetry;

/// <summary>
///     Plexor-styled console formatter for <see cref="ILogger" /> output.
///     Reads <see cref="Microsoft.Extensions.Logging.Console.ConsoleFormatter" />
///     from <c>Microsoft.Extensions.Logging.Console</c> and emits a
///     single-line, color-coded format via Spectre.Console
///     <see cref="AnsiConsole.WriteLine(string)" />.
/// </summary>
/// <remarks>
///     <para><b>Format</b>: <c>HH:mm:ss.fff [LEVEL] {Category}: message</c> on
///     one line. Exceptions (if present) follow on indented dim lines
///     with the type name prefixed.</para>
///     <para><b>Level colors</b> (Spectre markup):
///     <list type="bullet">
///       <item><c>Trace</c> / <c>Debug</c>: dim gray</item>
///       <item><c>Information</c>: dim cyan</item>
///       <item><c>Warning</c>: yellow</item>
///       <item><c>Error</c>: red bold</item>
///       <item><c>Critical</c>: white on red</item>
///     </list></para>
///     <para><b>Why not use SimpleConsoleFormatter.</b> The default
///     formatter emits ASCII; we want ANSI colors that look at home
///     next to the Plexor CLI banner (which already uses Spectre).
///     The cost is one extra package reference (<c>Spectre.Console</c>,
///     transitively already present in <c>Plexor.Shared.Console</c>)
///     and one extra formatter class.</para>
/// </remarks>
public sealed class PlexorConsoleFormatter : ConsoleFormatter
{
    /// <summary>The formatter name registered with
    /// <see cref="ConsoleFormatterNames" />.</summary>
    public const string FormatterName = "plexor";

    /// <summary>Default constructor — required by
    /// <see cref="ConsoleFormatterNames" />'s <c>AddConsoleFormatter</c>.</summary>
    public PlexorConsoleFormatter() : base(FormatterName)
    {
    }

    /// <inheritdoc />
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var line = FormatLine(logEntry);
        AnsiConsole.WriteLine(line);
    }

    private static string FormatLine<TState>(in LogEntry<TState> logEntry)
    {
        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        var levelMarkup = ColorFor(logEntry.LogLevel);
        var category = Markup.Escape(logEntry.Category);
        var message = Markup.Escape(logEntry.Formatter(logEntry.State, logEntry.Exception));

        var sb = new System.Text.StringBuilder(96);
        sb.Append("[grey]").Append(timestamp).Append("[/] ")
            .Append(levelMarkup).Append('[').Append(LevelText(logEntry.LogLevel)).Append(']').Append("[/] ")
            .Append("[white]{").Append(category).Append("}[/]: ")
            .Append(message);

        if (logEntry.Exception is { } ex)
        {
            sb.AppendLine()
                .Append("    [red dim]").Append(Markup.Escape(ex.GetType().Name)).Append("[/]")
                .Append(": [grey]").Append(Markup.Escape(ex.Message)).Append("[/]");
        }

        return sb.ToString();
    }

    private static string LevelText(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            LogLevel.None => "---",
            _ => level.ToString().ToUpperInvariant()[..Math.Min(3, level.ToString().Length)],
        };
    }

    /// <summary>Spectre markup for the level tag.</summary>
    /// <param name="level"></param>
    private static string ColorFor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "[grey]",
            LogLevel.Debug => "[grey]",
            LogLevel.Information => "[cyan]",
            LogLevel.Warning => "[yellow]",
            LogLevel.Error => "[bold red]",
            LogLevel.Critical => "[white on red]",
            _ => "[white]",
        };
    }
}
