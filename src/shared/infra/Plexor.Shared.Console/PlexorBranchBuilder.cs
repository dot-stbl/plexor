// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorBranchBuilder — fluent builder for a sub-command branch.
// Returned by PlexorCliBuilder.AddBranch. Holds deferred
// configurations that are flushed into the underlying IConfigurator<T>
// when the parent builder's PlexorCliBuilder.Run executes.
//
// Extracted from CliAppBuilder.cs (Sprint 3, item 3 — 1 type per
// file per folder-organization.md §1).
// ============================================================================

using Spectre.Console.Cli;

namespace Plexor.Shared.Console;

/// <summary>
///     Fluent builder for a sub-command branch. Returned by
///     <see cref="PlexorCliBuilder.AddBranch" />. Holds deferred
///     configurations that are flushed into the underlying
///     <see cref="IConfigurator{T}" /> when the parent builder's
///     <see cref="PlexorCliBuilder.Run" /> executes.
/// </summary>
public sealed class PlexorBranchBuilder
{
    internal PlexorBranchBuilder(string name)
    {
        Content = new PlexorBranchContent
        {
            Name = name
        };
    }

    /// <summary>Mutable state of the branch builder.</summary>
    internal PlexorBranchContent Content { get; }

    /// <summary>The branch name (e.g. <c>"cluster"</c>).</summary>
    public string Name => Content.Name;

    /// <summary>
    ///     Add an alias for the branch itself (e.g. <c>"c"</c> for
    ///     <c>plx cluster ls</c>).
    /// </summary>
    /// <param name="alias"></param>
    public PlexorBranchBuilder WithAlias(string alias)
    {
        Content.Aliases.Add(alias);
        return this;
    }

    /// <summary>
    ///     Add a command to this branch. The optional
    ///     <paramref name="configure" /> lambda chains Spectre
    ///     configuration methods (description, alias, examples).
    /// </summary>
    /// <typeparam name="TCommand"></typeparam>
    /// <param name="commandName"></param>
    /// <param name="configure"></param>
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
    /// <param name="nestedName"></param>
    /// <param name="configure"></param>
    public PlexorBranchBuilder AddBranch(string nestedName, Action<PlexorBranchBuilder> configure)
    {
        var nested = new PlexorBranchBuilder(nestedName);
        configure(nested);
        Content.PendingConfigurations.Add(c =>
        {
            var branchConfigurator = c.AddBranch(nestedName,
                inner => nested.Content.ApplyCommandsTo(inner));

            foreach (var alias in nested.Content.Aliases)
            {
                _ = branchConfigurator.WithAlias(alias);
            }
        });

        return this;
    }
}
