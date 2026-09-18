// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtNetListParser — file-static parser for `virsh net-list
// --all` output. Pure function; no I/O, no DI. Output format (with
// --all) is the standard table:
//
//     Name                 State      Autostart     Persistent
//     ----------------------------------------------------------
//     default              active     yes           yes
//     plexor-prod-vpc      active     yes           yes
//
// Some libvirt versions add a "---------------------------------------------------------- "
// row after the header; we skip any line that doesn't start with a
// non-whitespace, non-'-' character. Names with spaces are not
// supported (matches our naming regex ^[a-zA-Z0-9_-]{1,16}$).
// ============================================================================

namespace Plexor.Shared.Network;

/// <summary>
///     Pure parser for <c>virsh net-list --all</c> output. Pulls
///     the first whitespace-delimited column (the network name)
///     off every non-header, non-rule line.
/// </summary>
public static class LibvirtNetListParser
{
    /// <summary>
    ///     Parse the output of <c>virsh net-list --all</c> into a
    ///     list of network names (in the order libvirt printed
    ///     them). Lines that look like headers, table rules, or
    ///     blanks are skipped. A name is the first whitespace-
    ///     delimited token on its line.
    /// </summary>
    /// <param name="stdout">Captured stdout from the virsh invocation.</param>
    /// <returns>
    ///     Read-only list of network names. Empty when stdout is
    ///     blank or every line was filtered out.
    /// </returns>
    public static IReadOnlyList<string> ParseNames(string stdout)
    {
        var names = new List<string>();
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return names;
        }

        foreach (var rawLine in stdout.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            // Skip the header row and the table rule.
            if (line.StartsWith("Name", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("---", StringComparison.Ordinal)
                || line.StartsWith("===", StringComparison.Ordinal))
            {
                continue;
            }

            var firstTokenEnd = line.IndexOf(' ');
            var name = firstTokenEnd < 0 ? line : line[..firstTokenEnd];
            if (name.Length == 0)
            {
                continue;
            }

            names.Add(name);
        }

        return names;
    }
}