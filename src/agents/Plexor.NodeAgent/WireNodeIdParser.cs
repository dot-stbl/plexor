// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WireNodeIdParser — wire-format node id → Guid helper. Used by
// NodePollLoop to translate the wire-format NodeId back into a Guid
// for the legacy CommandPollRequest.NodeId field. Tolerates malformed
// input by throwing FormatException; the caller's catch logs the
// failure and retries on the next tick.
//
// v0.1 keeps the Guid field in CommandPollRequest; a future iteration
// (feature/agent-poll-wire-update) will move the field to a wire-
// format string and remove this helper.
// ============================================================================

namespace Plexor.NodeAgent;

/// <summary>
///     Wire-format node id (e.g.
///     <c>node_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d</c>) → Guid.
///     Throws <see cref="FormatException" /> on malformed input.
/// </summary>
internal static class WireNodeIdParser
{
    /// <summary>
    ///     Strip the <c>node_</c> prefix and parse the remaining
    ///     32-char lowercase hex string as a Guid.
    /// </summary>
    /// <param name="wireNodeId"></param>
    public static Guid ParseNodeId(string wireNodeId)
    {
        var span = wireNodeId.AsSpan();
        var underscore = span.IndexOf('_');
        if (underscore < 0)
        {
            throw new FormatException(
                $"Wire node id '{wireNodeId}' is missing the 'node_' prefix.");
        }

        var uuid = span[(underscore + 1)..];
        return Guid.ParseExact(uuid, "N");
    }
}