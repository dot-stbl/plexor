using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Plexor.Shared.Telemetry;

/// <summary>
///     DI extension that registers the Plexor-styled console logger
///     (<see cref="PlexorConsoleFormatter" />) on the host's logging
///     pipeline.
/// </summary>
/// <remarks>
///     <para><b>What it does.</b> Replaces the default
///     <see cref="ConsoleFormatterNames.Simple" /> formatter with
///     <see cref="PlexorConsoleFormatter.FormatterName" />. The
///     built-in ASP.NET Core loggers + sinks continue to work — only
///     the formatter changes.</para>
///     <para><b>Usage</b>:
///     <code>
/// builder.Logging.AddPlexorConsole();
///     </code></para>
///     <para><b>Why a custom formatter.</b> The default formatter
///     emits plain ASCII; we want ANSI colors that match the Plexor
///     CLI banner (which already uses Spectre.Console). The colors map
///     to log level so ops can spot severity at a glance.</para>
/// </remarks>
public static class PlexorHostExtensions
{
    /// <summary>
    ///     Register <see cref="PlexorConsoleFormatter" /> as the console
    ///     formatter. Calling this multiple times is harmless (the
    ///     formatter registration is idempotent by name).
    /// </summary>
    /// <param name="builder">Logging builder.</param>
    /// <returns>The same <paramref name="builder" /> for chaining.</returns>
    public static ILoggingBuilder AddPlexorConsole(this ILoggingBuilder builder)
    {
        builder.AddConsoleFormatter<PlexorConsoleFormatter, SimpleConsoleFormatterOptions>(
            static options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "HH:mm:ss.fff";
                options.UseUtcTimestamp = false;
            });

        // Drop the default simple formatter so only the Plexor one
        // writes to the console. (Built-in loggers with explicit names
        // continue to work; only the formatter swap matters here.)
        builder.AddSimpleConsole();

        return builder;
    }
}
