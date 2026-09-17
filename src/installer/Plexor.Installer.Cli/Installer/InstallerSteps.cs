// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InstallerSteps — file-static step runner used by InitCommand and
// UpgradeCommand to render a single task under the live
// ProgressContext and surface a clean failure if the step returns
// non-zero.
//
// Each step shows as a row in the progress UI (one row at a time).
// On non-zero exit the helper throws — the surrounding
// ProgressRunner catches and the user sees a clean failure rather
// than a half-applied state.
//
// Per code-shape.md §1a, file-static helpers go in a `file static
// class` next to their consumers — no private methods on
// production classes.
// ============================================================================

using Plexor.Shared.Console;
using Spectre.Console;

namespace Plexor.Installer.Cli.Installer;

/// <summary>
///     Step runner shared by <c>plx init</c> and <c>plx upgrade</c>.
///     Renders one progress row per call; throws on non-zero exit so
///     the surrounding progress run aborts cleanly.
/// </summary>
public static class InstallerSteps
{
    /// <summary>
    ///     Run <paramref name="action" /> under the live progress
    ///     context, with <paramref name="description" /> as the row
    ///     label. On a non-zero return value from
    ///     <paramref name="action" />, throws
    ///     <see cref="InvalidOperationException" /> so the
    ///     surrounding progress run aborts and the remaining steps
    ///     don't run.
    /// </summary>
    /// <param name="context">Live progress context (passed by the runner).</param>
    /// <param name="description">Row label shown to the operator.</param>
    /// <param name="action">The work to perform. Returns 0 on success.</param>
    public static async Task RunAsync(ProgressContext context, string description, Func<Task<int>> action)
    {
        var task = context.AddTask(MarkupExtensions.Muted(description));
        var exit = await action();
        task.Increment(100);
        if (exit != 0)
        {
            throw new InvalidOperationException(
                $"step '{description}' failed with exit code {exit}");
        }
    }
}
