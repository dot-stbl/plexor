// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CidataCleanup — file-static helper for deleting cloud-init cidata
// files. Lives in its own file per class-decomposition (helper
// without dependencies → file-static class, not private method on
// the provider). Best-effort; missing-file is silent success
// (matches the "delete is eventual" semantic the agent's
// provider layer applies to volume / network cleanup).
// ==========================================================================

namespace Plexor.NodeAgent.Providers.Image.CloudInit;

/// <summary>
///     Best-effort delete of a cidata ISO file. Used by the
///     KVM provider on both the create-fail path and the
///     workload-delete path so the agent doesn't leak files
///     under <c>/var/lib/plexor/cidata/</c> across failures.
/// </summary>
public static class CidataCleanup
{
    /// <summary>
    ///     Delete the cidata ISO at <paramref name="path" /> if
    ///     non-null. Silent on missing file or directory
    ///     (matches the idempotent delete contract every other
    ///     Plexor compute backend follows).
    /// </summary>
    /// <param name="path">Absolute path to the ISO. Null is a no-op.</param>
    public static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (FileNotFoundException)
        {
            // Already gone — fine.
        }
        catch (DirectoryNotFoundException)
        {
            // Parent directory vanished mid-cleanup (operator
            // unmounted the cidata volume?). Can't do anything
            // useful.
        }
    }
}

