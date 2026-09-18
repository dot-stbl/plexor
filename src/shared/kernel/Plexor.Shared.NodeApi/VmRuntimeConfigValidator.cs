// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmRuntimeConfigValidator — pure-function validator for
// VmRuntimeConfig. Lives in Plexor.Shared.NodeApi so both the host
// (CreateVm handler) and the agent (libvirt provider's defense-in-
// depth re-check before translating to XML) use the same rules.
//
// Rules (Phase 1, see issue #4):
//   - Vcpu in [1, 256]
//   - RamBytes in [512 MiB, 1 TiB]
//   - DiskBytes in [1 GiB, 10 TiB]
//   - ImageRef not null and not whitespace
//   - NetworkName null OR matches ^[a-zA-Z0-9_-]{1,16}$
//   - SshKeyFingerprint null OR non-empty
//
// Validation returns ALL failures at once (a list, not a single
// throw). The control plane maps the resulting non-empty Errors
// list to a 400 ProblemDetails with each error on its own line.
// ============================================================================

using System.Text.RegularExpressions;

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Stateless validator for <see cref="VmRuntimeConfig" />.
///     Exposed as a static class because there is no per-instance
///     state; tests construct no object, they just call
///     <see cref="Validate" />.
/// </summary>
public static partial class VmRuntimeConfigValidator
{
    /// <summary>Minimum vCPU count (inclusive).</summary>
    public const int MinVcpu = 1;

    /// <summary>Maximum vCPU count (inclusive).</summary>
    public const int MaxVcpu = 256;

    /// <summary>Minimum memory in bytes (512 MiB).</summary>
    public const long MinRamBytes = 512L * 1024 * 1024;

    /// <summary>Maximum memory in bytes (1 TiB).</summary>
    public const long MaxRamBytes = 1024L * 1024 * 1024 * 1024;

    /// <summary>Minimum disk size in bytes (1 GiB).</summary>
    public const long MinDiskBytes = 1024L * 1024 * 1024;

    /// <summary>Maximum disk size in bytes (10 TiB).</summary>
    public const long MaxDiskBytes = 10L * 1024 * 1024 * 1024 * 1024;

    /// <summary>
    ///     Network-name pattern: letters, digits, hyphen, underscore;
    ///     length 1..16. Matches libvirt's network-name grammar
    ///     for our use (we don't allow dots because libvirt's default
    ///     'default' is fine and we never need FQDN-shaped names).
    /// </summary>
    private static readonly Regex NetworkNamePattern = MyRegex();

    /// <summary>
    ///     Run every rule against <paramref name="config" /> and
    ///     return the aggregate outcome. Pure function — no I/O,
    ///     no allocations beyond the result list. Safe to call from
    ///     the hot path.
    /// </summary>
    /// <param name="config">The candidate config to validate.</param>
    /// <returns>
    ///     A result with <c>IsValid=true</c> when every rule passed;
    ///     otherwise <c>IsValid=false</c> and one entry per failed
    ///     rule in <c>Errors</c>.
    /// </returns>
    public static VmRuntimeConfigValidationResult Validate(VmRuntimeConfig config)
    {
        var errors = new List<string>();

        if (config.Vcpu is < MinVcpu or > MaxVcpu)
        {
            errors.Add(
                $"Vcpu must be in [{MinVcpu}, {MaxVcpu}] (was {config.Vcpu}).");
        }

        if (config.RamBytes is < MinRamBytes or > MaxRamBytes)
        {
            errors.Add(
                $"RamBytes must be in [{MinRamBytes}, {MaxRamBytes}] (was {config.RamBytes}).");
        }

        if (config.DiskBytes is < MinDiskBytes or > MaxDiskBytes)
        {
            errors.Add(
                $"DiskBytes must be in [{MinDiskBytes}, {MaxDiskBytes}] (was {config.DiskBytes}).");
        }

        if (string.IsNullOrWhiteSpace(config.ImageRef))
        {
            errors.Add("ImageRef must not be empty.");
        }

        if (config.NetworkName is not null && !NetworkNamePattern.IsMatch(config.NetworkName))
        {
            errors.Add(
                $"NetworkName must match ^[a-zA-Z0-9_-]{{1,16}}$ when set (was '{config.NetworkName}').");
        }

        if (config.SshKeyFingerprint is not null && string.IsNullOrWhiteSpace(config.SshKeyFingerprint))
        {
            errors.Add("SshKeyFingerprint, when provided, must not be empty.");
        }

        return new VmRuntimeConfigValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors);
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]{1,16}$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
}
