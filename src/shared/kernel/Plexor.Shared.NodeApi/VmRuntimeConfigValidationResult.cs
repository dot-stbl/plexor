// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmRuntimeConfigValidationResult — outcome of running a
// VmRuntimeConfig through VmRuntimeConfigValidator. A list of
// human-readable error strings instead of one exception so the
// handler can surface ALL violations to the operator at once (a
// 400 with "Vcpu must be in [1,256] AND RamBytes must be >=
// 512MiB" is more useful than two round-trips).
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Validation outcome for a <see cref="VmRuntimeConfig" />.
/// </summary>
/// <param name="IsValid">
///     True when <paramref name="Errors" /> is empty. Convenience
///     so callers can branch on a single field instead of
///     <c>Errors.Count == 0</c>.
/// </param>
/// <param name="Errors">
///     Empty when the config is valid; one entry per failed rule
///     otherwise. Each entry is a one-line human-readable message
///     the operator can act on.
/// </param>
public sealed record VmRuntimeConfigValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
