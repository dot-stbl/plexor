# Change: phase-4-5-d-storage-network

## Why

Phase 4.5 shipped `IQuotaEnforcer` + the `storage.volumes.*`
+ `network.floating_ips.count` +
`network.load_balancers.count` catalog keys, but the storage
and network resource modules didn't exist yet — the catalog
was seeded with placeholder keys and no actual `Volume` /
`Bucket` / `FloatingIp` / `LoadBalancer` entities backed
them. A self-hosted deploy has no way to create a volume,
allocate a floating IP, or stand up a load balancer today.

Phase 4.5.d closes the gap: two new modules (Storage + Network)
that own their respective resources, wire into the quota
enforcer, expose REST endpoints, and ship the admin UI to
list / inspect them.

## What

### Storage module (`Plexor.Modules.Storage`)

Schema `storage`. Two entities:

- `Volume` — a block-storage volume attached to a node.
  Fields: `Id : VolumeId`, `OrgId`, `Name`, `SizeGb`,
  `NodeId?` (assigned lazily), `Status`,
  `CreatedAt / UpdatedAt`. `UNIQUE (OrgId, Name)`.
- `Bucket` — an S3-compatible object store bucket.
  Fields: `Id : BucketId`, `OrgId`, `Name`, `Region`,
  `SizeBytes`, `ObjectCount`. `UNIQUE (OrgId, Name)`.

REST endpoints (all gated by `[RequirePermission]`):

- `POST /api/v1/storage/volumes` (`storage.create`) — create
  a volume (pre-check + reserve
  `storage.volumes.count` + `storage.volumes.gb`).
- `GET /api/v1/storage/volumes` (`storage.read`) — list.
- `GET /api/v1/storage/volumes/{id}` (`storage.read`) —
  detail.
- `PATCH /api/v1/storage/volumes/{id}` (`storage.update`) —
  resize (see the UpdateSize fix below).
- `DELETE /api/v1/storage/volumes/{id}` (`storage.delete`) —
  delete.
- Same five endpoints for `buckets` (`storage.buckets.*`
  permission strings, separate from `storage.volumes.*`).

Quota integration:
`CreateVolumeAsync` calls
`IQuotaEnforcer.CheckAndReserveAsync(scope, "storage.volumes.count", 1)`
+ `CheckAndReserveAsync(scope, "storage.volumes.gb", sizeGb)`
inside the resource-create transaction. Failure rolls back.

The `UpdateSizeAsync` quota delta fix
(`Plexor.Modules.Storage.Application.Volumes.UpdateVolumeSizeAsync`)
calls `CheckAndReserveAsync(scope, "storage.volumes.gb",
sizeDelta)` (positive on grow, negative on shrink) — same
advisory lock + same DB transaction as the create path.

### Network module (`Plexor.Modules.Network`)

Schema `network`. Two entities:

- `FloatingIp` — a public IP address assignable to a node.
  Fields: `Id : FloatingIpId`, `OrgId`, `Address` (IPv4
  literal), `NodeId?` (assigned), `Status`,
  `CreatedAt / UpdatedAt`. `UNIQUE (OrgId, Address)`.
- `LoadBalancer` — a TCP/HTTP load balancer fronting one
  or more workloads. Fields: `Id : LoadBalancerId`,
  `OrgId`, `Name`, `Algorithm` (`round-robin` | `least-conn`),
  `Port`, `TargetWorkloadIds` (jsonb array of UUID v7),
  `Status`. `UNIQUE (OrgId, Name)`.

REST endpoints (mirroring storage):

- `POST /api/v1/network/floating-ips` (`network.create`) —
  pre-check + reserve `network.floating_ips.count`.
- `GET /api/v1/network/floating-ips` (`network.read`) —
  list.
- `GET /api/v1/network/floating-ips/{id}` (`network.read`) —
  detail.
- `PATCH /api/v1/network/floating-ips/{id}` (`network.update`)
  — reassign to a node.
- `DELETE /api/v1/network/floating-ips/{id}` (`network.delete`)
  — release.
- Same five endpoints for `load-balancers`.

Quota integration:
`CreateFloatingIpAsync` calls
`CheckAndReserveAsync(scope, "network.floating_ips.count", 1)`.
`CreateLoadBalancerAsync` calls
`CheckAndReserveAsync(scope, "network.load_balancers.count", 1)`.
Same advisory lock + same transaction shape as storage.

### IStorageQuotaReader / INetworkQuotaReader

A new seam that lets other modules (compute, audit) query
the storage / network resource counts without going through
the quota system. The read-side equivalent of
`IQuotaEnforcer`; lives in `Plexor.Shared.Kernel.Storage` /
`Plexor.Shared.Kernel.Network`.

```csharp
public interface IStorageQuotaReader
{
    Task<long> CountVolumesAsync(QuotaScope scope, CancellationToken ct);
    Task<long> TotalVolumeGbAsync(QuotaScope scope, CancellationToken ct);
}

public interface INetworkQuotaReader
{
    Task<long> CountFloatingIpsAsync(QuotaScope scope, CancellationToken ct);
    Task<long> CountLoadBalancersAsync(QuotaScope scope, CancellationToken ct);
}
```

Implementation reads directly from the Storage / Network
DbContext with the same tenant filter as every other Plexor
read.

### Admin UI

- `AdminStoragePage` — list + create + resize + delete
  volumes; same for buckets. Lives under
  `/admin/storage`.
- `AdminNetworkPage` — list + create + delete + reassign
  floating IPs; same for load balancers. Lives under
  `/admin/network`.

## Impact

- **`storage` capability** (new) — schema `storage`, owned
  by `Plexor.Modules.Storage`. The promoted spec is in
  `openspec/specs/storage/spec.md`.
- **`network` capability** (new) — schema `network`, owned
  by `Plexor.Modules.Network`. The promoted spec is in
  `openspec/specs/network/spec.md`.
- **`quotas` capability** — three quota keys get real
  backing (storage.volumes.count + storage.volumes.gb +
  network.floating_ips.count +
  network.load_balancers.count). The reservation
  semantics in `IQuotaEnforcer` are unchanged.
- **`identity` capability** — four new permission strings
  per module (`storage.create / read / update / delete` +
  `network.create / read / update / delete` +
  `storage.buckets.*`). The built-in `admin` role carries
  the `*` wildcard and covers all of them.
- **`Plexor.Host/Program.cs`** — registers
  `AddStorageModule()` + `AddNetworkModule()`.
- **`Plexor.Migrator`** — adds `InitStorage` +
  `InitNetwork` migrations; the catalog keys from
  Phase 4.5.a are referenced by name (no schema change —
  the keys already exist in `quotas.quota_definitions`).

## Out of scope

- **Ceph RBD / OpenStack Cinder drivers** — v0.1 stores the
  resource records; the actual storage backend is a separate
  provider (Phase 5+).
- **Bucket S3-compatible backend** — the `Bucket` entity
  exists; the S3 wire surface (list / put / get) ships
  later.
- **Floating-IP allocation pool** — v0.1 records the IP
  literal the operator supplies; the pool manager (giving
  out IPs from a CIDR) is Phase 5+.
- **Load-balancer traffic steering** — the `LoadBalancer`
  entity records the config; the actual LB process (HAProxy
  / Envoy) is Phase 5+.
- **Cross-cluster resource sharing** — Phase 5+ with
  multi-cluster.
- **Snapshot / clone** — Phase 5+.
