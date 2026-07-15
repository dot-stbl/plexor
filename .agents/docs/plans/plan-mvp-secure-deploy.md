# Plan: Plexor MVP secure self-hosted deploy

## Goal

Ship a Plexor MVP that runs end-to-end on OpenNebula-provisioned VMs:
control plane on one VM, one or more NodeAgents on other VMs, Docker
workloads managed through Plexor. All wire-level traffic
authenticated via mTLS, all entities identified by prefixed UUIDv7,
all ports in the PLexor private range (48000-48999).

This plan builds **on top of** `plan-clusters` (Cluster/Node aggregates
shipped in the prior commit `c010258` … `1b9e9e3`) — it does not redo
that work. It adds the wire security + workload runtime that turns
the cluster schema into something that actually runs containers.

## Scope

### In scope (this plan)

1. **Prefixed UUIDv7 identifiers** for all primary entities.
2. **mTLS wire security** between Plexor.Host and Plexor.NodeAgent
   (CA on filesystem, client cert per node, CN-based identity).
3. **Docker runtime provider** in NodeAgent — Plexor creates /
   starts / stops / deletes containers on the node.
4. **PLexor port range wiring** — every default port override
   so Plexor does not collide with K3s, OpenNebula, Vault, etc.
5. **Verification on OpenNebula** — manual smoke test of the
   full flow with the user's cluster.

### Out of scope (deferred)

- **Cert rotation** — the host issues a cert at join with TTL
  matching the CA lifetime (10y). Cert stays valid until the
  node is removed from the cluster (DELETE cascade revokes via
  serial in `forge.revoked_certs`). This matches kubeadm's
  posture for control-plane leaf certs: long-lived, manual
  rotation only. If compliance pressure arrives (PCI-DSS, SOC2)
  we add `CertRotationWorker` + atomic cert swap + revoked-certs
  table; the swap is additive — handlers depend only on
  `ICertificateAuthority`.
- **Vault / external CA** (Phase 2) — `ICertificateAuthority`
  interface is in place from the start so the swap is mechanical,
  but the only impl in this plan is `FileSystemCertificateAuthority`.
  but the only impl in this plan is `FileSystemCertificateAuthority`.
- **KVM / Libvirt runtime** — would let Plexor provision real VMs
  rather than containers; we already have stubs in
  `Plexor.NodeAgent/Providers/` for this. Deferred to plan-k8s /
  plan-runtime-providers.
- **Frontend end-to-end against real backend** — `/clusters` flow
  on the web is still mock-backed. Out of scope here.
- **Multi-cluster / federation** — single cluster only.

## Architecture decisions

Captured from the design discussion (2026-07-15 / 16) and locked
into this plan.

### AD-1. Identifier scheme

- Every primary entity uses a **strongly-typed prefixed UUIDv7**
  value object: `ClusterId` (`cluster_…`), `NodeId` (`node_…`),
  `TokenId` (`tok_…`), `WorkloadId` (`wl_…`).
- The **prefix** is hard-coded in the type — there is no way to
  instantiate a `ClusterId` whose string starts with `node_`. This
  makes routing-by-prefix (log filtering, type discovery) trivial
  and removes a class of "wrong ID in wrong field" bugs at compile
  time.
- `JoinTokenSecret` is **not** prefixed — it stays as raw 32-byte
  base64, treated as a credential, not an ID. It lives in the
  `forge.join_tokens.secret_hash` column (PBKDF2-hashed) and is
  only ever compared via constant-time hash compare.
- **Generator**: `UUIDNext` NuGet (`Uuid.NewNext()`). RFC 9562
  compliant UUIDv7 — 48-bit Unix-ms timestamp + 74 random bits.
  Source-generated, AOT-friendly.
- **Wire format**: ULID-style lowercase, no dashes.
  `cluster_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d` (5-char prefix + 26-char
  body = 31 chars). Lives in `varchar(64)` columns with headroom for
  future ID types.
- **Sortable**: yes — `ORDER BY id` is monotonic by creation time,
  so pagination by ID is equivalent to pagination by `created_at`
  and we can drop the secondary index for stable cursor pagination.

### AD-2. Wire security (mTLS)

- **Host↔NodeAgent traffic is mTLS** at the TLS handshake. No
  application-level bearer / JWT / shared secret for NodeAgent
  auth — the cert IS the credential. TLS handshake happens before
  any HTTP payload is read.
- **CA**: `FileSystemCertificateAuthority`. CA root key in
  `/var/lib/plexor/ca.key` (mode `0600`, owned by the `plexor`
  service account). CA root cert in `/var/lib/plexor/ca.crt`
  (readable by every node — they need it to verify the host).
- **Cert TTL**: matches the CA lifetime (10 years). NodeAgent
  receives its client cert at `POST /node-agent/join`. The cert is
  stored in `/var/lib/plexor/node.crt` + `/var/lib/plexor/node.key`
  (mode `0600`). NodeAgent's HTTPS client presents it on every
  request. Cert stays valid until the node leaves the cluster —
  revocation is by serial in `forge.revoked_certs`. This matches
  kubeadm's posture for control-plane leaf certs: long-lived,
  manual rotation only.
- **Cert subject**: `CN=node_<NodeId>` — the CN is the canonical
  Plexor NodeId. The host extracts the CN at the TLS handshake
  via ASP.NET Core's `IHttpContextAccessor` after
  `ClientCertificateMode = RequireCertificate` and uses it as the
  identity claim. No "this node claims to be X" lookup — the cert
  is the X.
- **Revocation**: in scope. `forge.revoked_certs (serial,
  revoked_at, reason)` table. DELETE on cluster / DELETE on node
  cascades to `INSERT INTO forge.revoked_certs`. `MtlsAuthMiddleware`
  rejects any client cert whose serial is in the revoked set.
  No CRL / OCSP — the in-DB lookup is sufficient for the small
  number of nodes in a self-hosted cluster (typically <50).
- **`ICertificateAuthority` interface**: defined from day one so
  the swap to `VaultCertificateAuthority` in Phase 2 is a single
  DI binding change.

### AD-3. PLexor port range

All Plexor-owned services listen on `48000-48999`. None uses a
default port.

| Port | Service |
|---|---|
| 48001 | `Plexor.Host` HTTP (browser-facing, plain HTTP; TLS terminates upstream at nginx / Caddy) |
| 48002 | `Plexor.Host` mTLS (NodeAgent-facing, mutual TLS) |
| 48003 | PostgreSQL (override of default 5432 to avoid collision with pre-installed Postgres on OpenNebula VM) |
| 48004 | `Plexor.NodeAgent` local debug API (optional — for `plx node exec` / metrics scrape on the node) |
| 48005 | `Plexor.Web` (vite dev server) |
| 48006 | `Plexor.Metrics` (Prometheus scrape endpoint, optional) |
| 48007 | `Plexor.Tracing` (OTel collector, optional) |
| 48010 | `Plexor.MCP` server (Phase 2 per `architecture/mcp.md`) |
| 48020 | `Plexor.Cluster` internal event bus (NATS / in-memory — Phase 2) |
| 48099 | Reserved |
| 49000-49999 | **Dynamic range** for `PLEXOR_PORT_BASE` overrides (second stack on the same host) |

Things this avoids:

- 80/443 (reverse-proxy territory)
- 5432 (Postgres default)
- 6443 / 10250 (K3s territory, plan-k8s)
- 2633 / 2474 (OpenNebula frontend + XML-RPC)
- 8200 / 8500 / 8600 (Vault / Consul — Phase 2)
- 2375 / 2376 (Docker daemon)
- 51820 (WireGuard — plan-networking)
- 8443 (HTTPS alt; we use 48002 instead)

### AD-4. Runtime — Docker

`DockerRuntimeProvider` is the only workload runtime in this plan.
KVM / LXC stubs already exist in `Plexor.NodeAgent/Providers/` but
do not need to ship for MVP — Docker is the path to "Plexor creates
a container that runs on the node" within this plan.

Docker SDK: `Docker.DotNet` (the canonical C# client). Talks to the
local daemon over `/var/run/docker.sock`. Daemon installed on the
node alongside Plexor.NodeAgent.

## Phases

### Phase A — Identifiers (foundation, 0.5 day)

Everything else depends on this. Stop and confirm before
proceeding to B.

**Create** under `src/shared/Plexor.Shared.Identifiers/`:

```
Plexor.Shared.Identifiers.csproj
IdentifierPrefixes.cs          # file static class with const strings
IdGenerator.cs                # UUIDNext Uuid.NewNext() wrapper
ClusterId.cs                  # readonly partial record struct
NodeId.cs                     # readonly partial record struct
TokenId.cs                    # readonly partial record struct
WorkloadId.cs                 # readonly partial record struct
JoinTokenSecret.cs            # readonly record struct (raw 32-byte base64, not prefixed)
IdParse.cs                    # static Parse(string) → TId by prefix dispatch
```

**Modify** `Directory.Packages.props` to pin `UUIDNext` version.

**Modify** `Plexor.Modules.Clusters.Domain/*.cs`:

- `Cluster.Id` : `Guid` → `ClusterId`
- `Node.Id` : `Guid` → `NodeId`
- `JoinToken.Id` : `Guid` → `TokenId`
- Handlers updated to use `ClusterId.New()`, `NodeId.New()`, etc.
  when minting new entities.

**Modify** `Plexor.Modules.Clusters.Infrastructure/Persistence/*Configuration.cs`:

- `HasConversion(id => id.Value, raw => ClusterId.Parse(raw))` on
  every `Id` property.
- `HasMaxLength(64)` on every Id column.

**Modify** `Plexor.Modules.Clusters.Api/Controllers/ClustersController.cs`:

- `[FromRoute] string id` → `[FromRoute] ClusterId id` (auto-parse
  via the binding model binder — falls back to 400 on malformed).

**Generate** EF Core migration:

```
20260716090000_Identifiers_Varchar64.cs

ALTER TABLE forge.clusters ALTER COLUMN id TYPE varchar(64);
ALTER TABLE forge.nodes    ALTER COLUMN id TYPE varchar(64);
ALTER TABLE forge.join_tokens ALTER COLUMN id TYPE varchar(64);
-- existing rows get prefixed:  id || '_' ||  prefix  (but DB is empty at this point)
```

**Modify** tests in `tests/unit/Plexor.Modules.Clusters.Unit/` and
`tests/unit/Plexor.Modules.Sigil.Unit/` to use the new typed IDs in
seeds + assertions.

**Verification**:

- `dotnet build plexor.slnx` 0/0
- `dotnet test Plexor.Modules.Clusters.Unit` 11/11
- `dotnet test Plexor.Modules.Sigil.Unit` 8/8
- `MapprlyApiSmokeTests` 1/1 (against real Postgres on 48003)
- `dotnet ef migrations add` succeeds and applies cleanly to a
  fresh `forge` schema

### Phase B — mTLS (1.5 days)

**Create** under `src/host/Plexor.Host/CertAuthority/`:

```
ICertificateAuthority.cs
FileSystemCertificateAuthority.cs   # MVP impl
X509Extensions.cs                    # RSA + X509 builders
CertAuthorityOptions.cs              # cert path, key path, default TTL
```

`ICertificateAuthority` surface:

```csharp
public interface ICertificateAuthority
{
    /// <summary>Issue a new client certificate signed by the Plexor CA.</summary>
    /// <param name="subject">X500DistinguishedName whose CN is the NodeId.</param>
    /// <param name="ttl">How long the cert is valid.</param>
    X509Certificate2 IssueClientCert(X500DistinguishedName subject, TimeSpan ttl);

    /// <summary>Validate a presented client certificate against the CA chain.</summary>
    bool VerifyClientCert(X509Certificate2 candidate);

    /// <summary>CA root cert (PEM bytes) — sent to nodes for chain verification.</summary>
    byte[] GetRootCertificatePem();
}
```

`FileSystemCertificateAuthority` behaviour:

- On construction, read `/var/lib/plexor/ca.crt` and
  `/var/lib/plexor/ca.key`. If absent, generate:
  - RSA 4096
  - Self-signed X509 root, 10-year validity
  - `Subject = "CN=Plexor Root CA, O=Plexor, C=US"`
  - Write to the configured paths with `0600` perms.
- Issue: create CSR with the requested subject, sign with the CA
  private key, return `X509Certificate2`.
- Verify: `X509Chain.Build(candidate)` with our CA root in
  `ChainPolicy.ExtraStore`. Reject if `Build()` returns false, if
  the chain length is anomalous, or if the cert's serial appears
  in `forge.revoked_certs`. Revocation lookup is a simple indexed
  SELECT on startup / per-request-cache; no CRL / OCSP needed at
  this scale (self-hosted cluster, typically < 50 nodes).

**Create** under `src/host/Plexor.Host/NodeAgent/`:

```
MtlsAuthMiddleware.cs          # extract X-Client-Cert-Subject → HttpContext.User claim
NodeAgentCertBootstrapper.cs   # on host startup, ensure CA exists + is loaded
```

`MtlsAuthMiddleware`:

- Runs after Kestrel has populated `HttpContext.Connection.ClientCertificate`.
- Reads `cert.Subject` (e.g. `CN=node_0190f4d6…`).
- Parses the CN into a `NodeId`.
- Looks up the cert serial in `forge.revoked_certs` —
  if found, returns 401 immediately.
- Calls `ICertificateAuthority.VerifyClientCert(cert)`.
- On success, sets `HttpContext.User` with a `ClaimsPrincipal`
  carrying the `NodeId` and a `node` role.
- On failure, returns 401 with `WWW-Authenticate: mTLS` header
  (lets the client know to retry with a cert).

**Create** `src/host/Plexor.Host/Persistence/RevokedCertsDbContext.cs`
(or extend an existing one) + migration `forge.revoked_certs`:

```sql
CREATE TABLE forge.revoked_certs (
    serial varchar(64) PRIMARY KEY,
    revoked_at timestamptz NOT NULL,
    revoked_by varchar(64) NOT NULL,    -- NodeId of the cluster delete caller
    reason varchar(256) NOT NULL DEFAULT ''
);
CREATE INDEX ix_revoked_certs_revoked_at ON forge.revoked_certs(revoked_at);
```

A small in-memory cache (5-second TTL) memoises the revoked-serial
set so the per-request SELECT does not bottleneck. The cache is
flushed explicitly on revoke-insert / on a periodic timer.

**Modify** `Plexor.Host/Program.cs`:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(48001);                              // browser HTTP
    options.ListenAnyIP(48002, lo =>
    {
        lo.UseHttps("/var/lib/plexor/host.pfx", certPassword,
            httpsOptions =>
            {
                httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            });
    });
});
```

**Modify** `Plexor.Host/appsettings.json`:

```json
{
  "Kestrel": {
    "Endpoints": { /* configured in code, not in appsettings */ }
  },
  "CertAuthority": {
    "CertPath": "/var/lib/plexor/ca.crt",
    "KeyPath":  "/var/lib/plexor/ca.key",
    "DefaultTtl": "1.00:00:00"
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=48003;Database=plexor;Username=plexor;Password=plexor"
  }
}
```

**Modify** `Plexor.Modules.Clusters.Api/Controllers/NodeAgentController.cs`:

- Replace the JoinToken-secret-in-header auth with
  `[Authorize(Policy = "mTLS-NodeAgent")]`.
- The policy is satisfied by `MtlsAuthMiddleware` having populated
  the `NodeId` claim.
- `NodeJoinCommand` now includes the cert's CN — the host trusts
  the cert for the CN, not for any user-supplied nodeId.
- `JoinToken` issuance flow:
  1. Operator calls `POST /api/v1/compute/clusters/{clusterId}/tokens`.
  2. Host creates `forge.join_tokens` row with `secret_hash`,
     `expires_at = now + 7d`.
  3. Host returns `secret` (plaintext) **once** in the response —
     the operator passes this to the node.
  4. NodeAgent calls `POST /node-agent/join` with the secret.
     Host validates secret, then issues a client cert with
     `CN=node_<newly-assigned NodeId>` and returns the cert +
     key (PEM bundle) to the node.
  5. NodeAgent writes cert + key to `/var/lib/plexor/node.{crt,key}`
     and uses them on every subsequent request.
  6. After the cert response, the JoinToken row is marked
     `status = "consumed"` — one-time use.

**Modify** `src/host/Plexor.NodeAgent/Composition/NodeAgentServiceCollectionExtensions.cs`:

- Configure `HttpClient` with custom `SocketsHttpHandler` that:
  - Presents the node client cert from `/var/lib/plexor/node.crt`.
  - Validates the host's server cert against
    `/var/lib/plexor/ca.crt` (downloaded from host at join).
- New `NodeAgentOptions` fields: `CertPath`, `KeyPath`, `CaPath`.

**Verification**:

- `dotnet build` 0/0
- Manual smoke (deferred to Phase F): host starts, accepts mTLS,
  NodeAgent joins, gets cert, uses it for subsequent heartbeats.
- WAF test (`MapprlyApiSmokeTests`) continues to work — it uses
  the non-mTLS port 48001.

### Phase D — Docker runtime + Workloads (1.5 days)

**Create** under `src/host/Plexor.NodeAgent/Providers/`:

```
DockerRuntimeProvider.cs        # IWorkloadRuntime impl using Docker.DotNet
DockerRuntimeOptions.cs         # socket path (default /var/run/docker.sock)
```

`DockerRuntimeProvider` surface (matches
`Plexor.Shared.Workloads/IWorkloadProvider`):

- `Task<WorkloadHandle> CreateAsync(WorkloadSpec spec, CT)`
- `Task StopAsync(WorkloadHandle handle, CT)`
- `Task StartAsync(WorkloadHandle handle, CT)`
- `Task DeleteAsync(WorkloadHandle handle, CT)`
- `Task<WorkloadStatus> InspectAsync(WorkloadHandle handle, CT)`

Uses `Docker.DotNet`'s `DockerClient` configured against
`unix:///var/run/docker.sock`.

**Create** under `src/host/Plexor.NodeAgent/Executors/`:

```
WorkloadCreateExecutor.cs       # ICommandExecutor for "workload.create"
WorkloadActionExecutor.cs        # ICommandExecutor for start/stop/delete
```

`WorkloadCreateExecutor`:

- Receives a `CreateWorkloadCommand { workloadId, image, env, ports,
  mounts }`.
- Calls `DockerRuntimeProvider.CreateAsync(spec)`.
- Returns `WorkloadCreatedResult { containerId, workloadId }`.
- The host stores `containerId` on the workload row.

**Modify** `src/host/Plexor.NodeAgent/Composition/CommandDispatcher.cs`:

- Register `WorkloadCreateExecutor` and `WorkloadActionExecutor` in
  the `ICommandExecutor` registry.

**Create** under `src/modules/Plexor.Modules.Clusters.Api/Controllers/`:

```
WorkloadsController.cs
```

Endpoints:

- `POST /api/v1/compute/workloads` — create. Operator-facing.
  Takes `CreateWorkloadRequest { clusterId, nodeId?, name, image,
  env, ports, mounts }`. Picks a node if `nodeId` is not supplied
  (round-robin or least-loaded — MVP: round-robin in the host's
  node-registry).
- `GET /api/v1/compute/workloads` — list paged, filterable (same
  PageAsync pipeline as Cluster list).
- `GET /api/v1/compute/workloads/{id}` — single, includes
  status + containerId + nodeId.
- `POST /api/v1/compute/workloads/{id}/start` — re-runs the
  command to the assigned node's executor.
- `POST /api/v1/compute/workloads/{id}/stop` — same.
- `DELETE /api/v1/compute/workloads/{id}` — same.

All commands are dispatched via `CommandPollLoop` (which already
exists in NodeAgent).

**Create** under `src/modules/Plexor.Modules.Clusters.Application/Workloads/`:

```
WorkloadCommands.cs             # Create, Start, Stop, Delete commands + results
WorkloadQueries.cs              # List, GetById queries
WorkloadSpecs.cs                # filter + ordering specs (PageAsync pipeline)
```

**Create** under `src/modules/Plexor.Modules.Clusters.Infrastructure/Workloads/`:

```
WorkloadCommandHandlers.cs      # writes via DbContext (Cluster + Workloads aggregate)
WorkloadReadHandlers.cs         # reads via Repository<Workload> + Specification
WorkloadConfiguration.cs        # EF configuration for forge.workloads table
```

EF entity `Workload`:

```csharp
public sealed class Workload : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    public WorkloadId Id { get; init; }                  // "wl_..."
    public ClusterId ClusterId { get; init; }            // FK to Cluster
    public NodeId? AssignedNodeId { get; set; }         // null until dispatched
    public string Name { get; init; }
    public string Image { get; init; }
    public string EnvJson { get; init; }                 // jsonb
    public string PortsJson { get; init; }               // jsonb
    public string MountsJson { get; init; }              // jsonb
    public string Status { get; set; }                   // pending|running|stopped|error
    public string? ContainerId { get; set; }             // docker container id
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

EF migration:

```
20260716120000_Workloads.cs

CREATE TABLE forge.workloads (
    id varchar(64) PRIMARY KEY,
    cluster_id varchar(64) NOT NULL REFERENCES forge.clusters(id) ON DELETE RESTRICT,
    assigned_node_id varchar(64) NULL REFERENCES forge.nodes(id) ON DELETE SET NULL,
    name varchar(256) NOT NULL,
    image varchar(512) NOT NULL,
    env jsonb NOT NULL,
    ports jsonb NOT NULL,
    mounts jsonb NOT NULL,
    status varchar(32) NOT NULL,
    container_id varchar(128) NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);
CREATE INDEX ix_forge_workloads_cluster_id ON forge.workloads(cluster_id);
CREATE INDEX ix_forge_workloads_assigned_node_id ON forge.workloads(assigned_node_id);
```

**Create** under `src/modules/Plexor.Modules.Clusters.Infrastructure/Mappers/`:

```
WorkloadMapper.cs               # IWorkloadMapper — singular, like ClusterMapper
```

**Verification**:

- `dotnet build` 0/0
- Unit tests for workload command handlers (Create, Start, Stop,
  Delete) using InMemory DbContext.
- `Plexor.Modules.Clusters.Unit` ≥ 15 tests passing.
- Manual smoke (deferred to Phase F): POST workload → NodeAgent
  receives command → container starts → status flows back via
  heartbeat.

### Phase F — Port wiring + OpenNebula verification (0.5 day)

**Modify**:

- `deploy/docker/compose.yaml` — `ports: "48003:5432"` for the
  Postgres service (was `5432:5432`). Postgres inside the container
  still listens on 5432; we just expose it externally on 48003.
- `web/apps/console/vite.config.ts` — `server.port = 48005`.
- `web/apps/console/src/shared/api/client.ts` (or equivalent) —
  `baseURL: "http://localhost:48001"`.
- `Plexor.NodeAgent/appsettings.json` — `Host:Url =
  "https://<host>:48002"`.

**Verification** (manual, on the user's OpenNebula cluster):

1. `podman compose -f deploy/docker/compose.yaml up -d` on the
   control-plane VM.
2. Apply migrations: `dotnet run --project src/host/Plexor.Migrator`.
3. Start `Plexor.Host` as a systemd service on the control-plane VM,
   listening on 48001 + 48002.
4. On a second OpenNebula VM:
   - `apt install docker.io`
   - Place `node.crt` + `node.key` from a manual join run
   - `dotnet run --project src/host/Plexor.NodeAgent`
5. From the host:
   - `curl -X POST /api/v1/compute/clusters -d '{"name":"prod"}'`
     → returns cluster id
   - `curl -X POST /api/v1/compute/clusters/{id}/tokens`
     → returns plaintext secret
   - On the node: `plx node join --host <host>:48002 --token <secret>`
     → returns the node client cert
   - `curl -X POST /api/v1/compute/workloads -d '{...}'`
     → returns workload id, status=pending
   - `curl -X GET /api/v1/compute/workloads/{id}`
     → status flips to running after NodeAgent picks it up
   - `docker ps` on the node shows the container
6. Tear down: stop Plexor.Host, verify NodeAgent's heartbeat
   fails after one retry (mTLS rejection because host is down).

## File map

(Combined across all phases. New files only.)

```
src/shared/Plexor.Shared.Identifiers/                        [Phase A, NEW project]
  Plexor.Shared.Identifiers.csproj
  IdentifierPrefixes.cs
  IdGenerator.cs
  ClusterId.cs
  NodeId.cs
  TokenId.cs
  WorkloadId.cs
  JoinTokenSecret.cs
  IdParse.cs

src/modules/Plexor.Modules.Clusters/
  Plexor.Modules.Clusters.Domain/Cluster.cs                  [MODIFY: Id type]
  Plexor.Modules.Clusters.Domain/Node.cs                     [MODIFY: Id type]
  Plexor.Modules.Clusters.Domain/JoinToken.cs                [MODIFY: Id type]
  Plexor.Modules.Clusters.Application/Clusters/*.cs          [MODIFY: use Id.New()]
  Plexor.Modules.Clusters.Application/Workloads/WorkloadCommands.cs   [Phase D, NEW]
  Plexor.Modules.Clusters.Application/Workloads/WorkloadQueries.cs    [Phase D, NEW]
  Plexor.Modules.Clusters.Application/Workloads/WorkloadSpecs.cs      [Phase D, NEW]
  Plexor.Modules.Clusters.Infrastructure/Persistence/*Configuration.cs  [MODIFY: converters]
  Plexor.Modules.Clusters.Infrastructure/Persistence/Migrations/*      [NEW migrations]
  Plexor.Modules.Clusters.Infrastructure/Persistence/Mappings/WorkloadConfiguration.cs  [Phase D, NEW]
  Plexor.Modules.Clusters.Infrastructure/Workloads/WorkloadCommandHandlers.cs  [Phase D, NEW]
  Plexor.Modules.Clusters.Infrastructure/Workloads/WorkloadReadHandlers.cs     [Phase D, NEW]
  Plexor.Modules.Clusters.Infrastructure/Mappers/WorkloadMapper.cs     [Phase D, NEW]
  Plexor.Modules.Clusters.Api/Controllers/ClustersController.cs       [MODIFY: route binding]
  Plexor.Modules.Clusters.Api/Controllers/NodeAgentController.cs      [MODIFY: cert-based auth]
  Plexor.Modules.Clusters.Api/Controllers/WorkloadsController.cs       [Phase D, NEW]

src/host/Plexor.Host/CertAuthority/                          [Phase B, NEW folder]
  ICertificateAuthority.cs
  FileSystemCertificateAuthority.cs
  X509Extensions.cs
  CertAuthorityOptions.cs

src/host/Plexor.Host/NodeAgent/                              [Phase B, NEW folder]
  MtlsAuthMiddleware.cs
  NodeAgentCertBootstrapper.cs

src/host/Plexor.NodeAgent/Providers/
  DockerRuntimeProvider.cs                                   [Phase D, NEW]
  DockerRuntimeOptions.cs                                    [Phase D, NEW]

src/host/Plexor.NodeAgent/Executors/
  WorkloadCreateExecutor.cs                                  [Phase D, NEW]
  WorkloadActionExecutor.cs                                   [Phase D, NEW]

src/host/Plexor.Host/Plexor.Host.csproj                      [MODIFY: UUIDNext, Docker.DotNet refs]
src/host/Plexor.NodeAgent/Plexor.NodeAgent.csproj            [MODIFY: UUIDNext, Docker.DotNet refs]
src/host/Plexor.Host/Program.cs                              [MODIFY: Kestrel dual endpoint]
src/host/Plexor.Host/appsettings.json                        [MODIFY: cert paths, port 48003]
src/host/Plexor.NodeAgent/appsettings.json                   [MODIFY: cert paths, host URL]
src/host/Plexor.NodeAgent/Composition/NodeAgentServiceCollectionExtensions.cs  [MODIFY: mTLS client]
src/host/Plexor.NodeAgent/Composition/CommandDispatcher.cs   [MODIFY: register workload executors]

Directory.Packages.props                                      [MODIFY: UUIDNext, Docker.DotNet versions]
deploy/docker/compose.yaml                                    [MODIFY: 48003:5432]
web/apps/console/vite.config.ts                              [MODIFY: server.port = 48005]
web/apps/console/src/shared/api/client.ts                    [MODIFY: baseURL = 48001]

tests/unit/Plexor.Modules.Clusters.Unit/
  Workloads/WorkloadCommandHandlerShould.cs                  [Phase D, NEW]
```

## Risks and known issues

### R-1. Sigil captive-dependency cycle (carried over)

`.planning/BACKEND-ISSUES.md` documents an 8-service captive-dep
cluster in `Plexor.Modules.Sigil.Infrastructure/Auth/`. It does not
break Production (scope validation off) but it blocks
`WebApplicationFactory<Program>`-based tests in Development. We
need to resolve it before this plan's Phase F verification can run
host-in-process tests. Resolution paths are documented in the
issue; recommended is `IServiceScopeFactory` injection in 4 Sigil
files. If we hit the captive cluster during F, we will fix it
inline (small touch, well-scoped).

### R-2. Plexor.Host startup ordering

The `FileSystemCertificateAuthority` must load (or create) the CA
**before** Kestrel starts accepting connections, otherwise the first
NodeAgent connection would race with cert bootstrap. Mitigation:
register `ICertificateAuthority` as a singleton resolved eagerly
via a `HostedService` that calls `EnsureExists()` synchronously
before `app.Run()`.

### R-3. Cert CN parse must reject malformed CNs

`MtlsAuthMiddleware` parses `cert.Subject` and extracts the CN. A
malicious cert signed by a different (compromised) CA with a
crafted CN could try to impersonate a NodeId. Mitigation: we
verify the cert chain against **our** CA before the CN parse; the
CN parse only happens after `VerifyClientCert` returns true.

### R-4. Docker daemon socket permissions

`/var/run/docker.sock` is typically `0660 root:docker`. The
Plexor NodeAgent user needs to be in the `docker` group, or we
need a dedicated `plexor-nodeagent` user in the `docker` group.
Mitigation: `deploy/docker/node-agent.service` ships a systemd
unit with the right group.

### R-5. JSONB column → C# `string` for env/ports/mounts

Workloads carry env / ports / mounts which are naturally
structured. We store them as JSONB in `forge.workloads` and
serialize/deserialize at the handler boundary. Acceptable for MVP.
A future task may introduce typed value objects (`EnvVar[]`,
`PortMapping[]`, `MountSpec[]`) with proper EF value converters.

### R-6. NodeAgent restart loses in-memory workload registry

`Plexor.NodeAgent/Composition/InMemoryWorkloadRegistry.cs` holds
the local view of "what containers I should be running". On
restart the NodeAgent rebuilds this from the host via the
`/node-agent/join` re-attestation path. Documented as "host is
source of truth" — NodeAgent is stateless across restarts.

## Verification matrix

| | Local | OpenNebula |
|---|---|---|
| Phase A | build + unit tests + smoke | n/a (no infra dependency) |
| Phase B | build + WAF test | manual join + cert verify |
| Phase D | build + unit tests | manual workload create → container running |

## What unblocks after this plan

- `plan-runtime-providers` (generalised IWorkloadRuntime + KVM/LXC impls).
- `plan-k8s` (managed K3s on Plexor-provisioned nodes — the
  workload create path generalises to "create K3s node pool").
- `plan-mvp-deploy` (end-to-end runbook + `plx init` + `plx node join`).
- `plan-networking` (WireGuard mesh + internal DNS — mTLS is the
  transport; WG is the network overlay).
- `plan-auth-providers` (dual IDP per tenant).

## References

- `.agents/docs/architecture.md` — top-level architecture
- `.agents/docs/architecture/networking.md` — WireGuard mesh (Phase 2)
- `.agents/docs/architecture/runtimes-and-bindings.md` — Runtime/Binding model
- `.agents/rules/architecture/persistence.md` — Repository+Spec rules
- `.agents/rules/coding/mapping.md` — Mapperly conventions
- `.agents/rules/coding/repository-spec-structure.md` — Spec layout
- `.agents/rules/coding/api-design.md` — controller conventions
- `.agents/rules/coding/code-shape.md` §7 (ToArrayAsync), §11 (ThrowIf ban)
- `.planning/BACKEND-ISSUES.md` — captive-dep cluster (R-1)
- `.agents/docs/plans/plan-clusters.md` — prior plan; this builds on it
- RFC 9562 — UUIDv7 specification
- `UUIDNext` — https://github.com/mareek/UUIDNext
- `Docker.DotNet` — https://github.com/dotnet/Docker.DotNet
- Kubernetes cert rotation model — https://kubernetes.io/docs/tasks/tls/certificate-rotation/
  (we mirror the kubeadm posture: CA never auto-rotates; leaf
  certs are long-lived and revoked by serial on node delete)