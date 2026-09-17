// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostSettings — base settings shared by every `plx host *`
// command. Carries the three connection-level options that all
// host commands need:
//
//   --host    <URL>   control-plane URL (e.g. https://plexor.example.com)
//   --token   <TOKEN> bearer token for the host API
//   --cluster <ID>    default cluster id; per-command when omitted
//
// All three are optional — PlxConfig.Resolve() falls back to env
// vars / config file / null. The concrete commands surface a clear
// "missing required: --host" message when the resolver returns null.
//
// The class is `abstract` so the CLI's binding machinery rejects
// direct instantiation while derived classes still inherit all
// `[CommandOption]` attributes.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Base settings shared by every <c>plx host *</c> command.
///     <c>--host</c>, <c>--token</c>, and <c>--cluster</c> are
///     inherited by derived settings classes via Spectre's option
///     attribute inheritance.
/// </summary>
public abstract class HostSettings : CommandSettings
{
    /// <summary>Plexor.Host base URL (e.g. <c>https://plexor.example.com</c>).</summary>
    [CommandOption("--host <URL>")]
    [Description("Plexor.Host base URL. Falls back to PLX_HOST or ~/.plx/config.json.")]
    public string? Host { get; init; }

    /// <summary>Bearer token for the host API.</summary>
    [CommandOption("--token <TOKEN>")]
    [Description("Bearer token for the host API. Falls back to PLX_TOKEN or ~/.plx/config.json.")]
    public string? Token { get; init; }

    /// <summary>Default cluster id; falls back to PLX_CLUSTER or the config file.</summary>
    [CommandOption("--cluster <ID>")]
    [Description("Cluster id (overrides the default in the config file).")]
    public string? Cluster { get; init; }

    /// <summary>
    ///     Resolve the merged configuration: this command's flags
    ///     win over env vars; env vars win over the config file.
    ///     Returns <c>null</c> when host / token cannot be resolved.
    /// </summary>
    /// <returns>Resolved config or <c>null</c> when required fields are missing.</returns>
    public PlxConfig? ResolveConfig()
    {
        return PlxConfig.Resolve(Host, Token, Cluster);
    }
}