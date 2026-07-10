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
//     holders with public fields. The builder methods mutate the
//     content, not the builder. The content can be inspected,
//     tested, and passed to helpers without dragging the builder.
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
/// Mutable state of <see cref="PlexorCliBuilder"/>. Fields are
/// <c>public</c> because the type is <c>internal</c> — the access
/// modifier means "within this assembly, anyone holding a reference
/// may set fields directly". Builder methods provide the public API.
/// </summary>
internal sealed class PlexorCliContent
{
    /// <summary>Raw command-line arguments passed to the CLI.</summary>
    public string[] Args = [];

    /// <summary>ASCII banner text rendered before the first command.
    /// <c>null</c> or empty means no banner.</summary>
    public string? BannerText;

    /// <summary>Program name used in help and error messages.</summary>
    public string? ToolName;

    /// <summary>Version string used for <c>--version</c>.</summary>
    public string? ToolVersion;

    /// <summary>Cluster context surfaced in the status footer.</summary>
    public string? ClusterName;

    /// <summary>Node context surfaced in the status footer.</summary>
    public string? NodeName;

    /// <summary>Deferred <see cref="IConfigurator"/> actions,
    /// flushed during <see cref="PlexorCliBuilder.Run"/>.</summary>
    public List<Action<IConfigurator>> PendingConfigurations = new();
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
    public PlexorCliBuilder AddCommand<TCommand>(string name, Action<ICommandConfigurator>? configure = null)
        where TCommand : class, ICommandLimiter<CommandSettings>, new()
    {
        Content.PendingConfigurations.Add(c =>
        {
            var cmd = c.AddCommand<TCommand>(name);
            configure?.Invoke(cmd);
        });
        return this;
    }

    /// <summary>Add a delegate command at the root level. Used
    /// for one-shot commands that don't need a full class.</summary>
    public PlexorCliBuilder AddDelegate(string name, Func<CommandContext, int> handler)
    {
        Content.PendingConfigurations.Add(c => _ = c.AddDelegate(name, handler));
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
            PrintBanner();

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
            PrintFooter();
            return exit;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(ErrorFormatter.Error(ex.GetType().Name, ex.Message));
            return 1;
        }
    }

    private void PrintBanner()
    {
        if (!string.IsNullOrEmpty(Content.BannerText))
        {
            AnsiConsole.Write(AsciiBanner.Custom(Content.BannerText));
            AnsiConsole.WriteLine();
        }
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
/// Mutable state of <see cref="PlexorBranchBuilder"/>. Public fields
/// because the type is internal — see <see cref="PlexorCliContent"/>
/// for the rationale.
/// </summary>
internal sealed class PlexorBranchContent
{
    /// <summary>The branch name (e.g. <c>"cluster"</c>).</summary>
    public string Name = string.Empty;

    /// <summary>Aliases configured via <see cref="PlexorBranchBuilder.WithAlias"/>.</summary>
    public List<string> Aliases = new();

    /// <summary>Deferred command configurations, flushed when the
    /// parent builder's <see cref="PlexorCliBuilder.Run"/> executes.</summary>
    public List<Action<IConfigurator<CommandSettings>>> PendingConfigurations = new();

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