// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCli — static entry point + fluent builder for every Plexor
// CLI binary. Wraps Spectre.Console.Cli with Plexor DS defaults
// (banner, footer, color-aware exception formatting).
//
// Architecture (per .agents/rules/coding/code-shape.md §12):
//   - PlexorCliBuilder / PlexorBranchBuilder are public sealed
//     classes — the API surface.
//   - PlexorCliContent / PlexorBranchContent are internal data
//     holders with public auto-properties. The builder methods
//     mutate the content, not the builder. The content can be
//     inspected, tested, and passed to helpers without dragging
//     the builder. Auto-properties (not fields) per project style
//     + CA1051 (see code-shape.md §12).
//
// Usage in Program.cs:
//
//     return PlexorCli.New(args)
//         .Name("plx")
//         .Version("0.2.1")
//         .SetBanner("PLEXOR")
//         .ForCluster("prod-cluster")
//         .AddCommand<InitCommand>("init", cmd => cmd
//             .WithDescription("Bootstrap a Plexor cluster on this host"))
//         .AddBranch("cluster", b => b
//             .AddCommand<ClusterListCommand>("list", cmd => cmd
//                 .WithAlias("ls")
//                 .WithDescription("List registered clusters")))
//         .Run();
//
// AOT contract:
//   - PlexorCli.New(args) builds the CommandApp at startup. Commands
//     are added by their closed type — no string-based reflection.
//   - Each command type must implement ICommand<TSettings> from
//     Spectre.Console.Cli; the settings type must have a public
//     parameterless ctor or be a record with init properties.
//   - Commands inside branches use CommandSettings (Spectre's
//     typed-configurator constraint). Custom-settings branches
//     deferred — out of scope for v0.1.
// ============================================================================

using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Shared.Console;

/// <summary>
/// Static factory for a Plexor CLI. Call <see cref="New"/> once at
/// the start of <c>Program.cs</c>, chain the fluent methods, and
/// end with <see cref="PlexorCliBuilder.Run"/>.
/// </summary>
public static class PlexorCli
{
    /// <summary>Begin building a new CLI.</summary>
    /// <param name="args">Raw command-line arguments (typically
    /// passed straight from <c>Main</c>).</param>
    public static PlexorCliBuilder New(string[] args)
    {
        return new PlexorCliBuilder(args);
    }
}

/// <summary>
/// Mutable state of <see cref="PlexorCliBuilder"/>. Properties are
/// <c>public</c> auto-properties because the type is <c>internal</c> —
/// the access means "within this assembly, anyone holding a
/// reference may read or write these properties". Builder methods
/// provide the typed API; the properties themselves are not part of
/// the public surface.
/// </summary>
internal sealed class PlexorCliContent
{
    /// <summary>Raw command-line arguments passed to the CLI.</summary>
    public string[] Args { get; set; } = [];

    /// <summary>ASCII banner text rendered before the first command.
    /// <c>null</c> or empty means no banner.</summary>
    public string? BannerText { get; set; }

    /// <summary>Explicit tagline shown under the banner or in the
    /// compact mark. <c>null</c> means derive from
    /// <see cref="ClusterName"/> / <see cref="NodeName"/> / default.</summary>
    public string? Tagline { get; set; }

    /// <summary>Program name used in help and error messages.</summary>
    public string? ToolName { get; set; }

    /// <summary>Version string used for <c>--version</c>.</summary>
    public string? ToolVersion { get; set; }

    /// <summary>Cluster context surfaced in the status footer.</summary>
    public string? ClusterName { get; set; }

    /// <summary>Node context surfaced in the status footer.</summary>
    public string? NodeName { get; set; }

    /// <summary>Deferred <see cref="IConfigurator"/> actions,
    /// flushed during <see cref="PlexorCliBuilder.Run"/>.</summary>
    public List<Action<IConfigurator>> PendingConfigurations { get; set; } = [];

    /// <summary>Metadata for every command registered via
    /// <c>AddCommand</c> / <c>AddDelegate</c>. Used by
    /// <see cref="PlexorCliBuilder.PrintBanner"/> to render the
    /// help-banner command list without re-parsing the
    /// Spectre configuration lambdas.</summary>
    public List<CommandSpec> RegisteredCommands { get; set; } = [];
}

/// <summary>
/// Fluent builder for a Plexor CLI. Holds the accumulated command
/// tree; <see cref="Run"/> builds the underlying
/// <see cref="CommandApp"/>, applies the configuration, and runs it.
/// State lives on <see cref="Content"/>; methods mutate it.
/// </summary>
public sealed class PlexorCliBuilder
{
    /// <summary>Mutable state of the builder. Internal data shape
    /// shared with the branch builder and runner.</summary>
    internal PlexorCliContent Content { get; }

    internal PlexorCliBuilder(string[] args)
    {
        Content = new PlexorCliContent
        {
            Args = args,
        };
    }

    /// <summary>Set the program name (used in help / error
    /// messages). Defaults to <c>"plx"</c>.</summary>
    public PlexorCliBuilder Name(string toolName)
    {
        Content.ToolName = toolName;
        return this;
    }

    /// <summary>Set the version string used for <c>--version</c>.
    /// No leading <c>v</c> prefix — the CLI renders the version
    /// with a leading <c>v</c> on display.</summary>
    public PlexorCliBuilder Version(string toolVersion)
    {
        Content.ToolVersion = toolVersion;
        return this;
    }

    /// <summary>Render an ASCII banner before the first command's
    /// output. Set to <c>null</c> to skip.</summary>
    public PlexorCliBuilder SetBanner(string? bannerText)
    {
        Content.BannerText = bannerText;
        return this;
    }

    /// <summary>Set the explicit tagline shown under the banner or
    /// in the compact mark. If unset, the tagline is derived from
    /// <see cref="ForCluster"/> / <see cref="ForNode"/> / default.</summary>
    public PlexorCliBuilder Tagline(string? tagline)
    {
        Content.Tagline = tagline;
        return this;
    }

    /// <summary>Cluster context for the status footer (optional).
    /// Surfaces the active cluster name at the end of each
    /// command's output.</summary>
    public PlexorCliBuilder ForCluster(string? clusterName)
    {
        Content.ClusterName = clusterName;
        return this;
    }

    /// <summary>Node context for the status footer (optional).
    /// Surfaces the active node name at the end of each command's
    /// output.</summary>
    public PlexorCliBuilder ForNode(string? nodeName)
    {
        Content.NodeName = nodeName;
        return this;
    }

    /// <summary>Add a command at the root level. The optional
    /// <paramref name="configure"/> lambda chains Spectre
    /// configuration methods (description, alias, examples, ...).</summary>
    /// <typeparam name="TCommand">Closed command type that
    /// implements <see cref="ICommand{T}"/> for some settings.</typeparam>
    /// <param name="icon">Single-character Unicode glyph shown next
    /// to the command name in the help-banner command list.</param>
    /// <param name="description">One-line description shown in the
    /// help table and the help banner.</param>
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

    /// <summary>Add a delegate command at the root level. Used
    /// for one-shot commands that don't need a full class.</summary>
    /// <param name="icon">Single-character Unicode glyph shown next
    /// to the command name in the help-banner command list.</param>
    /// <param name="description">One-line description shown in the
    /// help table and the help banner.</param>
    public PlexorCliBuilder AddDelegate(
        string name,
        string icon,
        string description,
        Func<CommandContext, int> handler)
    {
        Content.RegisteredCommands.Add(new CommandSpec(icon, name, description));
        Content.PendingConfigurations.Add(c =>
        {
            _ = c.AddDelegate(name, handler).WithDescription(description);
        });
        return this;
    }

    /// <summary>Begin a sub-command branch (e.g. <c>plx cluster ls</c>).
    /// The branch lambda configures commands under the branch.</summary>
    public PlexorCliBuilder AddBranch(string name, Action<PlexorBranchBuilder> configure)
    {
        var branch = new PlexorBranchBuilder(name);
        configure(branch);
        Content.PendingConfigurations.Add(c =>
        {
            var branchConfigurator = c.AddBranch(name, branchConfigurator =>
            {
                branch.Content.ApplyCommandsTo(branchConfigurator);
            });

            // Aliases hang off the returned IBranchConfigurator.
            foreach (var alias in branch.Content.Aliases)
            {
                _ = branchConfigurator.WithAlias(alias);
            }
        });
        return this;
    }

    /// <summary>Build, run, and return the exit code. Suitable for
    /// <c>return PlexorCli.New(args)...Run();</c>.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Top-level catch wraps any failure in a Plexor-formatted error.")]
    public int Run()
    {
        try
        {
            var isHelpLike = DetectHelpLike();
            PrintBanner(isHelpLike);

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
                PrintFooter();
            }

            return exit;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(ErrorFormatter.Error(ex.GetType().Name, ex.Message));
            return 1;
        }
    }

    /// <summary>Decide whether the current invocation is
    /// informational (help / version / no args) or an actual
    /// command execution. Informational invocations get the big
    /// banner; real commands get the compact mark.</summary>
    private bool DetectHelpLike()
    {
        if (Content.Args.Length == 0)
        {
            return true;
        }

        var first = Content.Args[0];
        return first is "--help" or "-h" or "--version" or "-V" or "version" or "v" or "help";
    }

    private void PrintBanner(bool isHelpLike)
    {
        var toolName = Content.ToolName ?? "plexor";
        var version = Content.ToolVersion ?? "0.0.0";
        var tagline = ResolvePlainTagline();

        if (isHelpLike)
        {
            // Help-like: full boxed banner with logo, version, tagline,
            // and the command list.
            AnsiConsole.MarkupLine(BannerArt.FullHelpBanner(
                toolName,
                version,
                tagline,
                Content.RegisteredCommands));
        }
        else
        {
            // Real command: one-line compact mark.
            AnsiConsole.MarkupLine(BannerArt.CompactMark(toolName, version, tagline));
        }
    }

    /// <summary>Resolve the tagline as plain text (no markup),
    /// used by the banner renderer.</summary>
    private string ResolvePlainTagline()
    {
        if (!string.IsNullOrEmpty(Content.Tagline))
        {
            return Content.Tagline;
        }

        if (Content.ClusterName is not null)
        {
            return $"for cluster {Content.ClusterName}";
        }

        if (Content.NodeName is not null)
        {
            return $"for node {Content.NodeName}";
        }

        return "self-hosted cloud platform";
    }

    private string ResolveTagline()
    {
        if (!string.IsNullOrEmpty(Content.Tagline))
        {
            return MarkupExtensions.Muted(Content.Tagline);
        }

        if (Content.ClusterName is not null)
        {
            return MarkupExtensions.Muted($"for cluster {Content.ClusterName}");
        }

        if (Content.NodeName is not null)
        {
            return MarkupExtensions.Muted($"for node {Content.NodeName}");
        }

        return MarkupExtensions.Muted("self-hosted cloud platform");
    }

    private void PrintFooter()
    {
        if (Content.ToolName is null && Content.ToolVersion is null &&
            Content.ClusterName is null && Content.NodeName is null)
        {
            return;
        }

        var footer = new StatusFooter(
            ToolName: Content.ToolName ?? "plx",
            Version: Content.ToolVersion ?? "0.0.0",
            ClusterName: Content.ClusterName,
            NodeName: Content.NodeName);
        AnsiConsole.MarkupLine(footer.Render());
    }
}

/// <summary>
/// Mutable state of <see cref="PlexorBranchBuilder"/>. Properties are
/// <c>public</c> auto-properties because the type is <c>internal</c> —
/// see <see cref="PlexorCliContent"/> for the rationale.
/// </summary>
internal sealed class PlexorBranchContent
{
    /// <summary>The branch name (e.g. <c>"cluster"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Aliases configured via <see cref="PlexorBranchBuilder.WithAlias"/>.</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>Deferred command configurations, flushed when the
    /// parent builder's <see cref="PlexorCliBuilder.Run"/> executes.</summary>
    public List<Action<IConfigurator<CommandSettings>>> PendingConfigurations { get; set; } = [];

    /// <summary>Apply the pending command configurations to the
    /// supplied Spectre branch configurator.</summary>
    public void ApplyCommandsTo(IConfigurator<CommandSettings> target)
    {
        foreach (var cfg in PendingConfigurations)
        {
            cfg(target);
        }
    }
}

/// <summary>
/// Fluent builder for a sub-command branch. Returned by
/// <see cref="PlexorCliBuilder.AddBranch"/>. Holds deferred
/// configurations that are flushed into the underlying
/// <see cref="IConfigurator{T}"/> when the parent builder's
/// <see cref="PlexorCliBuilder.Run"/> executes.
/// </summary>
public sealed class PlexorBranchBuilder
{
    /// <summary>Mutable state of the branch builder.</summary>
    internal PlexorBranchContent Content { get; }

    internal PlexorBranchBuilder(string name)
    {
        Content = new PlexorBranchContent
        {
            Name = name,
        };
    }

    /// <summary>The branch name (e.g. <c>"cluster"</c>).</summary>
    public string Name => Content.Name;

    /// <summary>Add an alias for the branch itself (e.g. <c>"c"</c>
    /// for <c>plx cluster ls</c>).</summary>
    public PlexorBranchBuilder WithAlias(string alias)
    {
        Content.Aliases.Add(alias);
        return this;
    }

    /// <summary>Add a command to this branch. The optional
    /// <paramref name="configure"/> lambda chains Spectre
    /// configuration methods (description, alias, examples).</summary>
    public PlexorBranchBuilder AddCommand<TCommand>(string commandName, Action<ICommandConfigurator>? configure = null)
        where TCommand : class, ICommandLimiter<CommandSettings>, new()
    {
        Content.PendingConfigurations.Add(c =>
        {
            var cmd = c.AddCommand<TCommand>(commandName);
            configure?.Invoke(cmd);
        });
        return this;
    }

    /// <summary>Add a nested branch (e.g. <c>plx cluster node ls</c>).</summary>
    public PlexorBranchBuilder AddBranch(string nestedName, Action<PlexorBranchBuilder> configure)
    {
        var nested = new PlexorBranchBuilder(nestedName);
        configure(nested);
        Content.PendingConfigurations.Add(c =>
        {
            var branchConfigurator = c.AddBranch(nestedName, inner =>
            {
                nested.Content.ApplyCommandsTo(inner);
            });

            foreach (var alias in nested.Content.Aliases)
            {
                _ = branchConfigurator.WithAlias(alias);
            }
        });
        return this;
    }
}
