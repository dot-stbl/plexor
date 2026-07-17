// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DockerComposeConfig — typed schema for the Docker Compose workload
// spec. The wire-format is opaque JSON (Plexor.Shared.NodeApi
// WorkloadSpec.Config), but for type-safety + editor support we lift
// it into a sealed record before rendering. The TryParse static
// factory reports the JSON pointer of any malformed field so the
// control plane can surface "config missing field 'image'" to the
// operator without leaking the raw JSON.
// ============================================================================

using System.Collections.Immutable;
using System.Text.Json;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Strongly-typed shape of a Docker Compose workload spec.
///     <see cref="TryParse" /> lifts the opaque
///     <c>WorkloadSpec.Config</c> JSON into this record; the
///     renderer turns it back into a YAML manifest.
///
///     Image, ports, environment, and volumes map 1:1 to the
///     standard docker-compose <c>services.&lt;name&gt;</c> keys —
///     no transforms, no app-provider-specific conventions.
///     App providers (Postgres, Redis, ...) compose the right
///     values when they write the workload spec.
/// </summary>
public sealed record DockerComposeConfig
{
    /// <summary>
    ///     OCI image reference — <c>nginx:1.25</c>,
    ///     <c>ghcr.io/stbl/postgres:15</c>, etc. Required.
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    ///     TCP ports to publish, mapped 1:1 (host:container). Empty
    ///     for workloads that don't expose external ports (sidecars,
    ///     internal services).
    /// </summary>
    public IReadOnlyList<int> Ports { get; init; } = Array.Empty<int>();

    /// <summary>
    ///     Key-value pairs injected as
    ///     <c>services.&lt;name&gt;.environment</c>. Null/missing
    ///     in JSON deserializes to an empty dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; }
        = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    ///     Bind mounts — <c>/host/path:/container/path</c>. Use
    ///     <c>named</c> volumes via separate provider extensions
    ///     in Phase 7+ (Plexor is Linux-only at v0.1, no Windows
    ///     path semantics).
    /// </summary>
    public IReadOnlyList<string> Volumes { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Parse the workload spec's opaque config JSON into a
    ///     strongly-typed <see cref="DockerComposeConfig" />.
    ///     Returns null + writes the JSON pointer of the first
    ///     missing/invalid field to <paramref name="error" />.
    ///     Field-by-field validation matches the JSON shape the
    ///     app-provider guides document.
    /// </summary>
    /// <param name="config">
    ///     Opaque JSON element from <c>WorkloadSpec.Config</c>.
    ///     May be <c>JsonElement.ValueKind == Undefined</c> when
    ///     the spec omitted the config object.
    /// </param>
    /// <param name="error">
    ///     On failure: a human-readable JSON pointer identifying
    ///     the bad field. "config missing 'image'" / "config
    ///     ports[3] is not an integer" etc.
    /// </param>
    public static DockerComposeConfig? TryParse(JsonElement config, out string? error)
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

            // Regular Dictionary preserves JSON-declaration order
            // (ImmutableDictionary iterates in hash order — non-
            // deterministic between builds). Keep insertion order
            // so the rendered manifest is stable across replays.
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

        var volumes = new List<string>();
        if (config.TryGetProperty("volumes", out var volProp))
        {
            if (volProp.ValueKind != JsonValueKind.Array)
            {
                error = "config field 'volumes' must be an array";
                return null;
            }

            var i = 0;
            foreach (var vol in volProp.EnumerateArray())
            {
                if (vol.ValueKind != JsonValueKind.String)
                {
                    error = $"config volumes[{i}] is not a string";
                    return null;
                }
                volumes.Add(vol.GetString() ?? string.Empty);
                i++;
            }
        }

        error = null;
        return new DockerComposeConfig
        {
            Image = image,
            Ports = ports,
            Environment = environment,
            Volumes = volumes,
        };
    }
}
