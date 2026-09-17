// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmCreateCommand — `plx vm create`. Builds a PascalCase JSON
// payload matching the LibvirtKvmConfig record that the agent's
// libvirt-KVM provider deserialises (RamBytes / CpuCores /
// NetworkName / BaseImageRef), then calls
// `POST /api/v1/compute/clusters/{id}/workloads` and prints:
//
//   - the new workload id
//   - the assigned node id (or "pending scheduling")
//   - the workload's initial state (Provisioning)
//
// The `--target-node` flag is accepted for forward compatibility but
// the v0.1 host's `CreateWorkloadCommand` does not propagate it
// through the wire — the assignment falls through to the
// ManualPlacementScheduler's "stay unassigned" default.
// ============================================================================

using Plexor.Installer.Cli;
using Plexor.Installer.Cli.Refit;
using Plexor.Installer.Cli.Settings;
using Plexor.Shared.Console;
using Refit;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Installer.Commands;

/// <summary>
///     <c>plx vm create</c> — provision a new VM workload in the
///     resolved cluster. Calls
///     <c>POST /api/v1/compute/clusters/{clusterId}/workloads</c>
///     via the Refit client.
/// </summary>
public sealed class VmCreateCommand : AsyncCommand<VmCreateSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, VmCreateSettings settings)
    {
        var config = settings.ResolveConfig();
        if (config is null)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "host / token missing",
                "pass --host and --token, or set PLX_HOST + PLX_TOKEN, or write ~/.plx/config.json"));
            return 2;
        }

        if (string.IsNullOrWhiteSpace(config.Cluster))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "cluster missing",
                "pass --cluster <ID>, or set PLX_CLUSTER, or add cluster to ~/.plx/config.json"));
            return 3;
        }

        if (string.IsNullOrWhiteSpace(settings.Name) ||
            string.IsNullOrWhiteSpace(settings.Image))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "required args missing",
                "--name and --image are required"));
            return 3;
        }

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            var specJson = VmSpecBuilder.Build(
                settings.Vcpu,
                settings.RamMb,
                settings.Image);
            var request = new HostCreateWorkloadRequest(
                settings.Name,
                Kind: "vm",
                specJson);
            var response = await client.CreateWorkloadAsync(
                config.Cluster,
                request,
                default);
            VmCreateRenderer.Render(response, settings);
            return 0;
        }
        catch (ApiException ex)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                $"HTTP {(int)ex.StatusCode}",
                ex.Message));
            return 4;
        }
        finally
        {
            http.Dispose();
        }
    }
}

/// <summary>
///     File-static helper for <see cref="VmCreateCommand" />. Builds
///     the PascalCase JSON payload the agent's libvirt-KVM provider
///     deserialises (matches <c>LibvirtKvmConfig</c> in
///     Plexor.NodeAgent).
/// </summary>
file static class VmSpecBuilder
{
    /// <summary>
    ///     Construct the VM provider-specific config JSON. Shape
    ///     matches <c>LibvirtKvmConfig(RamBytes, CpuCores,
    ///     NetworkName, BaseImageRef)</c>. PascalCase keys because
    ///     <c>LibvirtConfigDeserializer.TryDeserialize</c> uses
    ///     default <see cref="System.Text.Json.JsonSerializerOptions" />,
    ///     which is case-sensitive PascalCase. <paramref name="ramMb" />
    ///     is converted to bytes (× 1024 × 1024) to match the
    ///     provider's long-bytes contract.
    /// </summary>
    /// <param name="vcpu">Logical vCPU count.</param>
    /// <param name="ramMb">RAM in MiB; converted to bytes.</param>
    /// <param name="imageRef">Image registry ref (e.g. <c>ubuntu-22.04-cloud</c>).</param>
    /// <returns>JSON string ready for the host's <c>SpecJson</c> slot.</returns>
    public static string Build(int vcpu, int ramMb, string imageRef)
    {
        var ramBytes = (long)ramMb * 1024L * 1024L;
        return string.Concat(
            "{",
            $"\"RamBytes\":{ramBytes},",
            $"\"CpuCores\":{vcpu},",
            "\"NetworkName\":\"default\",",
            $"\"BaseImageRef\":\"{EscapeJson(imageRef)}\"",
            "}");
    }

    private static string EscapeJson(string raw)
    {
        return raw
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

/// <summary>
///     File-static renderer for <see cref="VmCreateCommand" />.
///     Prints the new workload id + assigned node + initial state.
///     The forward-compat <c>--target-node</c> flag, if passed,
///     surfaces as a "[warn] ignored on this host version" line so
///     the operator sees that the pin did not actually take effect.
/// </summary>
file static class VmCreateRenderer
{
    /// <summary>
    ///     Print the new workload's id + assigned node + initial
    ///     state. <paramref name="settings" /> is inspected only for
    ///     <c>TargetNode</c> (forward-compat warning).
    /// </summary>
    /// <param name="workload">Create response from the host.</param>
    /// <param name="settings">CLI settings — only TargetNode is read.</param>
    public static void Render(HostWorkloadSummary workload, VmCreateSettings settings)
    {
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok(
            $"{BannerArt.Icon.Status} created vm {MarkupExtensions.B(workload.Name)} ({ShortId(workload.Id)})"));

        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  id:       {workload.Id}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  kind:     {workload.Kind}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            "  node:     "
            + (workload.AssignedNodeId is null
                ? $"{BannerArt.Icon.Pending} pending scheduling"
                : workload.AssignedNodeId)));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            "  state:    "
            + FormatState(workload.State)));

        if (!string.IsNullOrWhiteSpace(settings.TargetNode))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Console.MarkupLine(MarkupExtensions.Warn(
                $"{BannerArt.Icon.Warn} --target-node was passed but is ignored by the v0.1 backend; the workload was created without a placement pin"));
        }
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
