// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Wire-format DTOs between Plexor.Host (control plane) and
// Plexor.NodeAgent (worker on every compute node). All records are
// immutable, AOT-friendly (no reflection-based serialization), and
// serialize/deserialize via System.Text.Json source generators on each
// side. Mirrors contracts/plexor.openapi.yaml exactly.
//
// XML doc convention (enforced by CS1591, see .agents/rules/
// engineering-process.md rule 3): every public type/member has at

using System.Text.Json.Serialization;
// least a `<summary>`. `<param>` only when the name+type does not
// make the purpose obvious. Public modifier goes on the record /
// interface, NOT on positional record params (that is a C# syntax
// error).
// ============================================================================

using System.Text.Json;

namespace Plexor.Shared.NodeApi;

// ---------------------------------------------------------------------------
// Hardware snapshot
// ---------------------------------------------------------------------------

/// <summary>
///     Hardware probe values reported by the agent on join and on every
///     heartbeat. The control plane uses these for capacity planning and
///     to display the node list in the UI.
/// </summary>
/// <param name="DiskBytes">
///     Root filesystem capacity by default; the
///     agent exposes a single number for v0.1 and the control plane treats
///     it as advisory (not a reservation limit).
/// </param>
public sealed record NodeHardware(
    int CpuCores,
    long RamBytes,
    long DiskBytes,
    string Hostname,
    string KernelVersion);

// ---------------------------------------------------------------------------
// Workload kinds
// ---------------------------------------------------------------------------

/// <summary>
///     Identifies which provider a workload targets. Each sealed record is
///     the wire name the agent dispatches on (e.g. <c>"vm"</c>, <c>"lxc"</c>).
///     Adding a new kind is a sealed record + matching provider
///     implementation in the NodeAgent — no other shared-contract changes.
/// </summary>
public abstract record WorkloadKind
{
    /// <summary>Wire name (e.g. <c>"vm"</c>, <c>"lxc"</c>, <c>"k8s.pod"</c>).</summary>
    public abstract string Name { get; }

    /// <summary>KVM/QEMU virtual machine via libvirt.</summary>
    public sealed record Vm : WorkloadKind
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "vm";
    }

    /// <summary>LXC system container via libvirt (uri <c>lxc:///</c>).</summary>
    public sealed record Lxc : WorkloadKind
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "lxc";
    }

    /// <summary>Kubernetes pod scheduled by a k3s / upstream k8s API.</summary>
    public sealed record K8sPod : WorkloadKind
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "k8s.pod";
    }

    /// <summary>Generic container (podman / docker) on the host.</summary>
    public sealed record Container : WorkloadKind
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "container";
    }
}

/// <summary>
///     Polymorphic specification for a workload. <see cref="Kind" />
///     selects which provider parses <see cref="Config" />; each provider
///     ignores fields it doesn't recognize (schema is provider-defined,
///     not part of the shared wire contract). The control plane serializes
///     the provider-specific config verbatim.
/// </summary>
/// <param name="Config">
///     Provider-specific JSON. Each provider defines
///     its own schema; the shared contract is opaque JSON.
/// </param>
public sealed record WorkloadSpec(
    WorkloadKind Kind,
    string Name,
    JsonElement Config);

// ---------------------------------------------------------------------------
// Command types
// ---------------------------------------------------------------------------

/// <summary>
///     Abstract base for command type identifiers. Concrete commands are
///     sealed records inheriting from this. String-typed <see cref="Name" />
///     so System.Text.Json can serialize without a custom converter and
///     adding a new command type doesn't require a schema-regenerating
///     build (the Agent dispatches on the string at runtime).
/// </summary>
public abstract record CommandType
{
    /// <summary>Wire command name (e.g. <c>"workload.create"</c>).</summary>
    public abstract string Name { get; }

    /// <summary><c>workload.create</c> — provision a new workload of the specified kind.</summary>
    public sealed record WorkloadCreate : CommandType
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "workload.create";
    }

    /// <summary><c>workload.start</c> — boot a previously provisioned workload.</summary>
    public sealed record WorkloadStart : CommandType
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "workload.start";
    }

    /// <summary><c>workload.stop</c> — gracefully shut down a running workload.</summary>
    public sealed record WorkloadStop : CommandType
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "workload.stop";
    }

    /// <summary><c>workload.delete</c> — remove a workload and its backing storage.</summary>
    public sealed record WorkloadDelete : CommandType
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "workload.delete";
    }
}

// ---------------------------------------------------------------------------
// Command outcome
// ---------------------------------------------------------------------------

/// <summary>
///     Outcome reported by the agent back to the control plane. The
///     <c>ErrorMessage</c> field on the result envelope is required when
///     the status is <see cref="Failed" /> and must be null otherwise —
///     the deserializer on the Host side validates the invariant.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CommandResultStatus>))]
public enum CommandResultStatus
{
    /// <summary>Command executed without error.</summary>
    Succeeded,

    /// <summary>
    ///     Command failed; <c>ErrorMessage</c> on the result envelope
    ///     is populated with a one-line human-readable detail.
    /// </summary>
    Failed
}

// ---------------------------------------------------------------------------
// Join (first message the node sends)
// ---------------------------------------------------------------------------

/// <summary>
///     First message the node sends. The control plane issues a join
///     token out-of-band (UI or installer); the agent presents it here.
///     Returns the assigned <c>NodeId</c> and the canonical control-plane
///     URL the agent should use for subsequent heartbeats and command
///     polls. The URL may differ from what the agent derived (e.g.
///     behind a reverse proxy, a private network, or a HA failover).
/// </summary>
public sealed record JoinRequest(
    string JoinToken,
    NodeHardware Hardware);

/// <summary>
///     Reply to the join request. <see cref="NodeId" /> is the canonical
///     handle the agent uses for all subsequent messages.
/// </summary>
public sealed record JoinResponse(
    Guid NodeId,
    Uri ControlPlaneUrl);

// ---------------------------------------------------------------------------
// Heartbeat
// ---------------------------------------------------------------------------

/// <summary>
///     Periodic liveness signal. The interval is set by the agent (30s
///     for v0.1); the control plane flips the node to <c>Offline</c> if
///     it hasn't seen a heartbeat in 3 intervals.
/// </summary>
/// <param name="SentAt">
///     UTC time the heartbeat was sent (so the
///     control plane can spot clock-skew across many nodes).
/// </param>
public sealed record HeartbeatRequest(
    Guid NodeId,
    DateTimeOffset SentAt,
    NodeHardware Hardware,
    int RunningVmCount);

// ---------------------------------------------------------------------------
// Command poll + result
// ---------------------------------------------------------------------------

/// <summary>
///     A command envelope. <see cref="Type" /> selects the executor on the
///     agent side; <see cref="PayloadJson" /> is the raw JSON body the
///     executor deserializes into its own strongly-typed args record. The
///     agent doesn't need to know the full command catalog — it just
///     dispatches on <c>Type</c>.
/// </summary>
public sealed record CommandEnvelope(
    Guid CommandId,
    Guid NodeId,
    string Type,
    string PayloadJson,
    DateTimeOffset IssuedAt);

/// <summary>
///     Result envelope posted by the agent after it executes a command.
///     On failure, <c>ErrorMessage</c> carries a one-line human-readable
///     detail; the control plane may log it and surface it in the UI.
/// </summary>
public sealed record CommandResult(
    Guid CommandId,
    Guid NodeId,
    CommandResultStatus Status,
    string? ErrorMessage,
    DateTimeOffset CompletedAt);

/// <summary>
///     Long-poll request body. Returns immediately if no commands are
///     queued; otherwise returns the next batch. The agent issues
///     another poll as soon as the response is received.
/// </summary>
public sealed record CommandPollRequest(
    Guid NodeId,
    int MaxBatch,
    long? WaitCursor);

/// <summary>
///     Reply to a long-poll. <see cref="NextCursor" /> is the cursor the
///     agent sends on the next poll so it sees only newer commands.
/// </summary>
public sealed record CommandPollResponse(
    IReadOnlyList<CommandEnvelope> Commands,
    long NextCursor);
