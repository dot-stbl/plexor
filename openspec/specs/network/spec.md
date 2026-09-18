# Capability: network

## Purpose

Plexor's network module owns two resource types:

1. **Floating IPs** — public IP addresses assignable to
   Plexor nodes.
2. **Load balancers** — TCP/HTTP load balancers fronting
   one or more workloads.

The module is the consumer of two quota catalog keys
shipped by Phase 4.5 (`network.floating_ips.count`,
`network.load_balancers.count`) and exposes REST endpoints
for floating IP + load balancer CRUD. Quota reservation
happens inside the resource-create transaction so a failed
INSERT rolls back the reservation.

This capability is owned by `Plexor.Modules.Network`.
Schema name in SQL and migrations: `network`. C# concept
names: `FloatingIp`, `LoadBalancer`, `FloatingIpId`,
`LoadBalancerId`.

## Requirements

### Requirement: FloatingIp entity

The system SHALL expose a `FloatingIp` entity
(`Plexor.Modules.Network.Domain.Entities.FloatingIp`,
schema `network.floating_ips`) for public IP addresses
assigned to Plexor nodes.

`FloatingIp` SHALL carry:

- `Id : FloatingIpId` — strongly-typed `fip_<UUIDv7>` PK.
- `OrgId : Guid` — tenant scope.
- `Address : string` — IPv4 literal in `A.B.C.D` form
  (FluentValidation regex match).
- `NodeId : NodeId?` — assigned node id; null until
  assigned.
- `Status : FloatingIpStatus` — `Available` | `Assigning`
  | `Assigned` | `Releasing`.
- `CreatedAt : DateTimeOffset`, `UpdatedAt :
  DateTimeOffset`.

`UNIQUE (OrgId, Address)` enforces uniqueness per org.

### Requirement: LoadBalancer entity

The system SHALL expose a `LoadBalancer` entity
(`Plexor.Modules.Network.Domain.Entities.LoadBalancer`,
schema `network.load_balancers`) for TCP/HTTP load
balancers.

`LoadBalancer` SHALL carry:

- `Id : LoadBalancerId` — strongly-typed `lb_<UUIDv7>` PK.
- `OrgId : Guid` — tenant scope.
- `Name : string` — operator label, unique per org
  (`UNIQUE (OrgId, Name)`).
- `Algorithm : LoadBalancerAlgorithm` — `round-robin` |
  `least-conn` (v0.1).
- `Port : int` — listening port (1–65535).
- `TargetWorkloadIds : IReadOnlyList<Guid>` — list of
  workload ids (UUID v7) the LB fronts. Persisted as
  `jsonb` via EF Core `OwnedTypes` (typed primitive
  collection) so the read path skips `JsonDocument.Parse`
  (`ef-owned-types.md`).
- `Status : LoadBalancerStatus` — `Provisioning` |
  `Active` | `Deleting`.
- `CreatedAt : DateTimeOffset`, `UpdatedAt :
  DateTimeOffset`.

### Requirement: CreateFloatingIpAsync with quota reservation

The system SHALL expose
`CreateFloatingIpCommandHandler`
(`Plexor.Modules.Network.Application.FloatingIps.CreateFloatingIpCommandHandler`)
that:

1. Validates the request (FluentValidation: `address` is
   a valid IPv4 literal; `nodeId` if present references a
   real node in the org).
2. Calls
   `IQuotaEnforcer.CheckAndReserveAsync(scope, "network.floating_ips.count", 1)`.
3. Inserts the `FloatingIp` row.
4. Commits the transaction.

A reservation failure raises
`QuotaExceededException` → HTTP 429 ProblemDetails.

### Requirement: ReassignFloatingIpAsync

The system SHALL expose
`ReassignFloatingIpCommandHandler` that:

1. Reads the `FloatingIp` row.
2. Validates the new `NodeId` is in the same org.
3. UPDATEs `NodeId` (no quota impact — the IP is already
   counted).
4. Commits the transaction.

### Requirement: CreateLoadBalancerAsync with quota reservation

The system SHALL expose
`CreateLoadBalancerCommandHandler` that:

1. Validates the request (FluentValidation: `name`
   non-empty ≤ 64 chars; `port` 1–65535; `algorithm` is
   one of `round-robin` / `least-conn`; every
   `targetWorkloadId` references a workload in the same
   org).
2. Calls
   `IQuotaEnforcer.CheckAndReserveAsync(scope, "network.load_balancers.count", 1)`.
3. Inserts the `LoadBalancer` row.
4. Commits the transaction.

### Requirement: INetworkQuotaReader

The system SHALL expose
`INetworkQuotaReader`
(`Plexor.Shared.Kernel.Network.INetworkQuotaReader`) for
read-side consumers:

```csharp
public interface INetworkQuotaReader
{
    Task<long> CountFloatingIpsAsync(QuotaScope scope, CancellationToken ct);
    Task<long> CountLoadBalancersAsync(QuotaScope scope, CancellationToken ct);
}
```

`EfNetworkQuotaReader`
(`Plexor.Modules.Network.Infrastructure`) implements
the interface by querying `network.floating_ips` /
`network.load_balancers` directly with the same tenant
filter as every other Plexor read.

### Requirement: REST endpoints for floating IPs

The system SHALL expose the following endpoints, all
behind `Plexor.Shared.Authorization.RequirePermissionAttribute`:

- `POST /api/v1/network/floating-ips` (`network.create`)
  — create.
- `GET /api/v1/network/floating-ips?cursor=&limit=`
  (`network.read`) — list.
- `GET /api/v1/network/floating-ips/{id}` (`network.read`)
  — detail.
- `PATCH /api/v1/network/floating-ips/{id}`
  (`network.update`) — reassign to a node.
- `DELETE /api/v1/network/floating-ips/{id}`
  (`network.delete`) — release.

### Requirement: REST endpoints for load balancers

The system SHALL expose the same five endpoints for load
balancers:

- `POST /api/v1/network/load-balancers` (`network.create`).
- `GET /api/v1/network/load-balancers` (`network.read`).
- `GET /api/v1/network/load-balancers/{id}`
  (`network.read`).
- `PATCH /api/v1/network/load-balancers/{id}`
  (`network.update`).
- `DELETE /api/v1/network/load-balancers/{id}`
  (`network.delete`).

### Requirement: Permission strings

The system SHALL expose the following permission strings
in the role permission catalog:

- `network.create`, `network.read`, `network.update`,
  `network.delete`.

The built-in `admin` role carries the `*` wildcard and
covers all four. The built-in `viewer` role carries
`network.read` for read-only dashboards.

### Requirement: Standard 401 / 403 / 404 contract

The system SHALL apply the standard Plexor contract on
every network endpoint:

- **No bearer / invalid bearer** → HTTP 401 with
  `identity.token.*` code.
- **Valid bearer, missing permission** → HTTP 403 with
  `code = "identity.permission.denied"`.
- **Valid bearer, valid permission, wrong tenant** → HTTP
  404 (never 403 — never leak existence).

### Requirement: Migration order

`network.*` migrations SHALL be applied after `realm`,
`sigil`, `atlas`, `quotas`, and `storage` (FK in spirit —
load balancer targets reference storage volumes for
backing data). The `Plexor.Migrator` orders schemas by FK
dependency:
`realm` → `sigil` → `atlas` → `quotas` → `storage` →
`network`. The `InitNetwork` migration is the only
network migration in v0.1.

## Key Entities

### `FloatingIp`

`Plexor.Modules.Network.Domain.Entities.FloatingIp`
(schema `network.floating_ips`). Fields as listed above.

### `LoadBalancer`

`Plexor.Modules.Network.Domain.Entities.LoadBalancer`
(schema `network.load_balancers`). Fields as listed
above.

### Value Objects and Exceptions

- `FloatingIpId` (record struct) — typed wrapper, prefix
  `fip_<UUIDv7>`.
- `LoadBalancerId` (record struct) — typed wrapper,
  prefix `lb_<UUIDv7>`.
- `FloatingIpStatus` (enum) — `Available` | `Assigning` |
  `Assigned` | `Releasing`.
- `LoadBalancerStatus` (enum) — `Provisioning` | `Active`
  | `Deleting`.
- `LoadBalancerAlgorithm` (enum) — `round-robin` |
  `least-conn`.
- `NetworkPermissions` (constants) — four permission
  strings listed above.
