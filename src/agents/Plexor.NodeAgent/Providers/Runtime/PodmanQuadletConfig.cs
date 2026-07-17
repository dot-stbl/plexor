// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PodmanQuadletConfig — typed schema for a Podman Quadlet workload
// spec. The wire-format is opaque JSON (Plexor.Shared.NodeApi
// WorkloadSpec.Config); TryParse lifts it into this record with
// field-by-field validation that reports JSON pointers on failure.
// ============================================================================

using System.Collections.Immutable;
using System.Text.Json;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Strongly-typed shape of a Podman Quadlet workload spec.
///     <see cref="TryParse" /> lifts the opaque
///     <c>WorkloadSpec.Config</c> JSON into this record; the
///     renderer turns it back into a <c>&lt;name&gt;.container</c>
///     systemd quadlet INI file.
/// </summary>
public sealed record PodmanQuadletConfig
{
    /// <summary>
    ///     OCI image reference — <c>nginx:1.25</c>,
    ///     <c>ghcr.io/stbl/postgres:15</c>, etc. Required.
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    ///     TCP ports to publish, mapped 1:1 (host:container). Empty
    ///     for workloads that don't expose external ports.
    /// </summary>
    public IReadOnlyList<int> Ports { get; init; } = Array.Empty<int>();

    /// <summary>
    ///     Key-value pairs injected as <c>Environment=</c> entries
    ///     inside the <c>[Container]</c> section.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; }
        = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    ///     Whether to enable the <c>[Install]</c> section so the
    ///     workload auto-starts at boot. Default true (production
    ///     behavior); set false for one-shot or dev workloads.
    /// </summary>
    public bool AutoStart { get; init; } = true;

    /// <summary>
    ///     Restart policy written to the <c>[Service]</c> section's
    ///     <c>Restart=</c> key. Default <c>always</c> (production);
    ///     set <c>on-failure</c> / <c>no</c> / <c>unless-stopped</c>
    ///     via the JSON config when the workload semantics call for
    ///     it.
    /// </summary>
    public string Restart { get; init; } = "always";

    /// <summary>
    ///     Parse the workload spec's opaque config JSON into a
    ///     strongly-typed <see cref="PodmanQuadletConfig" />.
    ///     Returns null + writes the JSON pointer of the first
    ///     missing/invalid field to <paramref name="error" />.
    /// </summary>
    /// <param name="config">Opaque JSON element from
    ///     <c>WorkloadSpec.Config</c>.</param>
    /// <param name="error">On failure: human-readable JSON pointer.</param>
    public static PodmanQuadletConfig? TryParse(JsonElement config, out string? error)
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

            // Regular Dictionary preserves JSON-declaration order;
            // ImmutableDictionary iterates in hash order. The
            // renderer sorts before emitting so the output is
            // byte-stable regardless.
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

        var autoStart = true;
        if (config.TryGetProperty("autoStart", out var autoStartProp))
        {
            if (autoStartProp.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = "config field 'autoStart' must be a boolean";
                return null;
            }
            autoStart = autoStartProp.GetBoolean();
        }

        var restart = "always";
        if (config.TryGetProperty("restart", out var restartProp))
        {
            if (restartProp.ValueKind != JsonValueKind.String)
            {
                error = "config field 'restart' must be a string";
                return null;
            }
            var restartValue = restartProp.GetString() ?? string.Empty;
            if (!IsValidRestartPolicy(restartValue))
            {
                error = $"config field 'restart' must be one of: " +
                        "no, on-success, on-failure, on-abnormal, on-watchdog, on-abort, always.";
                return null;
            }
            restart = restartValue;
        }

        error = null;
        return new PodmanQuadletConfig
        {
            Image = image,
            Ports = ports,
            Environment = environment,
            AutoStart = autoStart,
            Restart = restart,
        };
    }

    /// <summary>
    ///     Whitelist of systemd-valid <c>Restart=</c> values.
    ///     Anything else is a typo / misunderstanding of the
    ///     quadlet contract and we surface it as a config error
    ///     at parse time.
    /// </summary>
    private static bool IsValidRestartPolicy(string value)
    {
        return value is "no" or "on-success" or "on-failure" or "on-abnormal"
            or "on-watchdog" or "on-abort" or "always";
    }
}
