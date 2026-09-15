// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// K3sWorkloadRenderer — pure-function spec → kustomize directory.
// File-static per class-decomposition.md: the renderer only
// depends on string formatting + the config record, both of
// which are testable without infrastructure.
//
// Output shape (one directory per workload):
//   /var/lib/plexor/workloads/k3s/<name>/
//     kustomization.yaml     -- lists the resources below
//     deployment.yaml        -- apps/v1 Deployment with one
//                                container (image, ports, env)
//     service.yaml           -- v1 Service (only when ports are
//                                exposed; omitted for background
//                                jobs / init containers that don't
//                                accept traffic)
//
// The three file bodies are built by K3sWorkloadRendererHelpers
// (file-scope, same folder). Keeping this file slim — only the
// orchestration in Render — keeps the renderer under the 300-line
// threshold (class-decomposition.md). PodmanQuadletRenderer (Tier 4)
// and DockerComposeRenderer (Tier 3) are flatter because their
// output is single-file; Tier 5 emits three.
//
// All three files use LF line endings explicitly.
// ============================================================================

using System.Collections.Immutable;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     In-memory representation of the kustomize directory the
///     renderer emits. Each property is the file contents;
///     the provider writes them under
///     <c>/var/lib/plexor/workloads/k3s/&lt;name&gt;/&lt;file&gt;</c>
///     before invoking <c>kubectl apply -k &lt;path&gt;</c>.
/// </summary>
/// <param name="KustomizationYaml">
///     Contents of <c>kustomization.yaml</c>.
/// </param>
/// <param name="DeploymentYaml">
///     Contents of <c>deployment.yaml</c>.
/// </param>
/// <param name="ServiceYaml">
///     Contents of <c>service.yaml</c> — empty string when the
///     workload exposes no ports (background jobs etc.); the
///     provider omits the file entirely in that case.
/// </param>
public sealed record K3sManifest(
    string KustomizationYaml,
    string DeploymentYaml,
    string ServiceYaml);

/// <summary>
///     Pure-function renderer that turns a
///     <see cref="K3sWorkloadConfig" /> into a kustomize directory.
///     Delegates the per-file YAML construction to
///     <see cref="K3sWorkloadRendererHelpers" />.
/// </summary>
internal static class K3sWorkloadRenderer
{
    /// <summary>
    ///     Render the workload's kustomize directory. Caller
    ///     writes each YAML to its target file path and runs
    ///     <c>kubectl apply -k &lt;dir&gt;</c>.
    /// </summary>
    /// <param name="workloadName">
    ///     Kubernetes resource name (also the filesystem
    ///     directory name). The renderer uses this for the
    ///     Deployment + Service names + their labels. Must be
    ///     a valid RFC 1123 DNS label (caller validated).
    /// </param>
    /// <param name="config">Parsed config.</param>
    /// <exception cref="ArgumentException"></exception>
    public static K3sManifest Render(string workloadName, K3sWorkloadConfig config)
    {
        if (string.IsNullOrWhiteSpace(workloadName))
        {
            throw new ArgumentException(
                "k3s workload name cannot be null or whitespace.",
                nameof(workloadName));
        }

        // config is non-nullable; trust the type system per
        // .agents/rules/coding/code-shape.md §11.

        var hasPorts = config.Ports.Count > 0;
        var labels = ImmutableDictionary.CreateRange(
        [
            new KeyValuePair<string, string>("app", workloadName),
            new KeyValuePair<string, string>("managed-by", "plexor"),
        ]);

        var deploymentYaml = K3sWorkloadRendererHelpers.BuildDeployment(
            workloadName, config.Namespace, config.Replicas,
            config.Image, config.Ports, config.Environment, labels);

        var serviceYaml = hasPorts
            ? K3sWorkloadRendererHelpers.BuildService(
                workloadName, config.Namespace, config.Ports, labels)
            : string.Empty;

        var kustomizationYaml = K3sWorkloadRendererHelpers.BuildKustomization(
            workloadName, config.Namespace, hasPorts);

        return new K3sManifest(kustomizationYaml, deploymentYaml, serviceYaml);
    }
}
