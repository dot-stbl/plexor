// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ProgressRunner — thin static wrapper over Spectre.Console.Progress
// that gives call sites a single async method to start a progress
// run. Use this for any operation with multiple discrete steps the
// user should see progressing (init phases, upgrade phases, etc).
//
// Usage:
//
//     await ProgressRunner.RunAsync(async ctx =>
//     {
//         var scanTask = ctx.AddTask("[grey]scanning[/]", maxValue: 6);
//         for (var i = 0; i < 6; i++)
//         {
//             // ... do work ...
//             scanTask.Increment(1);
//         }
//     });
// ============================================================================

using Spectre.Console;

namespace Plexor.Shared.Console;

/// <summary>
/// Static helper for one-shot progress runs. Wraps
/// <see cref="Progress"/> so call sites only see the async lambda —
/// no need to manage the builder lifecycle.
/// </summary>
public static class ProgressRunner
{
    /// <summary>Start a progress run and run the body. The body
    /// adds tasks and increments them; the renderer updates the
    /// terminal in place until the body returns.</summary>
    /// <param name="body">The work to run with a live progress
    /// context. Receives the running <see cref="ProgressTask"/>s
    /// via the Spectre API.</param>
    public static async Task RunAsync(Func<ProgressContext, Task> body)
    {
        await AnsiConsole.Progress()
            .HideCompleted(false)
            .AutoRefresh(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(body);
    }
}