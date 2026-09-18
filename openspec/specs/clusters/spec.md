# Capability: clusters

## Purpose

The Plexor.Host control plane + the Plexor.NodeAgent data-plane
agents that join it. A cluster is one control-plane process
running on a self-hosted host plus zero or more node agents that
have joined via the join flow. Clusters own the workload lifecycle
on those nodes: pending → provisioning → running → stopped /
failed.

Self-hosted is the only shape in MVP: one host + N node agents =
one cluster. Multi-cluster (one Host orchestrating many
clusters across hosts) is Phase 7+ and out of scope for v0.1.

This capability is owned by `Plexor.Modules.Clusters`. Schema name
in SQL and migrations: `forge` (the architecture theme name; the
C# module project is `Plexor.Modules.Clusters`). C# concept
names: `Cluster`, `Node`, `Workload`, `JoinToken`, `NodeCommand`.

## Requirements

### Requirement: One Cluster = one control plane + N nodes

The system SHALL model a `Cluster`
(`Plexor.Modules.Clusters.Domain.Cluster`) as one
`Plexor.Host` control plane and the set of
`Plexor.NodeAgent` nodes that have joined it via a `JoinToken`.

A self-hosted Plexor install SHALL have exactly one Cluster row
created by `plx init` (the ISO installer or the on-Ubuntu
init flow). Multi-cluster deploys (one Host orchestrating many
clusters across hosts) are Phase 7+ and SHALL NOT be supported
in v0.1 — the data model MAY carry the FK, but no controller
or migration SHALL create more than one cluster per Host.

### Requirement: Cluster lifecycle starts with `plx init`

The system SHALL create a Cluster row only via the `plx init`
command (or the ISO installer). `plx init` SHALL:

1. Probe the host for `/dev/kvm`, libvirtd, openvswitch,
   ceph-mon, postgresql, nats availability.
2. Persist the operator's selection in `/etc/plexor/install.yaml`
   (compute, network, storage, state, event bus providers).
3. Initialize the `forge` schema with a single Cluster row
   representing this host, plus the `clusters` join URL, the
   host's WireGuard public key, and the host's binary version
   (`HostVersion`).

A cluster created outside `plx init` (e.g. by direct DB insert
or a future API) is a violation of the install contract and
SHALL fail at startup with a stable error code.

### Requirement: JoinToken for node authentication

The system SHALL issue `JoinToken`
(`Plexor.Modules.Clusters.Domain.JoinToken`) rows to authenticate
node agents joining a cluster. Issuance happens via
`POST /api/v1/compute/clusters/{id}/tokens` and the token is
shown **once** in the response — only the SHA-256 hash is
persisted.

The token format SHALL be a 256-bit base64url secret prefixed by
the token id (`tok_<UUIDv7>`). The join flow uses
`POST /api/v1/compute/clusters/join` with
`{ "token": "...", "wireguard_public_key": "...", "hostname":
"...", "iso_version": "...", "spec": { ... } }`.

A token SHALL be single-use: consumption flips `Status` to
`Revoked` and records the joining node's id in
`RedeemedByNodeId`. Default TTL is 7 days. Revoked or expired
tokens SHALL return 401 on attempt-to-redeem.

The `JoinToken` placeholder currently in
`Plexor.Shared.NodeApi/NodeContracts.cs` SHALL be replaced by
API-key auth in this capability's Phase 4 swap (see
`openspec/specs/identity/spec.md` §"API key replaces JoinToken
placeholder").

### Requirement: Node joins via join URL + one-time token

The system SHALL accept node join requests via the cluster's
`Endpoint` URL plus a one-time join token. On successful join
the node SHALL:

1. Be recorded in a `Node` row
   (`Plexor.Modules.Clusters.Domain.Node`).
2. Report its hardware snapshot (CPU cores, RAM bytes, disk
   bytes, network interfaces) at first heartbeat.
3. Begin posting heartbeats every 30 seconds; three missed
   heartbeats (90 s silence) flips `NodeStatus` to `Gone`.
4. Receive a NodeAgent-issued mTLS client cert for ongoing
   communication.

The cluster's host MUST verify the token's hash, mark the token
revoked, then accept subsequent mTLS connections from the
joining node's cert (CN = node id).

### Requirement: Workloads run on a runtime

The system SHALL model workloads as `Workload`
(`Plexor.Modules.Clusters.Domain.Entities.Workload`) rows with a
`Kind` discriminator — `"vm"`, `"lxc"`, `"k8s.pod"`, or
`"container"`. The control plane records the operator's
specification (`SpecJson`), the assigned node
(`AssignedNodeId`), and the lifecycle state reported by the
NodeAgent (`Pending` → `Provisioning` → `Running` → `Stopped` or
`Failed`).

The runtime type and the host it runs on are decoupled: a
`vm` workload may be assigned to any node that exposes a
hypervisor capability, a `k8s.pod` to any node that runs k3s.
v0.1 MVP runtimes are `docker-compose`, `podman-quadlet`, and
`k3s` — others may be added via install providers.

### Requirement: Cluster-level RuntimeId is immutable

The system SHALL store a `Cluster.RuntimeId`
(`docker-compose` / `podman-quadlet` / `k3s`) on each cluster.
`RuntimeId` SHALL be set at cluster creation and SHALL NOT be
mutable afterwards. Switching runtime (e.g. `docker-compose` →
`k3s`) SHALL require creating a new cluster and migrating
workloads explicitly.

The default runtime at `plx init` SHALL be
`Plexor.Shared.NodeApi.ClusterRuntimeIds.Default` (currently
`docker-compose`).

### Requirement: WireGuard mesh for node-to-host mTLS

The system SHALL use WireGuard for node-to-host networking. Each
cluster has a host-side WireGuard public key
(`Cluster.WireguardPublicKey`) populated on first boot. Each
node records its own WireGuard public key
(`Node.WireguardPublicKey`) during the join handshake.

The control plane and the agent SHALL authenticate the
WireGuard mesh using these keys (peer allowlist) and SHALL layer
mTLS on top for application-level identity (X.509 client cert
with CN = node id).

### Requirement: NodeCommand for control-plane → agent commands

The system SHALL persist control-plane → node commands as
`NodeCommand`
(`Plexor.Modules.Clusters.Domain.Entities.NodeCommand`) rows in
`forge.commands`, one table per node. Status moves through
`Pending` → `Sent` → `Acked` or `Failed`.

The agent SHALL poll `/nodes/{nodeId}/commands/poll` for pending
entries, execute via the registered `ICommandExecutor`, and post
results via `/nodes/{nodeId}/commands/{cmdId}/result`. The
`CommandId` field is stable across retries so the control plane
can dedupe.

### Requirement: Version compatibility check

The system SHALL compare the cluster's `HostVersion` against the
node's `IsoVersion` at every heartbeat. A version mismatch
SHALL warn (log + audit event) but SHALL NOT reject — the
operator may be mid-upgrade. Hard rejection is reserved for
wire-format-breaking changes (Phase 7+).

### Requirement: WorkloadLifecycleState state machine

The system SHALL route every state transition on a `Workload`
row through `WorkloadStateMachine`
(`Plexor.Modules.Clusters.Application.Workloads.WorkloadStateMachine`).

The state machine SHALL recognize five states:

- `Pending` — workload created but not yet scheduled.
- `Provisioning` — NodeAgent accepted the work; the runtime
  is allocating.
- `Running` — active on the runtime.
- `Stopped` — stopped (preserved on disk).
- `Failed` — provisioning or runtime error.

The state machine SHALL enforce the transition graph:

- `Pending` → `Provisioning` — operator Start.
- `Pending` → `Failed` — operator delete (cancelled).
- `Provisioning` → `Running` — NodeAgent reports success
  (`LocalId` populated).
- `Provisioning` → `Failed` — NodeAgent reports failure.
- `Running` → `Stopped` — operator Stop.
- `Running` → `Failed` — NodeAgent reports runtime error.
- `Stopped` → `Pending` — operator Restart.

A transition that is not in the graph SHALL be rejected
with a stable error code
(`compute.workload.invalid_transition`). The state machine
is the **only** place `Workload.State` is mutated; direct
assignments are a code-review reject.

### Requirement: IComputeProvider seam

The system SHALL expose `IComputeProvider`
(`Plexor.Shared.Kernel.Compute.IComputeProvider`) with the
following surface:

```csharp
public interface IComputeProvider
{
    string ProviderId { get; }
    Task<ComputeWorkloadHandle> StartAsync(ComputeWorkloadSpec spec, CancellationToken ct);
    Task StopAsync(ComputeWorkloadHandle handle, CancellationToken ct);
    Task<ComputeWorkloadState> GetStateAsync(ComputeWorkloadHandle handle, CancellationToken ct);
    Task DeleteAsync(ComputeWorkloadHandle handle, CancellationToken ct);
}
```

The interface is the seam every compute backend
implements. `ComputeWorkloadHandle` carries the
provider-specific runtime handle (libvirt UUID, k3s pod
name, or — for the NoOp provider — a synthetic GUID).

`IComputeProvider` is owned by
`Plexor.Shared.Kernel.Compute` so NodeAgent + the cluster
module both depend on it.

### Requirement: NoOpComputeProvider default

The system SHALL ship `NoOpComputeProvider`
(`Plexor.Shared.Kernel.Compute.NoOpComputeProvider`) as
the v0.1 default implementation:

- `ProviderId = "noop"`.
- `StartAsync` mints a `ComputeWorkloadHandle` with
  `LocalId = Guid.NewGuid()` and returns.
- `StopAsync` and `DeleteAsync` are no-ops (return
  `Task.CompletedTask`).
- `GetStateAsync` always returns `Running`.

The FE SHALL display a "no-op provider" badge next to
workloads backed by `NoOpComputeProvider` so operators
don't confuse "Running" with "actually exists".

The NodeAgent registers `NoOpComputeProvider` as the
default in v0.1. Real providers (libvirt, k3s) replace
the registration via
`services.AddSingleton<IComputeProvider, LibvirtComputeProvider>()`
in `Plexor.NodeAgent/Program.cs`.

### Requirement: ComputeWorkloadSpec vs Workload.SpecJson

The NodeAgent SHALL parse the opaque `Workload.SpecJson`
into a typed `ComputeWorkloadSpec`
(`Plexor.Shared.Kernel.Compute.ComputeWorkloadSpec`)
before forwarding to `IComputeProvider.StartAsync`.

`ComputeWorkloadSpec` SHALL carry at minimum:

- `Kind : string` — `vm` | `lxc` | `k8s.pod` | `container`.
- `Image : string?` — image name (libvirt / QEMU volume
  source).
- `Cpu : int` — vCPU count.
- `RamGb : int` — RAM in GiB.
- `DiskGb : int` — root disk in GiB.
- `ExtraJson : string?` — provider-specific extra config
  (passed through unchanged).

`Workload.SpecJson` remains the operator-facing surface;
provider-specific extras live there for backends that
need them (e.g. KVM's `<os><type arch="...">`, k3s's
`resources.limits`).

### Requirement: WorkloadStateChanged audit event

The system SHALL publish a `WorkloadStateChanged` domain
event on every successful transition. The audit module
SHALL consume the event and write an
`atlas.audit_entries` row:

- `action = "workload.state.changed"`.
- `target_type = "Workload"`.
- `target_id = <workload id>`.
- `payload_json = { "from": "<state>", "to": "<state>",
  "actorUserId": "..." }`.

A consumer who wants the full timeline for a workload
queries
`GET /api/v1/audit?actionPrefix=workload.state.*&targetId=X`.

### Requirement: Start / Stop / Delete commands route through IComputeProvider

The control plane SHALL route every
`StartWorkloadCommand`, `StopWorkloadCommand`, and
`DeleteWorkloadCommand` through `IComputeProvider`:

- `StartWorkloadCommandHandler` →
  `IComputeProvider.StartAsync(spec, ct)`. The returned
  `ComputeWorkloadHandle.LocalId` is persisted on the
  `Workload` row.
- `StopWorkloadCommandHandler` →
  `IComputeProvider.StopAsync(handle, ct)`. The state
  transitions `Running` → `Stopped`.
- `DeleteWorkloadCommandHandler` →
  `IComputeProvider.DeleteAsync(handle, ct)`. The workload
  row is deleted.

The NodeAgent's command-dispatch pipeline
(`/nodes/{id}/commands/poll`) SHALL poll the control
plane, look up the workload's `LocalId`, and call the
provider on the agent host.

### Requirement: LibvirtComputeProvider

The system SHALL ship `LibvirtComputeProvider`
(`Plexor.NodeAgent.Compute.LibvirtComputeProvider`) as
the libvirt implementation of `IComputeProvider`.

`LibvirtComputeProvider` SHALL:

- `ProviderId = "libvirt"`.
- `StartAsync(spec, ct)` — build a `virt-install` command
  line from `ComputeWorkloadSpec`
  (`--name`, `--uuid`, `--vcpus`, `--ram`, `--disk size=`,
  `--import`, `--os-variant`), run it via the shell
  helper, and return a `ComputeWorkloadHandle` with
  `LocalId = <libvirt-uuid>`. Timeout:
  `LibvirtComputeOptions.StartTimeoutSeconds` (default
  300, range 60–1800).
- `StopAsync(handle, ct)` — `virsh shutdown <uuid>` with
  grace-period fallback. After
  `LibvirtComputeOptions.StopGracePeriodSeconds` (default
  30, range 5–120), the provider falls back to
  `virsh destroy <uuid>`.
- `GetStateAsync(handle, ct)` — `virsh dominfo <uuid>`,
  parses the `State:` line, maps to
  `ComputeWorkloadState` (`running` / `shut off` /
  `paused` / `crashed` / `pmsuspended`).
- `DeleteAsync(handle, ct)` — `virsh undefine <uuid>
  --nvram` (NVRAM cleanup for UEFI guests) + `virsh
  vol-delete` for the backing storage if the volume was
  provisioned by Plexor. A failed undefine is logged; the
  workload row is deleted regardless (best-effort
  cleanup).

### Requirement: LibvirtShell helper

The system SHALL ship `LibvirtShell`
(`Plexor.NodeAgent.Compute.LibvirtShell`) — an
`internal static class` that wraps every `virsh` /
`virt-install` invocation:

```csharp
public static Task<LibvirtCommandResult> RunAsync(
    string[] args, TimeSpan timeout, CancellationToken ct);
```

`LibvirtShell` SHALL:

- Use `Process.Start` with `UseShellExecute = false` (per
  `cross-platform.md` §8).
- Kill the process tree on cancellation.
- Surface full `stderr` on non-zero exit (no truncation).
- Log every invocation at `LogLevel.Debug` with the full
  arg list (no secrets — `virt-install` arguments don't
  carry secrets).

`LibvirtCommandResult` is a record
`(ExitCode : int, Stdout : string, Stderr : string)`.

### Requirement: LibvirtComputeOptions

The system SHALL expose `LibvirtComputeOptions`
(`Plexor.NodeAgent.Configuration.LibvirtComputeOptions`,
`SectionName = "Clusters:Compute:Libvirt"`):

- `LibvirtUri : string` (default `"qemu:///system"`).
- `DefaultPool : string` (default `"default"`).
- `BaseImageDir : string`
  (default `"/var/lib/libvirt/images"`).
- `StartTimeoutSeconds : int` (range 60–1800, default
  300).
- `StopGracePeriodSeconds : int` (range 5–120, default
  30).

`ComputeInstaller.AddComputeProvider` SHALL bind +
validate the options with
`ValidateDataAnnotations().ValidateOnStart()`. A missing
or malformed section fails the NodeAgent boot, not the
first workload start.

### Requirement: Conditional DI registration

The NodeAgent SHALL register `LibvirtComputeProvider` as
the `IComputeProvider` implementation ONLY when the
`[Clusters:Compute:Libvirt]` config section is present.
Otherwise the `NoOpComputeProvider` default stays
registered.

The conditional registration lives in
`Plexor.NodeAgent.Installers.ComputeInstaller.AddComputeProvider`:
the installer reads the section via
`config.GetSection(LibvirtComputeOptions.SectionName).Exists()`,
branches, and registers the appropriate provider.

### Requirement: Base image provisioner

The system SHALL ship `BaseImageProvisioner`
(`Plexor.NodeAgent.Compute.BaseImageProvisioner`) that
ensures a libvirt volume exists for a given image
reference:

```csharp
public Task<LocalImageHandle> EnsureBaseImageAsync(
    string imageRef, CancellationToken ct);
```

The provisioner SHALL:

1. Compute the SHA-256 of the published image.
2. Check the local libvirt pool for a volume with the
   matching checksum.
3. On a hit, return the existing volume handle.
4. On a miss, download the image from
   `[Clusters:Compute:ImageRegistry]` via the default
   `IHttpClientFactory` resilience pipeline, verify the
   checksum, and create a libvirt volume from the file.
5. Return the new volume handle.

The first workload that references a new image pays the
download cost; subsequent workloads reuse the volume.
The download path uses `IHttpClientFactory` with a
5-minute timeout. Authenticated pulls are Phase 5+.

### Requirement: Graceful shutdown vs forced destroy

The system SHALL prefer ACPI shutdown (`virsh shutdown`)
to forced destroy (`virsh destroy`). The provider runs
the ACPI shutdown first; if the guest is still running
after `StopGracePeriodSeconds`, the provider invokes
`virsh destroy` as a fallback.

The grace period is configurable per-host via
`LibvirtComputeOptions.StopGracePeriodSeconds`. An
operator who knows their workload is ACPI-broken can
shorten the grace (min 5s); an operator with
slow-shutdown guests can extend it (max 120s). The
default of 30s is the right balance for typical Linux
guests.

### Requirement: NVRAM cleanup for UEFI guests

The system SHALL pass `--nvram` to `virsh undefine` so
NVRAM storage is deleted alongside the domain definition.
A failed undefine (e.g. NVRAM file locked by a stuck
qemu) is logged at `LogLevel.Warning` and the workload
row is deleted regardless (the libvirt domain is
best-effort cleanup; the workload row is the source of
truth).

### Requirement: Workload.LocalId equals the libvirt UUID

The system SHALL mint a UUID v7 in C# and pass it via
`virt-install --uuid <UUID>`. The provider populates
`Workload.LocalId` with the same UUID — Plexor's row and
the libvirt domain are the same string, no translation
table. `virsh domuuid <name>` returns the same value if
verified post-create.

## Key Entities

### `Cluster`

`Plexor.Modules.Clusters.Domain.Cluster`
(schema `forge.clusters`). Fields:

- `Id : ClusterId` — strongly-typed `cluster_<UUIDv7>` PK.
- `Name : string` — operator label, unique per org.
- `OrgId : Guid` — tenant scope.
- `Region : string` — operator-assigned region label
  (`eu-central-1`).
- `Status : ClusterStatus` — lifecycle status.
- `WireguardPublicKey : string` — host's WireGuard key.
- `JoinTokenExpiresAt : DateTimeOffset?` — current active
  token expiry (null when no token is in flight).
- `InstallProviders : IReadOnlyList<string>` — chosen at
  `plx init` (`kvm`, `lxc`, `pod`, `ovs`, `cilium`).
- `HostVersion : string` — running Plexor.Host binary version.
- `Endpoint : string` — where the host is reachable
  (`plx node join <endpoint>`).
- `RuntimeId : string` — `docker-compose` | `podman-quadlet`
  | `k3s`; immutable after creation.
- `CreatedAt : DateTimeOffset`, `UpdatedAt : DateTimeOffset`.

### `Node`

`Plexor.Modules.Clusters.Domain.Node`
(schema `forge.nodes`). Fields:

- `Id : NodeId` — strongly-typed `node_<UUIDv7>` PK.
- `ClusterId : ClusterId` — FK to parent cluster.
- `OrgId : Guid` — denormalized.
- `Hostname : string` — unique per cluster
  (`UNIQUE (ClusterId, Hostname)`).
- `Role : NodeRole` — role within the cluster.
- `Status : NodeStatus` — `Joining` | `Healthy` | `Degraded`
  | `Gone`.
- `Spec : NodeSpec` — hardware snapshot at join.
- `IsoVersion : string` — ISO image version.
- `LastHeartbeatAt : DateTimeOffset?`.
- `CreatedAt : DateTimeOffset`, `UpdatedAt : DateTimeOffset`.
- `WireguardPublicKey : string`.
- `VmCount : int` — current VM count scheduled on this node.

### `Workload`

`Plexor.Modules.Clusters.Domain.Entities.Workload`
(schema `forge.workloads`). Fields:

- `Id : WorkloadId` — strongly-typed `wl_<UUIDv7>` PK.
- `ClusterId : ClusterId` — parent cluster.
- `AssignedNodeId : NodeId?` — null during provisioning.
- `LocalId : string?` — runtime handle returned by the agent
  (libvirt UUID, k3s pod name).
- `Name : string` — operator label, unique per cluster.
- `Kind : string` — `vm` | `lxc` | `k8s.pod` | `container`.
- `SpecJson : string` — operator config (image, env, ports,
  volumes).
- `State : WorkloadState` — current state.
- `LastMessage : string?` — last error or note from agent.
- `LastReportedAt : DateTimeOffset?`.
- `CreatedAt : DateTimeOffset`, `UpdatedAt : DateTimeOffset`.

### `JoinToken`

`Plexor.Modules.Clusters.Domain.JoinToken`
(schema `forge.join_tokens`). Fields:

- `Id : TokenId` — strongly-typed `tok_<UUIDv7>` PK.
- `ClusterId : ClusterId` — FK.
- `OrgId : Guid` — denormalized.
- `Label : string` — short human label
  (`node-1 (prod-eu-1)`).
- `Status : TokenStatus` — `Active` | `Revoked`.
- `TokenHash : string` — SHA-256 of base64url secret.
- `IntendedRole : NodeRole` — restricts the token to a role.
- `MinIsoVersion : string` — version check.
- `IssuedAt : DateTimeOffset`.
- `CreatedAt : DateTimeOffset`.
- `ExpiresAt : DateTimeOffset` — default 7 days.
- `RedeemedByNodeId : NodeId?` — set on successful join.

### `NodeCommand`

`Plexor.Modules.Clusters.Domain.Entities.NodeCommand`
(schema `forge.commands`). Fields:

- `Id : Guid` — surrogate (UUID v7).
- `NodeId : NodeId` — target node.
- `CommandId : Guid` — stable wire id (UUID v7).
- `Type : string` — e.g. `workload.start`, `workload.stop`.
- `PayloadJson : string` — opaque JSON body.
- `Status : NodeCommandStatus` —
  `Pending` | `Sent` | `Acked` | `Failed`.
- `ResultJson : string?` — null until Acked or Failed.
- `CreatedAt : DateTimeOffset`.
- `CompletedAt : DateTimeOffset?`.