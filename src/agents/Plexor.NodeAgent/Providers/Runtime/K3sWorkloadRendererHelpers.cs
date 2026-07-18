// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// K3sWorkloadRendererHelpers — file-static pure-function helpers that
// build the three YAML files of the kustomize directory. Pulled out
// of K3sWorkloadRenderer.cs to keep that file under the 300-line
// class-decomposition threshold (see class-decomposition.md).
//
// Per-file helpers without DI -> file static class in *Helpers.cs
// (class-decomposition.md). These methods are exposed as
// `internal static` so the renderer's tests can validate them
// independently if needed, but the public Renderer entry point
// (`K3sWorkloadRenderer.Render`) is the only caller in production.
// ============================================================================

using System.Text;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Pure-function YAML builders for each of the three files in
///     a kustomize directory (kustomization.yaml + deployment.yaml
///     + service.yaml). Each method emits a single file's contents.
/// </summary>
internal static class K3sWorkloadRendererHelpers
{
    private const string ApiVersionDeployment = "apps/v1";
    private const string ApiVersionService = "v1";

    /// <summary>
    ///     <c>kustomization.yaml</c> body. Pins namespace so
    ///     <c>kubectl apply</c> lands in the right namespace
    ///     without external prerequisites, and lists the
    ///     resource files in declaration order.
    /// </summary>
    /// <param name="workloadName"></param>
    /// <param name="namespace"></param>
    /// <param name="hasService"></param>
    public static string BuildKustomization(
        string workloadName, string @namespace, bool hasService)
    {
        var sb = new StringBuilder();
        sb.Append("apiVersion: kustomize.config.k8s.io/v1beta1\n")
            .Append("kind: Kustomization\n")
            .Append("namespace: ")
            .Append(@namespace)
            .Append("\nresources:\n")
            .Append("- deployment.yaml\n");
        if (hasService)
        {
            sb.Append("- service.yaml\n");
        }
        // Common labels propagate to all resources — useful when
        // the operator later runs `kubectl -l app=web delete all`.
        sb.Append("commonLabels:\n")
            .Append("  app: ")
            .Append(workloadName)
            .Append('\n');
        return sb.ToString();
    }

    /// <summary>
    ///     <c>deployment.yaml</c> body. apps/v1 Deployment with one
    ///     container. env: blocks sorted by key for byte-stable
    ///     YAML output (same determinism fix as Tier 3/4 renderers).
    /// </summary>
    /// <param name="workloadName"></param>
    /// <param name="namespace"></param>
    /// <param name="replicas"></param>
    /// <param name="image"></param>
    /// <param name="ports"></param>
    /// <param name="environment"></param>
    /// <param name="labels"></param>
    public static string BuildDeployment(
        string workloadName,
        string @namespace,
        int replicas,
        string image,
        IReadOnlyList<int> ports,
        IReadOnlyDictionary<string, string> environment,
        IReadOnlyDictionary<string, string> labels)
    {
        var sb = new StringBuilder();
        sb.Append("apiVersion: ")
            .Append(ApiVersionDeployment)
            .Append("\nkind: Deployment\n")
            .Append("metadata:\n")
            .Append("  name: ")
            .Append(workloadName)
            .Append("\n  namespace: ")
            .Append(@namespace)
            .Append("\n  labels:\n");
        AppendLabels(sb, labels);
        sb.Append("spec:\n")
            .Append("  replicas: ").Append(replicas)
            .Append("\n  selector:\n")
            .Append("    matchLabels:\n")
            .Append("      app: ").Append(workloadName)
            .Append("\n  template:\n")
            .Append("    metadata:\n")
            .Append("      labels:\n");
        AppendLabels(sb, labels);
        sb.Append("    spec:\n")
            .Append("      containers:\n")
            .Append("      - name: ").Append(workloadName)
            .Append("\n        image: ").Append(image)
            .Append('\n');

        if (ports.Count > 0)
        {
            sb.Append("        ports:\n");
            foreach (var port in ports)
            {
                sb.Append("        - containerPort: ")
                    .Append(port)
                    .Append("\n          name: port-")
                    .Append(port)
                    .Append("\n          protocol: TCP\n");
            }
        }

        if (environment.Count > 0)
        {
            sb.Append("        env:\n");
            foreach (var kv in environment.OrderBy(
                         static kv => kv.Key, StringComparer.Ordinal))
            {
                sb.Append("        - name: ")
                    .Append(kv.Key)
                    .Append("\n          value: \"")
                    .Append(kv.Value.Replace("\"", "\\\""))
                    .Append("\"\n");


                // Escape embedded quotes in env values so the
                // emitted YAML stays parseable.


            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     <c>service.yaml</c> body. v1 Service that fronts all
    ///     exposed ports on the same port number (NodePort /
    ///     LoadBalancer comes Phase 7+ via Plexor.Shared.Net).
    /// </summary>
    /// <param name="workloadName"></param>
    /// <param name="namespace"></param>
    /// <param name="ports"></param>
    /// <param name="labels"></param>
    public static string BuildService(
        string workloadName,
        string @namespace,
        IReadOnlyList<int> ports,
        IReadOnlyDictionary<string, string> labels)
    {
        var sb = new StringBuilder();
        sb.Append("apiVersion: ")
            .Append(ApiVersionService)
            .Append("\nkind: Service\n")
            .Append("metadata:\n")
            .Append("  name: ")
            .Append(workloadName)
            .Append("\n  namespace: ")
            .Append(@namespace)
            .Append("\n  labels:\n");
        AppendLabels(sb, labels);
        sb.Append("spec:\n")
            .Append("  selector:\n")
            .Append("    app: ")
            .Append(workloadName)
            .Append("\n  ports:\n");
        foreach (var port in ports)
        {
            sb.Append("  - port: ")
                .Append(port)
                .Append("\n    targetPort: port-")
                .Append(port)
                .Append("\n    name: port-")
                .Append(port)
                .Append("\n    protocol: TCP\n");
        }
        return sb.ToString();
    }

    /// <summary>
    ///     Indents labels under the current scope. The block
    ///     starts at <c>labels:</c> on its own line; this method
    ///     emits two-space-indented <c>key: value</c> pairs.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="labels"></param>
    private static void AppendLabels(
        StringBuilder sb, IReadOnlyDictionary<string, string> labels)
    {
        foreach (var kv in labels)
        {
            sb.Append("    ")
                .Append(kv.Key)
                .Append(": ")
                .Append(kv.Value)
                .Append('\n');
        }
    }
}
