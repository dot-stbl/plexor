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
// All three files use LF line endings explicitly. Podman
// Quadlet (Tier 4) and Docker Compose (Tier 3) renderers do
// the same; a future format-check pass could enforce this
// project-wide.
// ============================================================================

using System.Collections.Immutable;
using System.Text;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     In-memory representation of the kustomize directory the
///     renderer emits. Each property is the file contents;
///     the provider writes them under
///     <c>/var/lib/plexor/workloads/k3s/&lt;name&gt;/&lt;file&gt;</c>
///     before invoking <c>kubectl apply -k &lt;path&gt;</c>.
/// </summary>
/// <param name="KustomizationYaml">
///     Contents of <c>kustomization.yaml</c> — lists the
///     resources below (deployment + optional service).
/// </param>
/// <param name="DeploymentYaml">
///     Contents of <c>deployment.yaml</c> — apps/v1 Deployment
///     spec with one container (image, ports, env).
/// </param>
/// <param name="ServiceYaml">
///     Contents of <c>service.yaml</c> — v1 Service that fronts
///     the workload's exposed ports. Empty string when the
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
/// </summary>
internal static class K3sWorkloadRenderer
{
    private const string ApiVersionDeployment = "apps/v1";
    private const string ApiVersionService = "v1";

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
    /// <param name="config">Parsed config (image, namespace, replicas, ports, env).</param>
    public static K3sManifest Render(string workloadName, K3sWorkloadConfig config)
    {
        if (string.IsNullOrWhiteSpace(workloadName))
        {
            throw new ArgumentException(
                "k3s workload name cannot be null or whitespace.",
                nameof(workloadName));
        }

        // config is non-nullable; trust the type system per
        // .agents/rules/coding/code-shape.md §11. The provider's
        // CreateAsync rejects null via the TryParse out-error
        // pattern before getting here.

        var hasPorts = config.Ports.Count > 0;
        var labels = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, string>("app", workloadName),
            new KeyValuePair<string, string>("managed-by", "plexor"),
        });

        var deploymentYaml = RenderDeployment(
            workloadName, config.Namespace, config.Replicas,
            config.Image, config.Ports, config.Environment, labels);

        var serviceYaml = hasPorts
            ? RenderService(workloadName, config.Namespace, config.Ports, labels)
            : string.Empty;

        var kustomizationYaml = RenderKustomization(
            workloadName, config.Namespace, hasPorts);

        return new K3sManifest(kustomizationYaml, deploymentYaml, serviceYaml);
    }

    /// <summary>
    ///     <c>kustomization.yaml</c> body. Pins namespace so
    ///     <c>kubectl apply</c> lands in the right namespace
    ///     without external prerequisites, and lists the
    ///     resource files in declaration order.
    /// </summary>
    private static string RenderKustomization(
        string workloadName, string @namespace, bool hasService)
    {
        var sb = new StringBuilder();
        sb.Append("apiVersion: kustomize.config.k8s.io/v1beta1\n");
        sb.Append("kind: Kustomization\n");
        sb.Append("namespace: ");
        sb.Append(@namespace);
        sb.Append("\nresources:\n");
        sb.Append("- deployment.yaml\n");
        if (hasService)
        {
            sb.Append("- service.yaml\n");
        }
        // Common labels propagate to all resources — useful when
        // the operator later runs `kubectl -l app=web delete all`.
        sb.Append("commonLabels:\n");
        sb.Append("  app: ");
        sb.Append(workloadName);
        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>
    ///     <c>deployment.yaml</c> body. apps/v1 Deployment with one
    ///     container. env: blocks sorted by key for byte-stable
    ///     YAML output (same determinism fix as Tier 3/4 renderers).
    /// </summary>
    private static string RenderDeployment(
        string workloadName,
        string @namespace,
        int replicas,
        string image,
        IReadOnlyList<int> ports,
        IReadOnlyDictionary<string, string> environment,
        IReadOnlyDictionary<string, string> labels)
    {
        var sb = new StringBuilder();
        sb.Append("apiVersion: ");
        sb.Append(ApiVersionDeployment);
        sb.Append("\nkind: Deployment\n");
        sb.Append("metadata:\n");
        sb.Append("  name: ");
        sb.Append(workloadName);
        sb.Append("\n  namespace: ");
        sb.Append(@namespace);
        sb.Append("\n  labels:\n");
        AppendLabels(sb, labels);
        sb.Append("spec:\n");
        sb.Append("  replicas: ");
        sb.Append(replicas);
        sb.Append("\n  selector:\n");
        sb.Append("    matchLabels:\n");
        sb.Append("      app: ");
        sb.Append(workloadName);
        sb.Append("\n  template:\n");
        sb.Append("    metadata:\n");
        sb.Append("      labels:\n");
        AppendLabels(sb, labels);
        sb.Append("    spec:\n");
        sb.Append("      containers:\n");
        sb.Append("      - name: ");
        sb.Append(workloadName);
        sb.Append("\n        image: ");
        sb.Append(image);
        sb.Append('\n');

        if (ports.Count > 0)
        {
            sb.Append("        ports:\n");
            foreach (var port in ports)
            {
                sb.Append("        - containerPort: ");
                sb.Append(port);
                sb.Append("\n          name: port-");
                sb.Append(port);
                sb.Append("\n          protocol: TCP\n");
            }
        }

        if (environment.Count > 0)
        {
            sb.Append("        env:\n");
            foreach (var kv in environment.OrderBy(
                         static kv => kv.Key, StringComparer.Ordinal))
            {
                sb.Append("        - name: ");
                sb.Append(kv.Key);
                sb.Append("\n          value: \"");
                // Escape embedded quotes in env values so the
                // emitted YAML stays parseable.
                sb.Append(kv.Value.Replace("\"", "\\\""));
                sb.Append("\"\n");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     <c>service.yaml</c> body. v1 Service that fronts all
    ///     exposed ports on the same port number (NodePort /
    ///     LoadBalancer comes Phase 7+ via Plexor.Shared.Net).
    /// </summary>
    private static string RenderService(
        string workloadName,
        string @namespace,
        IReadOnlyList<int> ports,
        IReadOnlyDictionary<string, string> labels)
    {
        var sb = new StringBuilder();
        sb.Append("apiVersion: ");
        sb.Append(ApiVersionService);
        sb.Append("\nkind: Service\n");
        sb.Append("metadata:\n");
        sb.Append("  name: ");
        sb.Append(workloadName);
        sb.Append("\n  namespace: ");
        sb.Append(@namespace);
        sb.Append("\n  labels:\n");
        AppendLabels(sb, labels);
        sb.Append("spec:\n");
        sb.Append("  selector:\n");
        sb.Append("    app: ");
        sb.Append(workloadName);
        sb.Append("\n  ports:\n");
        foreach (var port in ports)
        {
            sb.Append("  - port: ");
            sb.Append(port);
            sb.Append("\n    targetPort: port-");
            sb.Append(port);
            sb.Append("\n    name: port-");
            sb.Append(port);
            sb.Append("\n    protocol: TCP\n");
        }
        return sb.ToString();
    }

    /// <summary>
    ///     Indents labels under the current scope. The block
    ///     starts at <c>labels:</c> on its own line; this method
    ///     emits two-space-indented <c>key: value</c> pairs.
    /// </summary>
    private static void AppendLabels(
        StringBuilder sb, IReadOnlyDictionary<string, string> labels)
    {
        foreach (var kv in labels)
        {
            sb.Append("    ");
            sb.Append(kv.Key);
            sb.Append(": ");
            sb.Append(kv.Value);
            sb.Append('\n');
        }
    }
}
