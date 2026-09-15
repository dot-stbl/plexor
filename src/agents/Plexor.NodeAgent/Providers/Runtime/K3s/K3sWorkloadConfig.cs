// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// K3sWorkloadConfig — typed schema for a k3s workload spec. The
// wire-format is opaque JSON (Plexor.Shared.NodeApi
// WorkloadSpec.Config); TryParse lifts it into this record with
// field-by-field validation that reports JSON pointers on failure.
//
// k3s workloads map 1:1 to a Kubernetes Deployment + Service in a
// single namespace. The config captures the K8s-friendly parameters
// (replicas, namespace) and the container spec (image, ports,
// environment) needed to render the YAML.
// ============================================================================

using System.Collections.Immutable;
using System.Text.Json;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Strongly-typed shape of a k3s workload spec.
///     <see cref="TryParse" /> lifts the opaque
///     <c>WorkloadSpec.Config</c> JSON into this record; the
///     renderer turns it into a kustomize directory
///     (<c>kustomization.yaml</c> + <c>deployment.yaml</c> +
///     <c>service.yaml</c>).
/// </summary>
public sealed record K3sWorkloadConfig
{
    /// <summary>
    ///     OCI image reference — <c>nginx:1.25</c>,
    ///     <c>ghcr.io/stbl/postgres:15</c>, etc. Required.
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    ///     Kubernetes namespace for the workload. Defaults to
    ///     <c>default</c>; Plexor's first app-provider typically
    ///     uses per-tenant namespaces but that's a Phase 7+
    ///     concern (today the workload namespace === its workload
    ///     name so multi-tenant isolation comes for free).
    /// </summary>
    public string Namespace { get; init; } = "default";

    /// <summary>
    ///     Number of pod replicas for the Deployment. Clamped to
    ///     the range [0, 16] in TryParse.
    /// </summary>
    public int Replicas { get; init; } = 1;

    /// <summary>
    ///     TCP container ports to expose via the Service. Empty
    ///     for workloads that don't expose external ports
    ///     (background jobs, init containers).
    /// </summary>
    public IReadOnlyList<int> Ports { get; init; } = [];

    /// <summary>
    ///     Environment variables injected via the Deployment's
    ///     spec.template.spec.containers[].env[]. Names follow DNS
    ///     label naming rules (alphanumeric + dash/underscore).
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; }
        = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    ///     Parse the workload spec's opaque config JSON into a
    ///     strongly-typed <see cref="K3sWorkloadConfig" />.
    ///     Returns null + writes the JSON pointer of the first
    ///     missing/invalid field to <paramref name="error" />.
    /// </summary>
    /// <param name="config">Opaque JSON element from
    ///     <c>WorkloadSpec.Config</c>.</param>
    /// <param name="error">On failure: human-readable JSON pointer.</param>
    public static K3sWorkloadConfig? TryParse(JsonElement config, out string? error)
    {
        if (config.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            error = "config missing 'image' (entire config object was empty)";
            return null;
        }

        if (config.ValueKind != JsonValueKind.Object)
        {
            error = $"config must be a JSON object (got {config.ValueKind})";
            return null;
        }

        if (!config.TryGetProperty("image", out var imageProp) ||
            imageProp.ValueKind != JsonValueKind.String)
        {
            error = "config missing required string field 'image'";
            return null;
        }

        var image = imageProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(image))
        {
            error = "config field 'image' must be a non-empty string";
            return null;
        }

        var @namespace = "default";
        if (config.TryGetProperty("namespace", out var namespaceProp))
        {
            if (namespaceProp.ValueKind != JsonValueKind.String)
            {
                error = "config field 'namespace' must be a string";
                return null;
            }
            var nsValue = namespaceProp.GetString() ?? string.Empty;
            if (!RuntimeHelpers.IsValidK8sName(nsValue))
            {
                error = "config field 'namespace' must be a valid Kubernetes " +
                        "name (lowercase alphanumeric or '-', max 63 chars); " +
                        $"got '{nsValue}'.";
                return null;
            }
            @namespace = nsValue;
        }

        var replicas = 1;
        if (config.TryGetProperty("replicas", out var replicasProp))
        {
            if (replicasProp.ValueKind is not JsonValueKind.Number ||
                !replicasProp.TryGetInt32(out var replicasValue))
            {
                error = "config field 'replicas' must be an integer";
                return null;
            }
            if (replicasValue is < 0 or > 16)
            {
                error = "config field 'replicas' must be in [0, 16]";
                return null;
            }
            replicas = replicasValue;
        }

        var ports = new List<int>();
        if (config.TryGetProperty("ports", out var portsProp))
        {
            if (portsProp.ValueKind != JsonValueKind.Array)
            {
                error = "config field 'ports' must be an array";
                return null;
            }

            var i = 0;
            foreach (var port in portsProp.EnumerateArray())
            {
                if (port.ValueKind != JsonValueKind.Number ||
                    !port.TryGetInt32(out var portValue))
                {
                    error = $"config ports[{i}] is not an integer";
                    return null;
                }
                ports.Add(portValue);
                i++;
            }
        }

        var environment = ImmutableDictionary<string, string>.Empty;
        if (config.TryGetProperty("environment", out var envProp))
        {
            if (envProp.ValueKind != JsonValueKind.Object)
            {
                error = "config field 'environment' must be an object";
                return null;
            }

            // Regular Dictionary preserves JSON-declaration order;
            // the renderer sorts before emitting so the YAML
            // output is byte-stable across replays.
            var envDict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in envProp.EnumerateObject())
            {
                var val = kv.Value;
                if (val.ValueKind != JsonValueKind.String)
                {
                    error = $"config environment['{kv.Name}'] is not a string";
                    return null;
                }
                envDict.Add(kv.Name, val.GetString() ?? string.Empty);
            }
            environment = ImmutableDictionary.CreateRange(envDict);
        }

        error = null;
        return new K3sWorkloadConfig
        {
            Image = image,
            Namespace = @namespace,
            Replicas = replicas,
            Ports = ports,
            Environment = environment,
        };
    }

}
