// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InstallerCli — synchronous dispatcher for `plx` commands.
// ============================================================================
// Rule: synchronous return (int) at the CLI layer; per-command handlers
// are async (each handler is its own method, properly Async-suffixed).
// Real implementation in follow-up phase:
//   plx init         install a cluster
//   plx upgrade      apply a new version
//   plx status       show cluster health
//   plx providers    list installed providers + capabilities
//   plx doctor       diagnose issues
//   plx destroy      tear down (with --keep-data option)
// ============================================================================

namespace Plexor.Installer;

internal static class InstallerCli
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        Console.WriteLine($"plx 0.1.0-dev (skeleton) — unknown command: {args[0]}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Plexor Installer CLI 0.1.0-dev");
        Console.WriteLine();
        Console.WriteLine("Usage: plx <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  init         Install a Plexor cluster on this host");
        Console.WriteLine("  upgrade      Apply a new version to the cluster");
        Console.WriteLine("  status       Show cluster health");
        Console.WriteLine("  providers    List installed providers");
        Console.WriteLine("  doctor       Diagnose issues");
        Console.WriteLine("  destroy      Tear down the cluster");
        Console.WriteLine();
        Console.WriteLine("Run `plx <command> --help` for command-specific options.");
    }
}