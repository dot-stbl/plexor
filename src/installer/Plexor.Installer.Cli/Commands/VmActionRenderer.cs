// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmActionRenderer — shared renderer for `plx vm start` and
// `plx vm stop`. Both commands issue a WorkloadActionCommand
// against the host and receive a HostWorkloadActionResult back
// (CommandId + new state). The print shape is identical, so the
// renderer lives in a single internal-static file (not file-static)
// and is reused by VmStartCommand + VmStopCommand.
//
// Per code-shape.md §1a (no private methods on production
// classes), the print logic is here, not on the commands.
// ============================================================================

using Plexor.Installer.Cli.Refit;
using Plexor.Shared.Console;
using Spectre.Console;

namespace Plexor.Installer.Commands;

/// <summary>
///     Shared renderer for the <c>vm start</c> and <c>vm stop</c>
///     commands. Both wrap a <see cref="HostWorkloadActionResult" />
///     reply and print the verb + workload id + new state +
///     command id (UUIDv7) for log correlation.
/// </summary>
internal static class VmActionRenderer
{
    /// <summary>
    ///     Print the action verb + workload id + new state +
    ///     command id (UUIDv7) from the agent's reply.
    /// </summary>
    /// <param name="verb">Past-tense verb (e.g. <c>"started"</c>, <c>"stopped"</c>).</param>
    /// <param name="vmId">Workload id the operator passed.</param>
    /// <param name="result">Action response from the host.</param>
    public static void Render(string verb, string vmId, HostWorkloadActionResult result)
    {
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok(
            $"{BannerArt.Icon.Status} {verb} {MarkupExtensions.B(ShortId(vmId))}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  state:     {FormatState(result.State)}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  command:   {result.CommandId:N}"));
    }

    private static string ShortId(string id)
    {
        var underscore = id.IndexOf('_');
        return underscore is >= 0 && underscore + 9 < id.Length
            ? id[..(underscore + 9)] + "…"
            : id;
    }

    private static string FormatState(HostWorkloadState state)
    {
        return state switch
        {
            HostWorkloadState.Provisioning => "provisioning",
            HostWorkloadState.Running => "running",
            HostWorkloadState.Stopped => "stopped",
            HostWorkloadState.Failed => "failed",
            HostWorkloadState.Unknown => "unknown",
            _ => state.ToString()
        };
    }
}
