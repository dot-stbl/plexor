namespace Plexor.Shared.NodeApi;

/// <summary>
///     Canonical wire-format command-type strings used by the
///     control-plane to node-agent envelope protocol. Every constant
///     here is the <c>Type</c> value carried on a
///     <c>CommandEnvelope</c>; producers and consumers share these
///     identifiers so a typo cannot silently desync the two sides.
/// </summary>
public static class WireCommandTypes
{
    /// <summary><c>workload.create</c> — provision a new workload.</summary>
    public const string WorkloadCreate = "workload.create";

    /// <summary><c>workload.start</c> — boot a provisioned workload.</summary>
    public const string WorkloadStart = "workload.start";

    /// <summary><c>workload.stop</c> — gracefully shut down a workload.</summary>
    public const string WorkloadStop = "workload.stop";

    /// <summary><c>workload.delete</c> — remove a workload and its storage.</summary>
    public const string WorkloadDelete = "workload.delete";

    /// <summary>
    ///     <c>workload.action</c> — multiplexed envelope for the
    ///     start / stop / delete lifecycle; the agent re-reads the
    ///     envelope to pick the concrete action.
    /// </summary>
    public const string WorkloadAction = "workload.action";
}
