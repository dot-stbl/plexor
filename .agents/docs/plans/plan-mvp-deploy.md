# Plan: Plexor MVP single-server self-hosted deploy

## Goal

End-to-end operational runbook for the Plexor MVP scenario
(per `.agents/docs/scope.md` §"MVP-минимум"): **one server, one
admin, four integrated open-source services** — Plexor control plane
+ Keycloak (auth) + Forgejo (git) + k3s single-node (workload
runtime). Operator (the admin) deploys the stack with `kubectl`
+ a handful of `helm install` / `kustomize` commands, then opens a
browser and the whole thing works.

## Why this plan is a separate worktree

`plan/mvp-deploy` worktree is downstream of every code plan
(identity, clusters, k8s, runtime providers). It's an **operational
artifact** — a deploy runbook, not code. Keeping it isolated from
the implementation branches lets the operator (or future CI)
follow the runbook without merge conflicts against the in-flight
implementation work. Implementation lands in the relevant code
plans; this worktree holds the end-to-end "make it work"
instructions.

## Architectural context (from .agents/docs)

- **Plexor model** (`architecture.md`, `scope.md`): self-hosted
  IaaS-style control plane. Single binary (`Plexor.Host`); single
  server MVP. Multi-tenant features (Tenant/Project/Team) ship
  in Phase 2+ — for MVP, single-tenant is the explicit user
  choice.
- **K8s runtime** (`architecture/runtimes-and-bindings.md`):
  Plexor treats k8s as a *delegated* runtime — Plexor applies a
  manifest to a k8s API endpoint rather than managing the cluster.
  The user'ский k3s cluster is the workload runtime; Plexor is a
  bounded tenant on it.
- **App providers** (`providers.md`): each app (postgres, redis,
  nginx, …) is a package the operator installs on top of the
  stack. Plexor doesn't ship its own Postgres — it installs
  CloudNativePG into the user's k3s and treats that as the DB.
- **Auth path** (`scope.md` §3, `architecture/identity.md`): the
  operator's Keycloak is the IDP for human users. Plexor accepts
  Keycloak-issued JWTs as a *resource server* (not as a primary
  IDP). The local admin seeded by `IdentityBootstrapper` is the
  break-glass path for when Keycloak is unreachable.

## Stack overview

| Service | Purpose | Where it runs | How Plexor talks to it |
|---|---|---|---|
| **Postgres** | Persistent store for Plexor (sigil/realm/k8s schemas) + Keycloak DB + Forgejo DB | k3s pod (`cnpg` operator) | EF Core / Npgsql |
| **Keycloak** | Human-user auth (OIDC) for the Plexor dashboard | k3s pod (`keycloak` chart) | Resource-server JWT validation; Sigil `IdentityBootstrapper` no longer runs |
| **Forgejo** | Git hosting for workload manifests + app-provider sources | k3s pod (`forgejo` chart) | Webhook → Plexor on push; future: app-provider install via OCI image |
| **k3s** | Workload runtime + Plexor deployment target | Single-node install on the host | `K3sWorkloadRuntime` (per `plan/runtime-providers`) |
| **Plexor.Host** | Control plane | k3s deployment (Helm chart) | n/a — top of the stack |
| **CloudNativePG** | Postgres operator | k3s pod | Provisions a Postgres cluster for Plexor/Keycloak/Forgejo |

All services run **on the same single host**, sharing the same
k3s cluster. Network: all services talk via in-cluster DNS names
(postgres.plexor.svc.cluster.local etc.).

## Prerequisites

**Hardware** (per `scope.md`):
- 1 × bare-metal or VM with **Ubuntu 24.04 LTS** (or
  Fedora 40+ / RHEL 9 if Podman is preferred over Docker)
- **8 vCPU / 32 GB RAM / 200 GB SSD** — minimum for MVP workloads
  (Plexor control plane + Keycloak + Forgejo + Postgres + a
  couple of demo workloads)
- **Public IPv4 + DNS A record** for the server (so the dashboard
  is reachable from outside — Keycloak OIDC redirect requires HTTPS)
- **Open ports 80/443** (HTTP/HTTPS) + **22** (operator SSH)

**Software** (operator runs these on the host before the Plexor
runbook):
```bash
# k3s — single-node control plane + worker in one
curl -sfL https://get.k3s.io | INSTALL_K3S_EXEC="--disable=traefik" sh -
# (we install our own ingress; the bundled traefik stays off)

# helm — for CloudNativePG, Keycloak, Forgejo charts
curl -fsSL https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash

# kubectl already shipped with k3s
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml
```

**DNS / TLS** (the operator's own domain):
- `plexor.example.com` — Plexor dashboard + REST API
- `keycloak.plexor.example.com` — Keycloak (OIDC issuer)
- `git.plexor.example.com` — Forgejo
- A wildcard cert (`*.plexor.example.com`) — operator procures
  via `certbot` + their own CA, or via Let's Encrypt if the domain
  is public

## Step-by-step deploy runbook

### 1. k3s — single-node cluster

```bash
# k3s single-node, with our own ingress instead of the bundled traefik
curl -sfL https://get.k3s.io | \
  INSTALL_K3S_EXEC="--disable=traefik" \
  sh -
mkdir -p ~/.kube
sudo cp /etc/rancher/k3s/k3s.yaml ~/.kube/config
sudo chown $(id -u):$(id -g) ~/.kube/config
kubectl get nodes  # expect: one node, role control-plane, Ready
```

### 2. CloudNativePG — Postgres operator + 3 clusters

```bash
helm repo add cnpg https://cloudnative-pg.github.io/charts
helm upgrade --install cnpg cnpg/cloudnative-pg \
  --namespace cnpg-system --create-namespace

# One Postgres cluster per service so we can scale / back up
# independently.
for ns in plexor keycloak forgejo; do
  kubectl create namespace $ns --dry-run=client -o yaml | kubectl apply -f -
  cat <<EOF | kubectl apply -f -
apiVersion: postgresql.cnpg.io/v1
kind: Cluster
metadata:
  name: postgres
  namespace: $ns
spec:
  instances: 1
  storage:
    size: 8Gi
  postgresql:
    parameters:
      max_connections: "100"
EOF
done
```

### 3. Keycloak — IDP for human users

```bash
helm repo add bitnami https://charts.bitnami.com/bitnami
helm upgrade --install keycloak bitnami/keycloak \
  --namespace keycloak \
  --set auth.adminUser=admin \
  --set auth.adminPassword="$KEYCLOAK_ADMIN_PASSWORD" \
  --set production=false \
  --set httpRoute=true \
  --set service.type=ClusterIP
# Wait for the operator to come up (typically 1-2 minutes).
kubectl wait --for=condition=Ready pod -l app.kubernetes.io/name=keycloak -n keycloak --timeout=5m
```

After install, the operator creates the **plexor-realm** via the
admin console: one realm, one client (`plexor-dashboard`,
public, OIDC), one client (`plexor-api`, confidential, OIDC
service-accounts). The Plexor server-side reads the realm's
issuer URL + client secret from the `Plexor.Keycloak` secret
(sealed in step 5).

### 4. Forgejo — Git hosting

```bash
helm repo add forgejo https://charts.forgejo.org
kubectl create secret generic forgejo-admin \
  -n forgejo --from-literal=username=admin \
  --from-literal=password="$FORGEJO_ADMIN_PASSWORD"
helm upgrade --install forgejo forgejo/forgejo \
  --namespace forgejo \
  --set persistence.size=20Gi
```

### 5. Plexor — control plane

**Build & push** the image (operator runs on their build host):
```bash
docker build -t plexor.local/host:0.1.0 -f src/host/Plexor.Host/Dockerfile .
docker push plexor.local/host:0.1.0
```
(On the deploy host — `plexor.local/host` resolves to the local
containerd / k3s image registry. For a fully self-contained single
node, `ctr -n k8s.io images import` works too.)

**Deploy** the Plexor chart:
```bash
kubectl create namespace plexor --dry-run=client -o yaml | kubectl apply -f -
kubectl create secret generic plexor-keycloak \
  -n plexor \
  --from-literal=issuer=https://keycloak.plexor.example.com/realms/plexor \
  --from-literal=client-id=plexor-dashboard \
  --from-literal=client-secret="$KEYCLOAK_PLEXOR_CLIENT_SECRET"
kubectl apply -f deploy/k8s/plexor.yaml
```

The `Plexor.Host` chart wires:
- `DATABASE_URL` from the `plexor` namespace Postgres service
- `Keycloak__Authority` from the `plexor-keycloak` secret
- `Keycloak__ClientId` / `Keycloak__ClientSecret` from the same
  secret
- The K8s in-cluster Service Account token is mounted so Plexor
  can apply manifests to its own namespace (delegated runtime)

### 6. Ingress (operator chooses between Traefik, ingress-nginx, or
their existing edge)

```bash
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx --create-namespace
# Apply our wildcard cert (let's encrypt + cert-manager is the
# default; k8s-ingress-nginx has a basic annotation-based fallback).
kubectl apply -f deploy/k8s/ingress.yaml
```

### 7. Smoke test (operator-side)

Open <https://plexor.example.com>:
1. **Login** redirects to Keycloak. Admin user authenticates.
2. **Dashboard** shows "0 clusters / 0 nodes / 0 workloads"
   (the local k3s node is registered automatically via the K8s
   delegated runtime — Plexor sees itself as the first cluster).
3. **Deploy a demo workload** via the dashboard: `nginx:latest`
   on port 80. Plexor applies the manifest to the k3s cluster.
4. **`kubectl get pods -n plexor`** shows the nginx pod running.
5. **Logout** — dashboard shows the login button again.

## Operational concerns

- **Backup**: a daily `pg_dumpall` of the three Postgres clusters
  to S3 / local NAS — Plexor's `atlas` audit log + Keycloak's
  userdb + Forgejo's repos are all there. **Restore drill once
  per quarter.**
- **Update flow**: `helm upgrade <release> <chart>` per service.
  Plexor itself: `kubectl set image deployment/plexor-host
  plexor.local/host=<new-tag>` then `kubectl rollout status
  deployment/plexor-host`. k3s auto-updates via
  `systemctl restart k3s`.
- **TLS rotation**: cert-manager + Let's Encrypt, default 60-day
  certs. **Test renewal at 30 days** to catch rate-limit / DNS
  issues before the 60-day cliff.
- **Monitoring**: lightweight — Prometheus + Grafana via the
  `kube-prometheus-stack` chart; Plexor exposes metrics on
  `:9090/metrics` (added in Phase 7+). Keycloak + Forgejo each
  have their own health endpoints.

## Acceptance

- `curl -k https://plexor.example.com/healthz` returns 200.
- Browser open to the dashboard, login via Keycloak succeeds,
  dashboard shows 0 resources. Deploy a sample workload,
  dashboard shows it as Running.
- `kubectl get all -n plexor -l app.kubernetes.io/part-of=plexor`
  shows the controller + a sample workload.
- Disaster recovery drill (rebuild a fresh k3s node, restore
  pg_dump, re-apply manifests) completes in under one hour.

## Out of scope (Phase 5+)

- **HA k3s** (3 control-plane + etcd quorum) — current scope is
  single-node; HA is Phase 6+ per the parity matrix.
- **Multi-region Postgres** — for now each service has its own
  single-instance Postgres; cross-region replication is
  CloudNativePG's `ReplicaCluster` feature, Phase 6+.
- **Backups to S3** — currently local-only; S3 backing is Phase
  6+ when the Object Storage abstraction lands.
- **Disaster recovery automation** — manual runbook works in
  Phase 5; rqlite or Velero automation is Phase 7+.
- **Phase-7+ observability stack** (Prometheus / Grafana / Loki)
  replaces the smoke-test curl with proper metrics.

## Related plans

- `plan/clusters` — Cluster/Node aggregate + REST endpoints
- `plan/k8s` — K3s / Talos app provider (how workloads get
  scheduled onto the runtime)
- `plan/runtime-providers` — IWorkloadRuntime + Docker Compose /
  k3s / Podman Quadlet implementations (used by Plexor to deploy
  via the dashboard)

This runbook consumes everything in those plans; the MVP deploy
becomes possible only after the code work lands.
