// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DockerComposeRenderer — pure-function spec → docker-compose.yaml
// transformation. File-static (no DI) per class-decomposition.md:
// the renderer only depends on string formatting + the config
// record, both of which are testable without infrastructure.
//
// Output format: standard docker-compose v2 YAML with 2-space
// indentation. Quoted strings only when necessary (port mappings
// always, env values only when they contain special characters).
//
// Pinned manifest structure:
//   services:
//     <spec.Name>:
//       image: <config.Image>
//       ports:                    (only if non-empty)
//         - "<port>:<port>"
//       environment:              (only if non-empty)
//         KEY: value
//       volumes:                  (only if non-empty)
//         - "/host:/container"
//
// Pulled out as a separate helper so the renderer is the only
// place manifest-shape decisions live — the provider just writes
// the rendered string to disk and shells `docker compose up -d`.
// ============================================================================

using System.Collections.Immutable;
using System.Text;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Pure-function renderer that turns a
///     <see cref="DockerComposeConfig" /> into a docker-compose
///     v2 YAML manifest. Tested via snapshot tests; not exposed
///     on the public DI surface.
/// </summary>
internal static class DockerComposeRenderer
{
    /// <summary>
    ///     Render the workload's service definition. Caller
    ///     writes the returned string to a per-workload
    ///     <c>compose.yaml</c> file on disk and invokes
    ///     <c>docker compose -f &lt;path&gt; up -d</c>.
    /// </summary>
    /// <param name="serviceName">
    ///     Service name (driven by <c>WorkloadSpec.Name</c>; the
    ///     provider treats it as the local id of the workload).
    /// </param>
    /// <param name="config">
    ///     Parsed config (image, ports, environment, volumes).
    /// </param>
    public static string Render(string serviceName, DockerComposeConfig config)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException(
                "Docker compose service name cannot be null or whitespace.",
                nameof(serviceName));
        }

        // config is non-nullable; trust the type system per
        // .agents/rules/coding/code-shape.md §11. TryParse in the
        // provider's CreateAsync returns a non-null config or
        // throws InvalidOperationException — the null state is
        // unreachable here.

        var sb = new StringBuilder();
        sb.Append("services:\n");
        sb.Append("  ");
        sb.Append(serviceName);
        sb.Append(":\n");
        sb.Append("    image: ");
        sb.Append(config.Image);
        sb.Append('\n');

        if (config.Ports.Count > 0)
        {
            sb.Append("    ports:\n");
            foreach (var port in config.Ports)
            {
                sb.Append("      - \"");
                sb.Append(port);
                sb.Append(':');
                sb.Append(port);
                sb.Append("\"\n");
            }
        }

        if (config.Environment.Count > 0)
        {
            sb.Append("    environment:\n");
            // Sort by key so the rendered manifest is byte-stable
            // across replays (the input IReadOnlyDictionary
            // makes no ordering guarantee — Dictionary is
            // insertion-order but we're sealed as the interface).
            foreach (var kv in config.Environment.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
            {
                sb.Append("      ");
                sb.Append(kv.Key);
                sb.Append(": ");
                sb.Append(kv.Value);
                sb.Append('\n');
            }
        }

        if (config.Volumes.Count > 0)
        {
            sb.Append("    volumes:\n");
            foreach (var volume in config.Volumes)
            {
                sb.Append("      - ");
                sb.Append(volume);
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }
}
