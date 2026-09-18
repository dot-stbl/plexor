# Design: phase-4-5-d-storage-network

Technical decisions for the Storage + Network modules. Each
section explains the trade-off chosen over the alternatives.

## Module placement

Two new modules, two new schemas:

| Schema | Tables | Owner |
|--------|--------|-------|
| `storage` | `volumes`, `buckets` | `Plexor.Modules.Storage` |
| `network` | `floating_ips`, `load_balancers` | `Plexor.Modules.Network` |

Naming follows the Plexor architecture theme: `storage` and
`network` are single-word one-token identifiers that don't
collide with reserved SQL keywords. The C# class names
(`Volume`, `Bucket`, `FloatingIp`, `LoadBalancer`) match the
domain vocabulary — operators see "volumes" in the UI, not
"storage resources".

## Volume vs Bucket — two resources, one module

Volumes and buckets are different storage primitives
(block vs object). They share the same module because:

1. **Single schema** — both live in `storage.*`.
2. **Common permission namespace** — `storage.create /
   read / update / delete` covers both; the bucket-specific
   operations add `storage.buckets.*` on top.
3. **Shared quota keys** — both consume
   `storage.volumes.count` + `storage.volumes.gb` for
   volumes; buckets are currently uncounted (Phase 5+
   adds `storage.buckets.count` if multi-bucket becomes
   common).

The two have separate `DbSet`s and separate EF configurations;
no entity sharing beyond the schema boundary.

## Why VolumeId / BucketId are typed wrappers

A `VolumeId` is `record struct VolumeId(Guid Value)` —
strongly typed so a `VolumeId` can never be silently passed
where a `BucketId` is expected (and vice versa). Same
pattern as `UserId`, `OrgId`, etc. — the project standard.

The wire format prefix (`vol_<UUIDv7>`,
`buc_<UUIDv7>`) is the typed wrapper's `ToString()`. A
parser in `Plexor.Shared.Kernel.Storage` round-trips the
prefix back to the wrapper.

## UpdateSize quota delta — why it's an enforcer concern

Resizing a volume is a quota-affecting operation:

- **Grow.** `sizeDelta = newSize - oldSize > 0`. The handler
  must reserve the extra GB before the UPDATE commits.
- **Shrink.** `sizeDelta < 0`. The handler can release the
  unused GB immediately (no reservation needed; just
  decrement `QuotaUsage.CurrentValue`).

The naive implementation calls
`CheckAndReserveAsync(scope, "storage.volumes.gb",
newSize)` — but that reserves the full new size, ignoring
the already-reserved old size. The fix: pass
`sizeDelta = newSize - oldSize` so the enforcer sees only
the net change. This is the `UpdateSizeAsync` quota delta
fix.

The advisory lock is the same — `pg_advisory_xact_lock(<scope-hash>)`
at scope granularity. The UPDATE on the Volume row + the
UPDATE on `QuotaUsage` + the caller's COMMIT all happen in
the same transaction.

## LoadBalancer — TargetWorkloadIds as jsonb

A load balancer targets N workloads. Modeling as a
many-to-many via a join table is the relational ideal but
adds a third table + a third EF configuration. The
trade-off accepted:

- **Chosen.** `TargetWorkloadIds : IReadOnlyList<Guid>` on
  the `LoadBalancer` row, mapped to `jsonb` via
  `EF.OwnedTypes` (a typed primitive collection).
  Validation on read rejects malformed UUIDs.
- **Rejected.** Separate `load_balancer_targets` table +
  EF skip-navigation. More moving parts; the join table
  carries no information beyond the FK pair.

`OwnedTypes` with a discriminator avoids the
`jsonb → JsonDocument.Parse` pattern that the project rule
bans (`ef-owned-types.md`).

## Floating IP allocation pool — deferred

v0.1 records the IP literal the operator supplies. An
allocation pool (the system giving out IPs from a CIDR) is
Phase 5+. Reasons:

- **Self-hosted deploy.** One org, one cluster, one node —
  the operator knows the network and provides the IP
  literal.
- **No floating-IP API to integrate with.** Without a
  cloud-provider abstraction, the "give me an IP" call has
  no upstream. The literal-supply shape is a placeholder
  until the provider abstraction lands.
- **Quota semantics unchanged.** Whether the IP came from a
  pool or was supplied literally, the
  `network.floating_ips.count` reservation is the same.

The `FloatingIp.Address` column validates IPv4 (regex
match on the wire format `A.B.C.D`).

## File-static helpers + internal sealed handlers

The handlers are `internal sealed` and `Plexor.Modules.Storage.Unit`
gets access via `InternalsVisibleTo`. The controllers live
in the Api project (`public sealed` — auto-discovered by
`AddControllers()`). Validation + mapping + quota calls all
live in `internal static` helpers next to the handler —
the handler body is the thin orchestration: validate →
fetch → enforcer → save → return.

The same pattern is used for the network module.

## Permission model

| Permission | Granted to | Purpose |
|---|---|---|
| `storage.create` | `admin` (`*`) | Create volume / bucket |
| `storage.read` | All authenticated | List / detail |
| `storage.update` | `admin` (`*`) | Resize / rename |
| `storage.delete` | `admin` (`*`) | Delete volume / bucket |
| `storage.buckets.create` | `admin` (`*`) | Create bucket |
| `storage.buckets.read` | All authenticated | List / detail |
| `storage.buckets.update` | `admin` (`*`) | Rename / set region |
| `storage.buckets.delete` | `admin` (`*`) | Delete bucket |
| `network.create` | `admin` (`*`) | Create floating IP / LB |
| `network.read` | All authenticated | List / detail |
| `network.update` | `admin` (`*`) | Reassign / reconfigure |
| `network.delete` | `admin` (`*`) | Delete floating IP / LB |

The `admin` role carries `*` so all twelve strings are
covered by default. `viewer` is intentionally excluded
from the write permissions — auditors read, they don't
allocate.

## Migration order

`storage` migrations apply after `realm` (FK in spirit — the
scope filter walks Realm), `sigil` (FK in spirit — actor
trace), `atlas` (FK in spirit — audit rows reference
storage resources), `quotas` (FK — the enforcer reads quota
assignments). The `Plexor.Migrator` orders schemas by FK
dependency:

```
realm → sigil → atlas → quotas → storage → network
```

The `InitStorage` and `InitNetwork` migrations are the only
storage / network migrations in v0.1.

## Open questions deferred

- **Ceph RBD / OpenStack Cinder.** v0.1 records the volume;
  the actual block-storage driver ships later.
- **Bucket S3-compatible wire surface.** The entity exists;
  list / put / get lands with the S3-compatible provider.
- **Floating-IP allocation pool.** Phase 5+ with the
  network-provider abstraction.
- **Load-balancer traffic steering.** The HAProxy / Envoy
  controller ships with the network-provider abstraction.
- **Volume snapshots.** Phase 5+.
- **Cross-region resource replication.** Phase 7+ with
  multi-cluster.
