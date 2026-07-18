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

using System.Text.Json;
using System.Text.Json.Serialization;
// least a `<summary>`. `<param>` only when the name+type does not
// make the purpose obvious. Public modifier goes on the record /
// interface, NOT on positional record params (that is a C# syntax
// error).
// ============================================================================

namespace Plexor.Shared.NodeApi;

// ---------------------------------------------------------------------------
// Hardware snapshot
// ---------------------------------------------------------------------------

/// <summary>
///     Hardware probe values reported by the agent on join and on every
///     heartbeat. The control plane uses these for capacity planning and
///     to display the node list in the UI.
/// </summary>
/// <param name="CpuCores"></param>
/// <param name="RamBytes"></param>
/// <param name="DiskBytes">
///     Root filesystem capacity by default; the
///     agent exposes a single number for v0.1 and the control plane treats
///     it as advisory (not a reservation limit).
/// </param>
/// <param name="Hostname"></param>
/// <param name="KernelVersion"></param>
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

    /// <summary>
    ///     QEMU software-emulated VM via libvirt (uri
    ///     <c>qemu:///system</c>, domain type <c>qemu</c>, no KVM).
    ///     Useful for running VMs on hosts without hardware
    ///     virtualization extensions; significantly slower than
    ///     KVM but functionally equivalent.
    /// </summary>
    public sealed record Qemu : WorkloadKind
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "qemu";
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

    /// <summary>
    ///     Single-host multi-container workload deployed via
    ///     <c>docker compose up -d</c>. Rendered to a
    ///     <c>docker-compose.yaml</c> file on the target node;
    ///     the agent invokes the docker CLI.
    /// </summary>
    public sealed record DockerCompose : WorkloadKind
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "docker-compose";
    }

    /// <summary>
    ///     Single-host container deployed as a systemd quadlet
    ///     unit (<filename>.container</filename>) under
    ///     <c>/etc/containers/systemd/</c>. The agent manages
    ///     the unit via <c>systemctl daemon-reload</c> +
    ///     <c>systemctl start &lt;name&gt;</c>. Rootless-friendly
    ///     alternative to docker-compose; default runtime on
    ///     RHEL/Alma/Fedora hosts.
    /// </summary>
    public sealed record PodmanQuadlet : WorkloadKind
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "podman-quadlet";
    }

    /// <summary>
    ///     Kubernetes workload deployed via <c>kubectl apply -k</c>
    ///     against an existing k3s cluster on the target node.
    ///     Rendered to a kustomize directory; the agent invokes
    ///     <c>kubectl --kubeconfig=/etc/rancher/k3s/k3s.yaml</c>.
    ///     Provisioning of the k3s cluster itself is out of scope
    ///     (see <c>plan-k8s</c>); this provider assumes k3s is
    ///     already installed.
    /// </summary>
    public sealed record K3s : WorkloadKind
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => "k3s";
    }
}

/// <summary>
///     Polymorphic specification for a workload. <see cref="Kind" />
///     selects which provider parses <see cref="Config" />; each provider
///     ignores fields it doesn't recognize (schema is provider-defined,
///     not part of the shared wire contract). The control plane serializes
///     the provider-specific config verbatim.
/// </summary>
/// <param name="Kind"></param>
/// <param name="Name"></param>
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
    Succeeded = 0,

    /// <summary>
    ///     Command failed; <c>ErrorMessage</c> on the result envelope
    ///     is populated with a one-line human-readable detail.
    /// </summary>
    Failed = 1
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
/// <param name="JoinToken"></param>
/// <param name="Hardware"></param>
public sealed record JoinRequest(
    string JoinToken,
    NodeHardware Hardware);

/// <summary>
/// <para>
///     Result of a successful <see cref="JoinRequest" />. The
///     NodeAgent persists these and uses them for every
///     subsequent call.
/// </para>
/// <para>
///     The mTLS triple (<see cref="NodeCertificatePem" /> /
///     <see cref="NodePrivateKeyPem" /> / <see cref="CaCertificatePem" />)
///     is issued by the host on every successful join — the
///     NodeAgent writes the cert + key to disk and uses them as
///     the client cert on every subsequent HTTPS call. The CA
///     certificate is included so the NodeAgent can verify the
///     host's server cert (mutual trust at the TLS layer).
/// </para>
/// </summary>
/// <param name="NodeId">Plexor node id (node_&lt;UUIDv7&gt;).</param>
/// <param name="ControlPlaneUrl">Post-join rendezvous point.</param>
/// <param name="NodeCertificatePem">PEM-encoded client cert (CN=node_&lt;id&gt;, signed by Plexor CA).</param>
/// <param name="NodePrivateKeyPem">PKCS#8 PEM-encoded private key for the client cert.</param>
/// <param name="CaCertificatePem">PEM-encoded Plexor CA root.</param>
public sealed record JoinResponse(
    Guid NodeId,
    Uri ControlPlaneUrl,
    string NodeCertificatePem,
    string NodePrivateKeyPem,
    string CaCertificatePem);

// ---------------------------------------------------------------------------
// Heartbeat
// ---------------------------------------------------------------------------

/// <summary>
///     One workload's current state, as reported by the node in
///     its most recent <see cref="HeartbeatRequest" />. The control
///     plane reconciles these reports against its durable
///     <c>forge.workloads</c> view and updates each
///     <c>LastReportedAt</c> + <c>State</c> accordingly. Drift
///     detection (Phase D Tier 4) consumes these to surface
///     "VM says Running but control-plane says Provisioning" to
///     the operator.
/// </summary>
/// <param name="WorkloadId">
///     Control-plane workload id (<c>wl_&lt;UUIDv7&gt;</c>).
///     The agent doesn't generate this; the control plane does,
///     at workload-create time, and stashes it in the
///     <c>workload.create</c> payload the agent received. The
///     agent echoes it back here verbatim.
/// </param>
/// <param name="LocalId">
///     Provider-assigned id (libvirt domain UUID, container id,
///     k8s pod name, etc.). Stable across start/stop on the
///     same workload; the control plane uses it for the
///     <c>Start/Stop/Delete</c> action commands.
/// </param>
/// <param name="Name">Human-facing workload name (matches <c>WorkloadSpec.Name</c>).</param>
/// <param name="State">Current lifecycle state.</param>
public sealed record WorkloadReport(
    Guid WorkloadId,
    string? LocalId,
    string Name,
    WorkloadReportState State);

/// <summary>
///     Lifecycle state reported by the agent. Mirrors
///     <c>Plexor.Shared.Workloads.WorkloadState</c> but is
///     wire-stable — the shared contract doesn't follow
///     internal enum additions.
/// </summary>
public enum WorkloadReportState
{
    /// <summary>Agent is creating the workload (image pull, etc.).</summary>
    Provisioning = 0,

    /// <summary>Workload is booted and accepting traffic.</summary>
    Running = 1,

    /// <summary>Workload is gracefully shut down.</summary>
    Stopped = 2,

    /// <summary>Last lifecycle operation failed.</summary>
    Failed = 3,

    /// <summary>Provider can't determine state.</summary>
    Unknown = 4
}

/// <summary>
///     Periodic liveness signal. The interval is set by the agent (30s
///     for v0.1); the control plane flips the node to <c>Offline</c> if
///     it hasn't seen a heartbeat in 3 intervals. The
///     <see cref="Reports" /> list drives Phase D Tier 4 drift
///     detection — the control plane reconciles each report
///     against its durable <c>forge.workloads</c> view.
/// </summary>
/// <param name="NodeId"></param>
/// <param name="SentAt">
///     UTC time the heartbeat was sent (so the
///     control plane can spot clock-skew across many nodes).
/// </param>
/// <param name="Hardware"></param>
/// <param name="RunningVmCount">
///     Convenience aggregate — the number of
///     workloads the agent currently has in <c>Running</c>
///     state. Equal to <c>reports.Count(r =&gt; r.State ==
///     Running)</c>; included so the control plane's per-node
///     dashboard can show a number without parsing the full
///     report list.
/// </param>
/// <param name="Reports">
///     Per-workload state reports. Empty when the node
///     hasn't provisioned any workloads yet.
/// </param>
public sealed record HeartbeatRequest(
    Guid NodeId,
    DateTimeOffset SentAt,
    NodeHardware Hardware,
    int RunningVmCount,
    IReadOnlyList<WorkloadReport> Reports);

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
/// <param name="CommandId"></param>
/// <param name="NodeId"></param>
/// <param name="Type"></param>
/// <param name="PayloadJson"></param>
/// <param name="IssuedAt"></param>
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
/// <param name="CommandId"></param>
/// <param name="NodeId"></param>
/// <param name="Status"></param>
/// <param name="ErrorMessage"></param>
/// <param name="LocalId">
///     Provider-assigned runtime id (libvirt domain UUID, container
///     id, etc.). Populated by the agent for <c>workload.create</c>
///     results so the control plane can write it into
///     <c>forge.workloads.local_id</c>; null for commands that
///     don't return a runtime handle (start / stop / delete
///     ack, heartbeat).
/// </param>
/// <param name="CompletedAt"></param>
public sealed record CommandResult(
    Guid CommandId,
    Guid NodeId,
    CommandResultStatus Status,
    string? ErrorMessage,
    Guid? LocalId,
    DateTimeOffset CompletedAt);

/// <summary>
///     Payload for a <c>workload.create</c> command. Deserialized from
///     <see cref="CommandEnvelope.PayloadJson" /> when the agent
///     dispatches a workload-create envelope. Carries the kind, name, and
///     provider-specific config that the local provider will translate
///     into its native representation (libvirt XML, k3s Pod spec, etc.).
/// </summary>
/// <param name="Spec"></param>
public sealed record CreateWorkloadPayload(
    WorkloadSpec Spec);

/// <summary>
///     Payload for <c>workload.start</c>, <c>workload.stop</c>, and
///     <c>workload.delete</c> commands. The <c>LocalId</c> is the
///     runtime handle the provider assigned at create-time (libvirt
///     domain UUID, container id, k3s pod name) — populated from
///     <c>forge.workloads.local_id</c> which the agent first wrote
///     via the Tier-4 heartbeat reconciliation. Stable across
///     start/stop/delete on the same workload. Stored as a
///     <c>string</c> on the wire because provider-assigned ids are
///     not always valid Guids (e.g. <c>i-abc123</c> for AWS-style
///     providers or containername for docker).
/// </summary>
/// <param name="LocalId">Runtime handle — string shape to match <c>forge.workloads.local_id</c>.</param>
public sealed record WorkloadActionPayload(
    string LocalId);

/// <summary>
///     Long-poll request body. Returns immediately if no commands are
///     queued; otherwise returns the next batch. The agent issues
///     another poll as soon as the response is received.
/// </summary>
/// <param name="NodeId"></param>
/// <param name="MaxBatch"></param>
/// <param name="WaitCursor"></param>
public sealed record CommandPollRequest(
    Guid NodeId,
    int MaxBatch,
    long? WaitCursor);

/// <summary>
///     Reply to a long-poll. <see cref="NextCursor" /> is the cursor the
///     agent sends on the next poll so it sees only newer commands.
/// </summary>
/// <param name="Commands"></param>
/// <param name="NextCursor"></param>
public sealed record CommandPollResponse(
    IReadOnlyList<CommandEnvelope> Commands,
    long NextCursor);
