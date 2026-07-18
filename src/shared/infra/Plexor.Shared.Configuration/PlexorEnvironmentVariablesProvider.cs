// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorEnvironmentVariablesProvider — reads PLX_* env vars into the
// IConfiguration tree as <section>:<key> entries.
//
// Format (flat single-underscore):
//   PLX_DATABASE_HOST=localhost      →  "Database:Host" = "localhost"
//   PLX_DATABASE_PORT=47100          →  "Database:Port" = "47100"
//   PLX_CERT_AUTHORITY_CERT_PATH=...  →  "CertAuthority:CertPath" = "..."
//
// Why not the .NET default (Section__Key)?
//   - Double underscore looks like noise (it's just a convention).
//   - Operators don't know which keys belong to which section without
//     reading the Options class.
//   - Conflicts with shell conventions — Section__Key is rejected
//     by some shells that strip underscores.
//   - The flat PLX_<SECTION>_<KEY> form is shorter, single
//     separator, and the section is right there in the env var
//     name.
// ============================================================================

using Microsoft.Extensions.Configuration;

namespace Plexor.Shared.Configuration;

/// <summary>
///     Reads <c>PLX_*</c> environment variables and exposes them as
///     <c>Section:Key</c> entries in <see cref="IConfiguration" />.
///     The Section + Key split is computed from the env var name:
///     the first uppercase run is treated as the section, the
///     remainder as the key path (single underscore joins stay as
///     one key, e.g. <c>PLX_DATABASE_POOL_MAX_SIZE</c> →
///     <c>Database:Pool_Max_Size</c>; multi-word properties still
///     need a flat Option name like <c>PoolMaxSize</c>).
/// </summary>
// Note: not sealed — test double in Plexor.Shared.Configuration.Unit
// subclasses this to inject a fixed dictionary instead of reading
// the process env.
public class PlexorEnvironmentVariablesProvider : ConfigurationProvider
{
    /// <summary>The required prefix. Override only for tests.</summary>
    public const string Prefix = "PLX_";

    /// <inheritdoc />
    public override void Load()
    {
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ReadEnvironmentVariables()
                     .Cast<System.Collections.DictionaryEntry>())
        {
            if (entry.Key?.ToString() is not { } rawName ||
                !rawName.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var stripped = rawName[Prefix.Length..];
            if (ToConfigKey(stripped) is not { } configKey)
            {
                continue;
            }

            Data[configKey] = entry.Value?.ToString();
        }
    }

    /// <summary>
    ///     Read-only view of the environment for this provider.
    ///     Overridden in tests to inject a fixed dictionary
    ///     (avoiding parallel-test interference on the process
    ///     env).
    /// </summary>
    protected virtual System.Collections.IDictionary ReadEnvironmentVariables()
    {
        return Environment.GetEnvironmentVariables();
    }

    /// <summary>
    ///     Map <c>SECTION_KEY</c> → <c>Section:Key</c>. Both
    ///     sides are kept as-is (uppercase preserved) so the
    ///     case-insensitive Options binder can match against
    ///     PascalCase property names.
    /// </summary>
    /// <param name="envTail"></param>
    internal static string? ToConfigKey(string envTail)
    {
        if (string.IsNullOrEmpty(envTail))
        {
            return null;
        }

        var underscoreIndex = envTail.IndexOf('_');
        if (underscoreIndex < 0)
        {
            // PLX_FOO (no key) → "Foo" — single-token value.
            return envTail;
        }

        var section = envTail[..underscoreIndex];
        var key = envTail[(underscoreIndex + 1)..];
        if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
        {
            return null;
        }

        return section + ":" + key;
    }
}
