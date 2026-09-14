// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtConfigDeserializer — file-static helper that wraps
// System.Text.Json.JsonElement.Deserialize<T>() with the project's
// standard "defaults on missing or malformed payload" policy. Every
// libvirt provider deserialises its provider-specific config from the
// control plane, and the wrapper is byte-identical across providers —
// only the target config record type changes.
//
// Extracted in Sprint 3 (item 6) per class-decomposition.md
// (shared helper without DI → file-static class).
// ============================================================================

using System.Text.Json;

namespace Plexor.NodeAgent.Providers;

/// <summary>
///     Shared "deserialize with defaults" wrapper for libvirt
///     provider-specific config records. Used by all three
///     libvirt xml builders (KVM, QEMU, LXC); the QEMU + LXC
///     builders had their own inline copy before the Sprint 3
///     dedup.
/// </summary>
internal static class LibvirtConfigDeserializer
{
    /// <summary>
    ///     Parse the provider-specific JSON config, falling back to
    ///     defaults on a missing / malformed payload so the agent
    ///     stays functional even with empty WorkloadSpec.Config.
    ///     The supplied <paramref name="defaults" /> factory is used
    ///     when the element is null (deserialised to a null record)
    ///     or the deserialiser throws — keeps every provider's
    ///     "missing config ⇒ workable defaults" path identical.
    /// </summary>
    /// <typeparam name="T">Provider-specific config record type.</typeparam>
    /// <param name="config">Raw JSON from the control plane.</param>
    /// <param name="defaults">Factory invoked when parsing fails or yields null.</param>
    /// <param name="result">Resolved config (defaults if parse failed).</param>
    /// <returns><c>true</c> when the JSON parsed cleanly; <c>false</c> when defaults were substituted.</returns>
    public static bool TryDeserialize<T>(JsonElement config, Func<T> defaults, out T result)
        where T : class, new()
    {
        try
        {
            result = config.Deserialize<T>() ?? defaults();
            return true;
        }
        catch
        {
            result = defaults();
            return false;
        }
    }
}
