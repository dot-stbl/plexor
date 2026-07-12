// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VersionCommand — `plx version` / `plx v`. Prints the version and
// exits. The actual visual (big PLEXOR banner + version line + tagline
// + footer) is rendered by PlexorCliBuilder.Run() before this
// command executes, so the body is intentionally a no-op.
//
// The command exists for the "explicit form" of asking for the version
// (vs `--version` which is the Spectre built-in flag). Both paths
// show the same visual.
// ============================================================================

using Spectre.Console.Cli;

namespace Plexor.Installer.Commands;

/// <summary>
///     <c>plx version</c> / <c>plx v</c>. The version is already shown
///     in the banner printed by <see cref="Plexor.Shared.Console.PlexorCliBuilder" />;
///     this command is a no-op so the explicit-subcommand form has the
///     same output as <c>--version</c>.
/// </summary>
internal sealed class VersionCommand : Command
{
    /// <summary>
    ///     Exit successfully. The banner was printed by the
    ///     builder; nothing else to do.
    /// </summary>
    public override int Execute(CommandContext context)
    {
        return 0;
    }
}
