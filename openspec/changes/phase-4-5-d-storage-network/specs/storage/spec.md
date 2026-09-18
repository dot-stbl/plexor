# Spec delta: storage (phase-4-5-d-storage-network)

This file is the proposed state of
`openspec/specs/storage/spec.md` after the change
`openspec/changes/phase-4-5-d-storage-network/` is merged.
It uses the `## ADDED Requirements` convention from
OpenSpec — every requirement here is new.

When the change lands, this delta is promoted into
`openspec/specs/storage/spec.md` under `## Requirements`,
and the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: Volume entity

The system SHALL expose a `Volume` entity
(`Plexor.Modules.Storage.Domain.Entities.Volume`,
schema `storage.volumes`) for block-storage volumes
attached to Plexor nodes.

`Volume` SHALL carry:

- `Id : VolumeId` — strongly-typed `vol_<UUIDv7>` PK.
- `OrgId : Guid` — tenant scope (denormalized for tenant
  filter).
- `Name : string` — operator label, unique per org
  (`UNIQUE (OrgId, Name)`).
- `SizeGb : int` — current allocated size (positive).
- `NodeId : NodeId?` — assigned node id; null until a node
  claims the volume.
- `Status : VolumeStatus` — `Available` | `Attaching` |
  `Attached` | `Detaching` | `Deleting`.
- `CreatedAt : DateTimeOffset`, `UpdatedAt :
  DateTimeOffset`.

### Requirement: Bucket entity

The system SHALL expose a `Bucket` entity
(`Plexor.Modules.Storage.Domain.Entities.Bucket`,
schema `storage.buckets`) for S3-compatible object
storage buckets.

`Bucket` SHALL carry:

- `Id : BucketId` — strongly-typed `buc_<UUIDv7>` PK.
- `OrgId : Guid` — tenant scope.
- `Name : string` — DNS-label-compatible name
  (`[a-z0-9-]{3,63}`), unique per org
  (`UNIQUE (OrgId, Name)`).
- `Region : string` — operator-supplied region label
  (`eu-central-1`).
- `SizeBytes : long` — current consumed bytes.
- `ObjectCount : long` — current object count.
- `CreatedAt : DateTimeOffset`, `UpdatedAt :
  DateTimeOffset`.

### Requirement: CreateVolumeAsync with quota reservation

The system SHALL expose
`CreateVolumeCommandHandler`
(`Plexor.Modules.Storage.Application.Volumes.CreateVolumeCommandHandler`)
that:

1. Validates the request (FluentValidation: `name` non-empty
   ≤ 64 chars; `sizeGb > 0`).
2. Calls
   `IQuotaEnforcer.CheckAndReserveAsync(scope, "storage.volumes.count", 1)`.
3. Calls
   `IQuotaEnforcer.CheckAndReserveAsync(scope, "storage.volumes.gb", sizeGb)`.
4. Inserts the `Volume` row.
5. Commits the transaction. Both reservations release on
   failure (the transaction is the same as the INSERT).

A reservation failure (`QuotaCheckResult.Denied`) raises
`QuotaExceededException` → HTTP 429 ProblemDetails with
`code = "quotas.exceeded"`, `extensions.limit`,
`extensions.used`, `extensions.requested`.

### Requirement: UpdateVolumeSizeAsync with quota delta

The system SHALL expose
`UpdateVolumeSizeCommandHandler` that resizes a volume:

1. Reads the current `Volume` row.
2. Computes `sizeDelta = newSize - currentSize`
   (positive on grow, negative on shrink).
3. Calls
   `IQuotaEnforcer.CheckAndReserveAsync(scope, "storage.volumes.gb", sizeDelta)`.
   The enforcer accepts negative amounts (a release) and
   adjusts `QuotaUsage.CurrentValue` accordingly.
4. UPDATEs the `Volume.SizeGb` field.
5. Commits the transaction.

Grow that exceeds the effective GB limit returns 429
(QuotaExceededException). Shrink never fails — there is no
lower bound on `SizeGb` other than `> 0` (enforced by the
validator).

### Requirement: IStorageQuotaReader

The system SHALL expose
`IStorageQuotaReader`
(`Plexor.Shared.Kernel.Storage.IStorageQuotaReader`) for
read-side consumers (audit, compute, dashboards):

```csharp
public interface IStorageQuotaReader
{
    Task<long> CountVolumesAsync(QuotaScope scope, CancellationToken ct);
    Task<long> TotalVolumeGbAsync(QuotaScope scope, CancellationToken ct);
}
```

`EfStorageQuotaReader` (`Plexor.Modules.Storage.Infrastructure`)
implements the interface by querying
`storage.volumes` directly with the same tenant filter as
every other Plexor read. The interface is the seam that
keeps the quota system from leaking its internal
`QuotaUsage` rows.

### Requirement: REST endpoints for volumes

The system SHALL expose the following endpoints, all behind
`Plexor.Shared.Authorization.RequirePermissionAttribute`:

- `POST /api/v1/storage/volumes` (`storage.create`) —
  create.
- `GET /api/v1/storage/volumes?cursor=&limit=` (`storage.read`)
  — list with cursor pagination.
- `GET /api/v1/storage/volumes/{id}` (`storage.read`) —
  detail.
- `PATCH /api/v1/storage/volumes/{id}` (`storage.update`) —
  resize + rename.
- `DELETE /api/v1/storage/volumes/{id}` (`storage.delete`) —
  delete (decrements both quota counters atomically).

### Requirement: REST endpoints for buckets

The system SHALL expose the same five endpoints for buckets:

- `POST /api/v1/storage/buckets` (`storage.buckets.create`).
- `GET /api/v1/storage/buckets` (`storage.buckets.read`).
- `GET /api/v1/storage/buckets/{id}` (`storage.buckets.read`).
- `PATCH /api/v1/storage/buckets/{id}`
  (`storage.buckets.update`).
- `DELETE /api/v1/storage/buckets/{id}`
  (`storage.buckets.delete`).

### Requirement: Permission strings

The system SHALL expose the following permission strings
in the role permission catalog:

- `storage.create`, `storage.read`, `storage.update`,
  `storage.delete` — for volume operations.
- `storage.buckets.create`, `storage.buckets.read`,
  `storage.buckets.update`, `storage.buckets.delete` — for
  bucket operations.

The built-in `admin` role carries the `*` wildcard and
covers all eight. The built-in `viewer` role carries
`storage.read` + `storage.buckets.read` for read-only
dashboards. No seeder change required — the wildcard is
the standard pattern.

### Requirement: Standard 401 / 403 / 404 contract

The system SHALL apply the standard Plexor contract on
every storage endpoint:

- **No bearer / invalid bearer** → HTTP 401 with
  `identity.token.*` code.
- **Valid bearer, missing permission** → HTTP 403 with
  `code = "identity.permission.denied"`.
- **Valid bearer, valid permission, wrong tenant** → HTTP
  404 (never 403 — never leak existence).

### Requirement: Migration order

`storage.*` migrations SHALL be applied after `realm`,
`sigil`, `atlas`, and `quotas` (FK in spirit — the quota
enforcer reads quota assignments and the audit module
references storage resources). The `Plexor.Migrator`
orders schemas by FK dependency:
`realm` → `sigil` → `atlas` → `quotas` → `storage` →
`network`. The `InitStorage` migration is the only
storage migration in v0.1.

## ADDED Key Entities

### `Volume`

`Plexor.Modules.Storage.Domain.Entities.Volume`
(schema `storage.volumes`). Fields as listed above.

### `Bucket`

`Plexor.Modules.Storage.Domain.Entities.Bucket`
(schema `storage.buckets`). Fields as listed above.

### Value Objects and Exceptions

- `VolumeId` (record struct) — typed wrapper, prefix
  `vol_<UUIDv7>`.
- `BucketId` (record struct) — typed wrapper, prefix
  `buc_<UUIDv7>`.
- `VolumeStatus` (enum) — `Available` | `Attaching` |
  `Attached` | `Detaching` | `Deleting`.
- `StoragePermissions` (constants) — eight permission
  strings listed above.
