// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodesListSettings — `plx host nodes list`. Inherits the
// connection options from HostSettings; no extra flags.
// ============================================================================

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx host nodes list</c>. Inherits
///     <c>--host / --token / --cluster</c> from
///     <see cref="HostSettings" />; no extra flags.
/// </summary>
public sealed class HostNodesListSettings : HostSettings;