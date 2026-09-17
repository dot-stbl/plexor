// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmListSettings — `plx vm list`. Inherits the connection options
// from HostSettings and adds page-size knobs. `--page` defaults to
// 1, `--page-size` to 25; the host clamps `pageSize` to [1, 100].
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx vm list</c>. Inherits <c>--host / --token
///     / --cluster</c> from <see cref="HostSettings" />.
/// </summary>
public sealed class VmListSettings : HostSettings
{
    /// <summary>1-based page index. Default 1.</summary>
    [CommandOption("--page <N>")]
    [Description("1-based page index (default 1).")]
    public int Page { get; init; } = 1;

    /// <summary>Items per page. Default 25; host clamps to [1, 100].</summary>
    [CommandOption("--page-size <N>")]
    [Description("Items per page (default 25; host clamps to [1, 100]).")]
    public int PageSize { get; init; } = 25;
}
