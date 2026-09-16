# Plexor deployment

Production-style deployment of Plexor components. Lives outside the
`src/` tree on purpose — deploy shapes (systemd units, smoke scripts,
operational playbooks) are not part of the .NET solution and don't
need to be built.

## Layout

```
deploy/
├── README.md                              # this file
├── docker/
│   └── compose.yaml                       # local dev stack (Postgres + Migrator + Host)
├── systemd/
│   └── plexor-nodeagent.service           # production systemd unit for NodeAgent
└── scripts/
    └── smoke-test-vm-create.sh            # libvirt smoke test, run by smoke-test.yml
```

## Local development

```bash
cd deploy/docker
docker compose up --build
# wait ~30s for migrator to finish, then host is up
curl http://localhost:47101/health
```

See [`deploy/docker/compose.yaml`](docker/compose.yaml) for the service
layout, port map, and dev credentials.

## Production: NodeAgent on a test VM

### One-time VM setup

```bash
# 1. Install .NET 10 runtime (the deploy ships a framework-dependent build).
#    On Ubuntu 24.04:
wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
sudo /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
sudo ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# 2. Install libvirt + virt-install for the smoke test.
sudo apt-get install -y qemu-kvm libvirt-clients libvirt-daemon-system virtinst
sudo virsh net-start default

# 3. Create the plexor system user (the deploy workflow also creates it
#    idempotently, but doing it once up front avoids the first-deploy race).
sudo useradd --system --home /home/plexor --shell /usr/sbin/nologin plexor

# 4. Allow the deploy user to manage the plexor-nodeagent systemd unit
#    without a password prompt. Drop-in:
sudo tee /etc/sudoers.d/plexor-deploy >/dev/null <<'EOF'
deploy ALL=(ALL) NOPASSWD: /usr/bin/systemctl daemon-reload, \
    /usr/bin/systemctl restart plexor-nodeagent, \
    /usr/bin/systemctl enable plexor-nodeagent, \
    /usr/bin/systemctl is-active plexor-nodeagent, \
    /usr/bin/systemctl show plexor-nodeagent, \
    /usr/sbin/virsh, \
    /usr/bin/install, \
    /usr/bin/rsync, \
    /usr/bin/mkdir, \
    /usr/bin/rm, \
    /usr/bin/useradd, \
    /usr/bin/chown, \
    /usr/bin/cp, \
    /usr/bin/chmod
EOF
sudo chmod 0440 /etc/sudoers.d/plexor-deploy

# 5. Add the deploy user's public key to ~deploy/.ssh/authorized_keys.
#    (Out of scope here — depends on your access policy.)
```

### GitHub-side setup

Configure three repo secrets under **Settings → Secrets and variables →
Actions**:

| Secret | Example | What |
|--------|---------|------|
| `TEST_VM_HOST` | `test-vm.example.com` | SSH hostname |
| `TEST_VM_USER` | `deploy` | SSH user with the sudoers drop-in above |
| `TEST_VM_SSH_KEY` | `-----BEGIN OPENSSH PRIVATE KEY-----...` | PEM private key for that user |

### Deploy

Push to `main`. The `deploy-nodeagent.yml` workflow runs automatically:

1. Builds `dotnet publish src/agents/Plexor.NodeAgent` on a clean runner.
2. Uploads the publish output + the systemd unit as one artifact.
3. SCPs the artifact to `/tmp/plexor-nodeagent-staging/` on the VM.
4. SSHes in to install: rsyncs to `/opt/plexor-nodeagent`, installs the
   systemd unit if changed, restarts the service.
5. Verifies via `systemctl is-active plexor-nodeagent`.

The matching `smoke-test.yml` then runs (after a short workflow_run
delay):

1. SSHes to the VM.
2. Runs `deploy/scripts/smoke-test-vm-create.sh` — creates a tiny
   Alpine VM via libvirt, then tears it down.
3. Pass / fail surfaces in the GH Actions UI.

### Manual smoke test

```bash
# On the VM, as the deploy user:
sudo ./deploy/scripts/smoke-test-vm-create.sh
```

The script traps EXIT and cleans up — re-running is safe.

### Override per-VM settings

The shipped systemd unit sets a default `Plexor__ControlPlaneUrl` of
`http://plexor-host.local:5000/`. Override per VM with a drop-in:

```bash
sudo systemctl edit plexor-nodeagent
# opens an editor; add:
#   [Service]
#   Environment=Plexor__ControlPlaneUrl=http://my-actual-host:5000/
sudo systemctl restart plexor-nodeagent
```

`systemctl edit` creates `/etc/systemd/system/plexor-nodeagent.service.d/override.conf`
which is layered on top of the shipped unit. The shipped unit is
read-only — your override survives future deploys unchanged.

## Why no /health endpoint

Plexor.NodeAgent is a Worker Service (not an HTTP service). Health
checks go through systemd:

```bash
sudo systemctl is-active plexor-nodeagent   # 'active' or 'inactive'
sudo journalctl -u plexor-nodeagent -f      # tail logs
sudo systemctl show plexor-nodeagent --property=ActiveState,SubState,MainPID
```

A future control-plane integration can expose NodeAgent health via the
Host's `/api/v1/nodes/{id}` endpoint — out of scope for this MVP.

## When something goes wrong

| Symptom | Where to look |
|---------|---------------|
| Deploy workflow fails at the SCP step | GitHub IPs may be blocked by the VM firewall; see [.github/workflows/README.md](../.github/workflows/README.md#ssh-connection-refused--timeout) |
| `systemctl restart` fails with permission denied | The sudoers drop-in is missing or wrong; verify with `sudo -l -U deploy` |
| NodeAgent starts but immediately exits | `journalctl -u plexor-nodeagent -n 200` — usually an mTLS enrollment failure or unreachable control plane |
| Smoke test fails with "default network not active" | `sudo virsh net-start default`; persist with `sudo virsh net-autostart default` |
| Smoke test fails with "ISO download failed" | The VM has no egress to dl-cdn.alpinelinux.org; check firewall rules |

## Adding new deploy targets

When the time comes to deploy Plexor.Host itself (not just the agent),
the same pattern applies:

1. Add a new `deploy/systemd/plexor-host.service` (or use the existing
   `src/host/Plexor.Host/Dockerfile` container).
2. Add a `deploy-host.yml` workflow alongside `deploy-nodeagent.yml`.
3. Add a corresponding smoke test if there's a meaningful host health
   probe (e.g. a real `/health` endpoint from the Host's Web SDK).

The shape is "one workflow per service", not "one mega-workflow" — each
service has its own deploy cadence, its own secrets, its own smoke test.
