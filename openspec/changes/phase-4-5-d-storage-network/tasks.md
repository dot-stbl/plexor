# Tasks: phase-4-5-d-storage-network

Numbered checklist. Each sub-section is one or more commits.

## 4.5.d.1 — Storage module (Domain + Infrastructure)

- [x] `Plexor.Modules.Storage` project structure
      (Domain / Application / Infrastructure / Api).
- [x] Domain entities: `Volume`, `Bucket` (init-only
      properties).
- [x] Value objects: `VolumeId` / `BucketId` (`vol_<UUIDv7>` /
      `buc_<UUIDv7>` typed wrappers).
- [x] EF DbContext (`StorageDbContext`) +
      `VolumeConfiguration` + `BucketConfiguration`
      (snake_case, schema `storage`).
- [x] Migration `InitStorage` via
      `dotnet ef migrations add InitStorage --context StorageDbContext`.
- [x] `IStorageQuotaReader` interface +
      `EfStorageQuotaReader` implementation.
- [x] Unit tests: `VolumeConfigurationShould` (4 cases) +
      `BucketConfigurationShould` (3 cases).

## 4.5.d.2 — Storage module (Application + Api)

- [x] Application: `CreateVolumeCommand` +
      `UpdateVolumeSizeCommand` + `DeleteVolumeCommand` +
      `CreateBucketCommand` + handlers (with quota enforcer
      integration).
- [x] `UpdateSizeAsync` quota delta fix — the resize handler
      calls
      `CheckAndReserveAsync(scope, "storage.volumes.gb",
      sizeDelta)` (positive / negative).
- [x] `StorageController` with the 10 endpoints (5 per
      resource).
- [x] FluentValidation on the create / update commands.
- [x] Permission strings `storage.create / read / update /
      delete` + `storage.buckets.*` added to
      `Plexor.Shared.Kernel.Storage.StoragePermissions`.
- [x] Unit tests: `CreateVolumeCommandHandlerShould` (5
      cases) + `UpdateVolumeSizeCommandHandlerShould` (4
      cases).

## 4.5.d.3 — Network module (Domain + Infrastructure)

- [x] `Plexor.Modules.Network` project structure.
- [x] Domain entities: `FloatingIp`, `LoadBalancer`.
- [x] Value objects: `FloatingIpId` / `LoadBalancerId`.
- [x] EF DbContext (`NetworkDbContext`) +
      `FloatingIpConfiguration` + `LoadBalancerConfiguration`
      (snake_case, schema `network`).
- [x] Migration `InitNetwork` via
      `dotnet ef migrations add InitNetwork --context NetworkDbContext`.
- [x] `INetworkQuotaReader` interface +
      `EfNetworkQuotaReader` implementation.

## 4.5.d.4 — Network module (Application + Api)

- [x] Application: `CreateFloatingIpCommand` +
      `ReassignFloatingIpCommand` + `DeleteFloatingIpCommand`
      + `CreateLoadBalancerCommand` +
      `DeleteLoadBalancerCommand` + handlers.
- [x] `NetworkController` with the 10 endpoints.
- [x] FluentValidation on the create commands.
- [x] Permission strings `network.create / read / update /
      delete` added to
      `Plexor.Shared.Kernel.Network.NetworkPermissions`.

## 4.5.d.5 — Quota wiring (create + update)

- [x] `CreateVolumeCommandHandler` calls
      `CheckAndReserveAsync(scope, "storage.volumes.count", 1)`
      AND
      `CheckAndReserveAsync(scope, "storage.volumes.gb",
      sizeGb)` inside the resource-create transaction.
- [x] `CreateFloatingIpCommandHandler` calls
      `CheckAndReserveAsync(scope, "network.floating_ips.count",
      1)`.
- [x] `CreateLoadBalancerCommandHandler` calls
      `CheckAndReserveAsync(scope,
      "network.load_balancers.count", 1)`.
- [x] `UpdateVolumeSizeCommandHandler` calls
      `CheckAndReserveAsync(scope, "storage.volumes.gb",
      sizeDelta)` (the UpdateSize quota delta fix).

## 4.5.d.6 — Admin UI

- [x] `AdminStoragePage` (`/admin/storage`) under
      `web/apps/console/src/features/admin/storage/`.
- [x] `AdminNetworkPage` (`/admin/network`) under
      `web/apps/console/src/features/admin/network/`.
- [x] i18n keys for every label.

## 4.5.d.7 — Composition root + Migrator

- [x] `Plexor.Host/Program.cs` — `AddStorageModule()` +
      `AddNetworkModule()` registered.
- [x] `Plexor.Migrator/Program.cs` — both installers called.
