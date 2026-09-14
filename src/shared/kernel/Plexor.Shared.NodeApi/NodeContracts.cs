// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Wire-format DTOs between Plexor.Host (control plane) and
// Plexor.NodeAgent (worker on every compute node). All records are
// immutable, AOT-friendly (no reflection-based serialization), and
// serialize/deserialize via System.Text.Json source generators on each
// side. Mirrors contracts/plexor.openapi.yaml exactly.
//
// This file holds the polymorphic *abstract* records (WorkloadKind +
// CommandType) and their enums (CommandResultStatus +
// WorkloadReportState). Individual top-level DTOs (JoinRequest /
// HeartbeatRequest / CommandEnvelope / etc.) live in their own files
// in this same namespace — see JoinRequest.cs et al. Splitting
// happened in Sprint 3 (item 5) per folder-organization.md §1.
//
// XML doc convention (enforced by CS1591, see .agents/rules/
// engineering-process.md rule 3): every public type/member has at
// least a `<summary>`. `<param>` only when the name+type does not
// make the purpose obvious. Public modifier goes on the record /
// interface, NOT on positional record params (that is a C# syntax
// error).
// ============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plexor.Shared.NodeApi;

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
        public override string Name => WireCommandTypes.WorkloadCreate;
    }

    /// <summary><c>workload.start</c> — boot a previously provisioned workload.</summary>
    public sealed record WorkloadStart : CommandType
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => WireCommandTypes.WorkloadStart;
    }

    /// <summary><c>workload.stop</c> — gracefully shut down a running workload.</summary>
    public sealed record WorkloadStop : CommandType
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => WireCommandTypes.WorkloadStop;
    }

    /// <summary><c>workload.delete</c> — remove a workload and its backing storage.</summary>
    public sealed record WorkloadDelete : CommandType
    {
        /// <summary>Wire name (see <see cref="CommandType.Name" />).</summary>
        public override string Name => WireCommandTypes.WorkloadDelete;
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
// Workload report state (kept here because it's a wire-stable enum used
// alongside WorkloadReport; the report record itself is its own file).
// ---------------------------------------------------------------------------

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
