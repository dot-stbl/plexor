// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorTomlFileProvider — reads a single TOML file into the
// IConfiguration tree as Section:Key entries.
//
// Flattening rules (matching the PLX_* env provider, so the two
// stay interchangeable):
//   [database]
//   host = "localhost"      →  "Database:Host" = "localhost"
//   port = 47100            →  "Database:Port" = "47100"
//
//   [cert_authority]
//   cert_path = "..."       →  "CertAuthority:CertPath" = "..."
//
// Nested tables (rare in MVP) flatten further:
//   [database.pool]
//   max_size = 10           →  "Database:Pool:MaxSize" = "10"
//
// Arrays of tables (TOML [[items]]) become a numeric-suffixed
// section per entry. They're rarely used in IConfiguration paths
// — Options binder doesn't iterate arrays — so we render them
// as best-effort numeric keys:
//   [[plugins]]
//   name = "x"              →  "Plugins:0:Name" = "x"
//   [[plugins]]
//   name = "y"              →  "Plugins:1:Name" = "y"
//
// Missing file is a no-op (not an error). Operator either has
// no overrides yet or set PLX_CONFIG_FILE to point elsewhere.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Tomlyn;
using Tomlyn.Model;

namespace Plexor.Shared.Configuration;

/// <summary>
///     Reads a single TOML file and exposes its values as
///     <see cref="IConfiguration" /> keys. The file may be absent
///     — operators without a user-level override get the lower
///     layers (JSON defaults, then env vars) and that's fine.
/// </summary>
public sealed class PlexorTomlFileProvider(string filePath) : ConfigurationProvider
{
    /// <summary>
    ///     Read the TOML file at the constructor-supplied path
    ///     and flatten its values into
    ///     <see cref="ConfigurationProvider.Data" />. Absent file
    ///     → empty data (higher layers still apply).
    /// </summary>
    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Data = data;
            return;
        }

        var text = File.ReadAllText(filePath);
        var model = TomlSerializer.Deserialize<TomlTable>(text);
        FlattenInto(model, prefix: string.Empty, data);

        Data = data;
    }

    private static void FlattenInto(
        object? node,
        string prefix,
        Dictionary<string, string?> data)
    {
        switch (node)
        {
            case null:
                return;

            case string s:
                data[prefix] = s;
                return;

            case bool b:
                data[prefix] = b ? "true" : "false";
                return;

            case long l:
                data[prefix] = l.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return;

            case double d:
                data[prefix] = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return;

            case TomlTable table:
                foreach (var kvp in table)
                {
                    var childPrefix = prefix.Length == 0
                        ? kvp.Key
                        : prefix + ":" + kvp.Key;
                    FlattenInto(kvp.Value, childPrefix, data);
                }
                return;

            case TomlTableArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var childPrefix = prefix.Length == 0
                        ? i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : prefix + ":" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    FlattenInto(array[i], childPrefix, data);
                }
                return;

            default:
                // Unknown TOML node type — render via ToString so
                // the operator still sees the value (e.g. dates).
                data[prefix] = node.ToString();
                return;
        }
    }
}