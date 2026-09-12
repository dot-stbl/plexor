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