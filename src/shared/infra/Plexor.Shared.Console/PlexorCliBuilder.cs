// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCliBuilder — fluent builder for a Plexor CLI. Holds the
// accumulated command tree; Run() builds the underlying CommandApp,
// applies the configuration, and runs it. State lives on Content;
// methods mutate it.
//
// Helpers (DetectHelpLike, PrintBanner, ResolvePlainTagline,
// PrintFooter) live in CliBuilderHelpers — file-static, per
// code-shape.md §1a (private methods on production classes are
// banned; helpers without DI go to a file-static class).
//
// Extracted from CliAppBuilder.cs (Sprint 3, item 3 — 1 type per
// file per folder-organization.md §1).
// ============================================================================

using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Shared.Console;

/// <summary>
///     Fluent builder for a Plexor CLI. Holds the accumulated command
///     tree; <see cref="Run" /> builds the underlying
///     <see cref="CommandApp" />, applies the configuration, and
///     runs it. State lives on <see cref="Content" />; methods
///     mutate it.
/// </summary>
public sealed class PlexorCliBuilder
{
    internal PlexorCliBuilder(string[] args)
    {
        Content = new PlexorCliContent
        {
            Args = args
        };
    }

    /// <summary>
    ///     Mutable state of the builder. Internal data shape shared
    ///     with the branch builder and runner.
    /// </summary>
    internal PlexorCliContent Content { get; }

    /// <summary>
    ///     Set the program name (used in help / error messages).
    ///     Defaults to <c>"plx"</c>.
    /// </summary>
    /// <param name="toolName"></param>
    public PlexorCliBuilder Name(string toolName)
    {
        Content.ToolName = toolName;
        return this;
    }

    /// <summary>
    ///     Set the version string used for <c>--version</c>. No
    ///     leading <c>v</c> prefix — the CLI renders the version
    ///     with a leading <c>v</c> on display.
    /// </summary>
    /// <param name="toolVersion"></param>
    public PlexorCliBuilder Version(string toolVersion)
    {
        Content.ToolVersion = toolVersion;
        return this;
    }

    /// <summary>
    ///     Render an ASCII banner before the first command's output.
    ///     Set to <c>null</c> to skip.
    /// </summary>
    /// <param name="bannerText"></param>
    public PlexorCliBuilder SetBanner(string? bannerText)
    {
        Content.BannerText = bannerText;
        return this;
    }

    /// <summary>
    ///     Set the explicit tagline shown under the banner or in
    ///     the compact mark. If unset, the tagline is derived from
    ///     <see cref="ForCluster" /> / <see cref="ForNode" /> /
    ///     default.
    /// </summary>
    /// <param name="tagline"></param>
    public PlexorCliBuilder Tagline(string? tagline)
    {
        Content.Tagline = tagline;
        return this;
    }

    /// <summary>
    ///     Cluster context for the status footer (optional).
    ///     Surfaces the active cluster name at the end of each
    ///     command's output.
    /// </summary>
    /// <param name="clusterName"></param>
    public PlexorCliBuilder ForCluster(string? clusterName)
    {
        Content.ClusterName = clusterName;
        return this;
    }

    /// <summary>
    ///     Node context for the status footer (optional). Surfaces
    ///     the active node name at the end of each command's
    ///     output.
    /// </summary>
    /// <param name="nodeName"></param>
    public PlexorCliBuilder ForNode(string? nodeName)
    {
        Content.NodeName = nodeName;
        return this;
    }

    /// <summary>
    ///     Add a command at the root level. The optional
    ///     <paramref name="configure" /> lambda chains Spectre
    ///     configuration methods (description, alias, examples, ...).
    /// </summary>
    /// <typeparam name="TCommand">
    ///     Closed command type that implements
    ///     <see cref="ICommand{T}" /> for some settings.
    /// </typeparam>
    /// <param name="name"></param>
    /// <param name="icon">
    ///     Single-character Unicode glyph shown next to the command
    ///     name in the help-banner command list.
    /// </param>
    /// <param name="description">
    ///     One-line description shown in the help table and the help
    ///     banner.
    /// </param>
    /// <param name="configure"></param>
    public PlexorCliBuilder AddCommand<TCommand>(
        string name,
        string icon,
        string description,
        Action<ICommandConfigurator>? configure = null)
            where TCommand : class, ICommandLimiter<CommandSettings>, new()
    {
        Content.RegisteredCommands.Add(new CommandSpec(icon, name, description));
        Content.PendingConfigurations.Add(c =>
        {
            var cmd = c.AddCommand<TCommand>(name).WithDescription(description);
            configure?.Invoke(cmd);
        });

        return this;
    }

    /// <summary>
    ///     Add a delegate command at the root level. Used for
    ///     one-shot commands that don't need a full class.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="icon">
    ///     Single-character Unicode glyph shown next to the command
    ///     name in the help-banner command list.
    /// </param>
    /// <param name="description">
    ///     One-line description shown in the help table and the
    ///     help banner.
    /// </param>
    /// <param name="handler"></param>
    public PlexorCliBuilder AddDelegate(
        string name,
        string icon,
        string description,
        Func<CommandContext, int> handler)
    {
        Content.RegisteredCommands.Add(new CommandSpec(icon, name, description));
        Content.PendingConfigurations.Add(c => _ = c.AddDelegate(name, handler).WithDescription(description));

        return this;
    }

    /// <summary>
    ///     Begin a sub-command branch (e.g. <c>plx cluster ls</c>).
    ///     The branch lambda configures commands under the branch.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="configure"></param>
    public PlexorCliBuilder AddBranch(string name, Action<PlexorBranchBuilder> configure)
    {
        var branch = new PlexorBranchBuilder(name);
        configure(branch);
        Content.PendingConfigurations.Add(c =>
        {
            var branchConfigurator = c.AddBranch(name,
                branchConfigurator => branch.Content.ApplyCommandsTo(branchConfigurator));

            // Aliases hang off the returned IBranchConfigurator.
            foreach (var alias in branch.Content.Aliases)
            {
                _ = branchConfigurator.WithAlias(alias);
            }
        });

        return this;
    }

    /// <summary>
    ///     Build, run, and return the exit code. Suitable for
    ///     <c>return PlexorCli.New(args)...Run();</c>.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Top-level catch wraps any failure in a Plexor-formatted error.")]
    public int Run()
    {
        try
        {
            var isHelpLike = CliBuilderHelpers.DetectHelpLike(Content.Args);
            CliBuilderHelpers.PrintBanner(Content);

            var app = new CommandApp();
            app.Configure(c =>
            {
                if (Content.ToolName is not null)
                {
                    _ = c.SetApplicationName(Content.ToolName);
                }

                if (Content.ToolVersion is not null)
                {
                    _ = c.SetApplicationVersion(Content.ToolVersion);
                }

                _ = c.SetExceptionHandler((ex, _) =>
                {
                    AnsiConsole.MarkupLine(ErrorFormatter.Error(ex.GetType().Name, ex.Message));
                    return -1;
                });

                foreach (var cfg in Content.PendingConfigurations)
                {
                    cfg(c);
                }
            });

            var exit = app.Run(Content.Args);

            if (!isHelpLike)
            {
                CliBuilderHelpers.PrintFooter(Content);
            }

            return exit;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(ErrorFormatter.Error(ex.GetType().Name, ex.Message));
            return 1;
        }
    }
}

/// <summary>
///     File-static helpers used by <see cref="PlexorCliBuilder.Run" />.
///     File-scoped per code-shape.md §1a (private methods on
///     production classes are banned; helpers without DI go to a
///     file-static class next to their only caller).
/// </summary>
file static class CliBuilderHelpers
{
    /// <summary>
    ///     Decide whether the current invocation is informational
    ///     (help / version / no args) or an actual command
    ///     execution. Informational invocations get the big banner;
    ///     real commands get the compact mark.
    /// </summary>
    /// <param name="args"></param>
    public static bool DetectHelpLike(string[] args)
    {
        if (args.Length == 0)
        {
            return true;
        }

        var first = args[0];
        return first is "--help" or "-h" or "--version" or "-V" or "version" or "v" or "help";
    }

    /// <summary>Render either the full help banner or the compact mark.</summary>
    /// <param name="content"></param>
    public static void PrintBanner(PlexorCliContent content)
    {
        var toolName = content.ToolName ?? "plexor";
        var version = content.ToolVersion ?? "0.0.0";
        var tagline = ResolvePlainTagline(content);

        if (DetectHelpLike(content.Args))
        {
            // Help-like: full boxed banner with logo, version, tagline,
            // and the command list.
            AnsiConsole.MarkupLine(BannerArt.FullHelpBanner(
                toolName,
                version,
                tagline,
                content.RegisteredCommands));
        }
        else
        {
            // Real command: one-line compact mark.
            AnsiConsole.MarkupLine(BannerArt.CompactMark(toolName, version, tagline));
        }
    }

    /// <summary>
    ///     Resolve the tagline as plain text (no markup), used by the
    ///     banner renderer.
    /// </summary>
    /// <param name="content"></param>
    public static string ResolvePlainTagline(PlexorCliContent content)
    {
        if (!string.IsNullOrEmpty(content.Tagline))
        {
            return content.Tagline;
        }

        if (content.ClusterName is not null)
        {
            return $"for cluster {content.ClusterName}";
        }

        if (content.NodeName is not null)
        {
            return $"for node {content.NodeName}";
        }

        return "self-hosted cloud platform";
    }

    /// <summary>Render the status footer at end of command output.</summary>
    /// <param name="content"></param>
    public static void PrintFooter(PlexorCliContent content)
    {
        if (content.ToolName is null && content.ToolVersion is null &&
            content.ClusterName is null && content.NodeName is null)
        {
            return;
        }

        var footer = new StatusFooter(
            content.ToolName ?? "plx",
            content.ToolVersion ?? "0.0.0",
            content.ClusterName,
            content.NodeName);

        AnsiConsole.MarkupLine(footer.Render());
    }
}
