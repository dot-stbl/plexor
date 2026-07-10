// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCli — static entry point + fluent builder for every Plexor
// CLI binary. Wraps Spectre.Console.Cli with Plexor DS defaults
// (banner, footer, color-aware exception formatting).
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
//   - Commands inside branches use CommandSettings as their settings
//     type by default (Spectre's constraint on the typed configurator).
//     Custom settings branches need a typed branch overload — out of
//     scope for the first iteration.
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
/// Fluent builder for a Plexor CLI. Holds the accumulated command
/// tree; <see cref="Run"/> builds the underlying
/// <see cref="CommandApp"/>, applies the configuration, and runs it.
/// </summary>
public sealed class PlexorCliBuilder
{
    private readonly string[] args;
    private readonly CommandApp app = new();
    private readonly List<Action<IConfigurator>> pendingConfigurations = new();
    private string? bannerText;
    private string? toolName;
    private string? toolVersion;
    private string? clusterName;
    private string? nodeName;

    internal PlexorCliBuilder(string[] args)
    {
        this.args = args;
    }

    /// <summary>Set the program name (used in help / error
    /// messages). Defaults to <c>"plx"</c>.</summary>
    public PlexorCliBuilder Name(string name)
    {
        toolName = name;
        return this;
    }

    /// <summary>Set the version string used for <c>--version</c>.
    /// No leading <c>v</c> prefix — the CLI renders the version
    /// with a leading <c>v</c> on display.</summary>
    public PlexorCliBuilder Version(string version)
    {
        toolVersion = version;
        return this;
    }

    /// <summary>Render an ASCII banner before the first command's
    /// output. Set to <c>null</c> to skip.</summary>
    public PlexorCliBuilder SetBanner(string? bannerText)
    {
        this.bannerText = bannerText;
        return this;
    }

    /// <summary>Cluster context for the status footer (optional).
    /// Surfaces the active cluster name at the end of each
    /// command's output.</summary>
    public PlexorCliBuilder ForCluster(string? clusterName)
    {
        this.clusterName = clusterName;
        return this;
    }

    /// <summary>Node context for the status footer (optional).
    /// Surfaces the active node name at the end of each command's
    /// output.</summary>
    public PlexorCliBuilder ForNode(string? nodeName)
    {
        this.nodeName = nodeName;
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
        pendingConfigurations.Add(c =>
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
        pendingConfigurations.Add(c => _ = c.AddDelegate(name, handler));
        return this;
    }

    /// <summary>Begin a sub-command branch (e.g. <c>plx cluster ls</c>).
    /// The branch lambda configures commands under the branch.</summary>
    public PlexorCliBuilder AddBranch(string name, Action<PlexorBranchBuilder> configure)
    {
        var branch = new PlexorBranchBuilder(name);
        configure(branch);
        pendingConfigurations.Add(c =>
        {
            var branchConfigurator = c.AddBranch(name, branchConfigurator =>
            {
                branch.ApplyCommandsTo(branchConfigurator);
            });

            // Aliases hang off the returned IBranchConfigurator.
            foreach (var alias in branch.Aliases)
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

            app.Configure(c =>
            {
                if (toolName is not null)
                {
                    _ = c.SetApplicationName(toolName);
                }

                if (toolVersion is not null)
                {
                    _ = c.SetApplicationVersion(toolVersion);
                }

                _ = c.SetExceptionHandler((ex, _) =>
                {
                    AnsiConsole.MarkupLine(ErrorFormatter.Error(ex.GetType().Name, ex.Message));
                    return -1;
                });

                foreach (var cfg in pendingConfigurations)
                {
                    cfg(c);
                }
            });

            var exit = app.Run(args);
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
        if (!string.IsNullOrEmpty(bannerText))
        {
            AnsiConsole.Write(AsciiBanner.Custom(bannerText));
            AnsiConsole.WriteLine();
        }
    }

    private void PrintFooter()
    {
        if (toolName is null && toolVersion is null && clusterName is null && nodeName is null)
        {
            return;
        }

        var footer = new StatusFooter(
            ToolName: toolName ?? "plx",
            Version: toolVersion ?? "0.0.0",
            ClusterName: clusterName,
            NodeName: nodeName);
        AnsiConsole.MarkupLine(footer.Render());
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
    private readonly string name;
    private readonly List<Action<IConfigurator<CommandSettings>>> pendingConfigurations = new();
    private readonly List<string> aliases = new();

    internal PlexorBranchBuilder(string name)
    {
        this.name = name;
    }

    /// <summary>The branch name (e.g. <c>"cluster"</c>).</summary>
    public string Name => name;

    /// <summary>The aliases configured for this branch. Read-only;
    /// callers configure aliases via <see cref="WithAlias"/>.</summary>
    public IReadOnlyList<string> Aliases => aliases;

    /// <summary>Add an alias for the branch itself (e.g. <c>"c"</c>
    /// for <c>plx cluster ls</c>).</summary>
    public PlexorBranchBuilder WithAlias(string alias)
    {
        aliases.Add(alias);
        return this;
    }

    /// <summary>Add a command to this branch. The optional
    /// <paramref name="configure"/> lambda chains Spectre
    /// configuration methods (description, alias, examples).</summary>
    public PlexorBranchBuilder AddCommand<TCommand>(string commandName, Action<ICommandConfigurator>? configure = null)
        where TCommand : class, ICommandLimiter<CommandSettings>, new()
    {
        pendingConfigurations.Add(c =>
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
        pendingConfigurations.Add(c =>
        {
            var branchConfigurator = c.AddBranch(nestedName, inner =>
            {
                nested.ApplyCommandsTo(inner);
            });

            foreach (var alias in nested.Aliases)
            {
                _ = branchConfigurator.WithAlias(alias);
            }
        });
        return this;
    }

    /// <summary>Apply the pending command configurations (not
    /// aliases — those are applied on the parent branch's returned
    /// configurator) to the supplied Spectre branch configurator.</summary>
    internal void ApplyCommandsTo(IConfigurator<CommandSettings> target)
    {
        foreach (var cfg in pendingConfigurations)
        {
            cfg(target);
        }
    }
}