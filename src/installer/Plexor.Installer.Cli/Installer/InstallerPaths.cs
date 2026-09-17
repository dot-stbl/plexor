// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InstallerPaths — resolves and validates the on-host paths the
// installer mutates (data dir, binary dir, systemd unit path,
// current-executable path). All paths are Linux-first; the Windows
// fallback uses the analogous %ProgramData% / %LocalAppData% locations.
//
// Path resolution rules (filesystem-paths.md §3):
//   - /var/lib/plexor               data + state (Plexor home)
//   - /var/lib/plexor/bin           host binaries
//   - /var/lib/plexor/config        runtime config (plexor.toml)
//   - /etc/systemd/system           unit files (system-level service)
//
// Override via env:
//   PLEXOR_DATA_PATH               data dir override
//   PLEXOR_BIN_PATH                bin dir override (default: data/bin)
//   PLEXOR_SYSTEMD_UNIT_PATH        systemd unit dir override
//
// All validation lives here (Validate() / TryResolve) so commands
// stay focused on orchestration and the validation rules are unit-
// testable as pure functions.
// ============================================================================

namespace Plexor.Installer.Cli.Installer;

/// <summary>
///     Path resolution + validation for the installer commands.
///     Pure functions only — no I/O, no DI.
/// </summary>
public static class InstallerPaths
{
    private const string DefaultDataPath = "/var/lib/plexor";
    private const string DefaultSystemdPath = "/etc/systemd/system";
    private const string ServiceName = "plexor-host.service";
    private const string PlexorDataEnv = "PLEXOR_DATA_PATH";
    private const string PlexorSystemdEnv = "PLEXOR_SYSTEMD_UNIT_PATH";
    private const string PlexorBinEnv = "PLEXOR_BIN_PATH";

    /// <summary>Default host data directory (where Plexor stores state).</summary>
    public static string DataDirectory => ResolvePath(PlexorDataEnv, DefaultDataPath);

    /// <summary>Binary directory under <see cref="DataDirectory" />.</summary>
    public static string BinDirectory =>
        Environment.GetEnvironmentVariable(PlexorBinEnv) ?? Path.Combine(DataDirectory, "bin");

    /// <summary>Runtime config directory under <see cref="DataDirectory" />.</summary>
    public static string ConfigDirectory => Path.Combine(DataDirectory, "config");

    /// <summary>Where the host service unit file lives.</summary>
    public static string SystemdDirectory =>
        Environment.GetEnvironmentVariable(PlexorSystemdEnv) ?? DefaultSystemdPath;

    /// <summary>Full path to the systemd unit file for the host service.</summary>
    public static string UnitFilePath => Path.Combine(SystemdDirectory, ServiceName);

    /// <summary>
    ///     Path the running binary currently lives at. Empty string if
    ///     unavailable (NativeAOT entry point may run via a temp
    ///     single-file extraction in some configurations).
    /// </summary>
    public static string CurrentExecutablePath
    {
        get
        {
            try
            {
                return Environment.ProcessPath ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    ///     Result of validating a path layout. <see cref="IsValid" />
    ///     indicates whether the layout is acceptable; <see cref="Error" />
    ///     carries the human reason when not.
    /// </summary>
    /// <param name="IsValid"></param>
    /// <param name="Error"></param>
    public sealed record ValidationResult(bool IsValid, string? Error)
    {
        /// <summary>Singleton — validation succeeded.</summary>
        public static ValidationResult Ok { get; } = new(true, null);

        /// <summary>Build a failure result from the reason.</summary>
        /// <param name="reason"></param>
        public static ValidationResult Fail(string reason)
        {
            return new ValidationResult(false, reason);
        }
    }

    /// <summary>
    ///     Validate that the resolved layout is sane:
    ///     data dir absolute, bin dir absolute + under data dir,
    ///     config dir absolute + under data dir, systemd unit path
    ///     absolute and ends in <c>.service</c>.
    /// </summary>
    public static ValidationResult Validate()
    {
        var data = DataDirectory;
        if (!Path.IsPathRooted(data))
        {
            return ValidationResult.Fail($"data dir not absolute: {data}");
        }

        var bin = BinDirectory;
        if (!Path.IsPathRooted(bin))
        {
            return ValidationResult.Fail($"bin dir not absolute: {bin}");
        }

        if (!bin.StartsWith(data, StringComparison.Ordinal))
        {
            return ValidationResult.Fail($"bin dir must live under data dir: {bin} ∉ {data}");
        }

        var config = ConfigDirectory;
        if (!Path.IsPathRooted(config))
        {
            return ValidationResult.Fail($"config dir not absolute: {config}");
        }

        if (!config.StartsWith(data, StringComparison.Ordinal))
        {
            return ValidationResult.Fail($"config dir must live under data dir: {config} ∉ {data}");
        }

        var unit = UnitFilePath;
        if (!Path.IsPathRooted(unit))
        {
            return ValidationResult.Fail($"unit path not absolute: {unit}");
        }

        if (!unit.EndsWith(".service", StringComparison.Ordinal))
        {
            return ValidationResult.Fail($"unit path must end with .service: {unit}");
        }

        return ValidationResult.Ok;
    }

    private static string ResolvePath(string envVar, string fallback)
    {
        var overrideValue = Environment.GetEnvironmentVariable(envVar);
        return string.IsNullOrWhiteSpace(overrideValue) ? fallback : overrideValue;
    }
}
