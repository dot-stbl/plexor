// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCli — static factory entry point. Call PlexorCli.New(args)
// once at the start of Program.cs, chain the fluent methods, and end
// with PlexorCliBuilder.Run().
//
// Extracted from CliAppBuilder.cs (Sprint 3, item 3 — 1 type per
// file per folder-organization.md §1).
// ============================================================================

namespace Plexor.Shared.Console;

/// <summary>
///     Static factory for a Plexor CLI. Call <see cref="New" /> once
///     at the start of <c>Program.cs</c>, chain the fluent methods,
///     and end with <see cref="PlexorCliBuilder.Run" />.
/// </summary>
public static class PlexorCli
{
    /// <summary>Begin building a new CLI.</summary>
    /// <param name="args">
    ///     Raw command-line arguments (typically passed straight from
    ///     <c>Main</c>).
    /// </param>
    public static PlexorCliBuilder New(string[] args)
    {
        return new PlexorCliBuilder(args);
    }
}
