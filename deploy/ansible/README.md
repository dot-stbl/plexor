# Plexor NodeAgent — Ansible Deploy

Provisions a Plexor compute node on a fresh Debian 12 VM: qemu-kvm,
libvirt, .NET 10 runtime, the NodeAgent binary, mTLS triple, and a
systemd service that starts on boot.

## Prerequisites

1. **Target host** — a Debian 12 (bookworm) VM or bare-metal box with:
   - KVM enabled in BIOS (`/dev/kvm` exposed to the OS).
   - Network access to the Plexor.Host control plane
     (default port 48001 — HTTP — and 48002 — mTLS).
   - SSH access for the Ansible user (or run the playbook on the box
     itself via `ansible_connection: local`).

2. **Ansible controller** — any Linux/macOS host with:
   - Ansible 8+ (`ansible --version` → `core: 2.15.x` or newer).
   - `community.general` collection is NOT required for this playbook;
     only the built-in modules are used.

3. **Pre-shared secrets** — collect these before invoking the
   playbook; they're set as host vars in the inventory:

   | Secret | Source | Format |
   |--------|--------|--------|
   | `plexor_join_token` | `Plexor.Host` operator | opaque string |
   | `plexor_node_id` | host's `/api/v1/nodes/{id}` after first heartbeat | `node_<26-char ULID>` |
   | `plexor_host_ca_local_path` | copy of host's `dev-certs/ca.crt` (or `/var/lib/plexor/ca.crt` in prod) | path on controller |
   | `plexor_ca_fingerprint` | `openssl x509 -in host-ca.crt -noout -fingerprint -sha256` | `sha256:<64-hex>` |
   | `plexor_nodeagent_binary_sha256` | GitHub release page (`.sha256` file) | `<64-hex>` |

   The inventory example (`inventory.example.yml`) lists each var with
   a placeholder. Copy it to `inventory.yml` (gitignored) and fill
   in the values.

## Variable reference

### Required

| Variable | Example | Description |
|----------|---------|-------------|
| `plexor_host_url` | `https://plexor.example.com` | Plexor.Host control plane URL. The agent joins, heartbeats, and polls commands here. |
| `plexor_join_token` | `opaque-string` | Out-of-band enrollment token (matches the host's policy). |
| `plexor_node_id` | `node_01HXYZABCDEFGHJKMNPQRSTVW` | Plexor NodeId in wire form. Must match `^node_[0-9A-HJKMNP-TV-Z]{26}$`. |
| `plexor_nodeagent_version` | `v0.1.0` | Release tag from the dot-stbl/plexor GitHub releases page. |

### Optional (with defaults)

| Variable | Default | Description |
|----------|---------|-------------|
| `plexor_nodeagent_release_url` | `https://github.com/dot-stbl/plexor/releases/download/{version}/plexor-nodeagent-linux-x64.tar.gz` | Tarball URL template (`{version}` substituted). |
| `plexor_nodeagent_binary_sha256` | unset | If set, the download is verified against this SHA-256. |
| `plexor_ca_fingerprint` | unset | If set, the pre-shared CA is fingerprint-verified before install. |
| `plexor_host_ca_local_path` | unset | If set, the controller uploads this file to `/etc/plexor-nodeagent/host-ca.crt` on the VM before running `setup-mtls.sh`. |
| `plexor_nodeagent_user` | `plexor-nodeagent` | Service user the systemd unit runs as. |
| `plexor_nodeagent_group` | `plexor-nodeagent` | Service group. |
| `plexor_nodeagent_install_dir` | `/opt/plexor-nodeagent` | Binary install directory. |
| `plexor_nodeagent_etc_dir` | `/etc/plexor-nodeagent` | Config + env + certs directory. |
| `plexor_nodeagent_cert_dir` | `/etc/plexor-nodeagent/certs` | mTLS triple (the agent writes here on first /join). |
| `plexor_nodeagent_data_dir` | `/var/lib/plexor-nodeagent` | Per-node scratch state (image cache, workload metadata). |

## Invocation

```bash
# 1. Copy + edit the inventory
cp deploy/ansible/inventory.example.yml deploy/ansible/inventory.yml
$EDITOR deploy/ansible/inventory.yml

# 2. Verify the playbook parses (recommended on the first run)
ansible-playbook -i deploy/ansible/inventory.yml \
    deploy/ansible/install-nodeagent.yml --syntax-check

# 3. Dry-run (check mode) — see what would change without applying
ansible-playbook -i deploy/ansible/inventory.yml \
    deploy/ansible/install-nodeagent.yml --check --diff

# 4. Apply
ansible-playbook -i deploy/ansible/inventory.yml \
    deploy/ansible/install-nodeagent.yml --ask-become-pass

# Re-run is safe (idempotent). To force a service restart after
# env-file edits, omit the --diff and the existing handler chain
# picks it up via notify.
```

If you don't have passwordless sudo, `--ask-become-pass` prompts for
the BECOME password. For CI, pass `--become-method=sudo -K` to read
it from stdin or use `--ask-vault-pass` + `ansible_ssh_common_args`
to inject a vault password.

## What the playbook does

In order:

1. **Validates** — asserts `plexor_node_id` matches the Plexor wire
   format and the host is Debian. Fails loudly before touching
   anything.
2. **apt update + system packages** — installs `qemu-kvm`,
   `libvirt-daemon`, `libvirt-clients`, `openssl`, `ca-certificates`,
   `curl`, `rsync`.
3. **.NET 10 runtime** — adds Microsoft's Debian apt repo and
   installs `aspnetcore-runtime-10.0` (or `dotnet-runtime-10.0` as
   fallback). The agent is a Worker SDK so the bare runtime
   suffices.
4. **`/dev/kvm` check** — warns (does not fail) if the device is
   missing; the agent falls back to TCG (slower software emulation)
   but VM ops still work.
5. **System user + directories** — creates `plexor-nodeagent`
   (system account, no login), and the install / etc / certs / data
   dirs at 0750, owned by the service user.
6. **Binary download + extract** — pulls the release tarball from
   GitHub (SHA-256 verified if `plexor_nodeagent_binary_sha256` is
   set), extracts into `/opt/plexor-nodeagent/`, sets the executable
   bit on `Plexor.NodeAgent`.
7. **Environment file** — writes `/etc/plexor-nodeagent/env` from
   inventory vars. Maps:
   - `plexor_host_url`        → `Plexor__ControlPlaneUrl`
   - hostname / cores / RAM   → `Plexor__Node__Hostname` /
                                 `Plexor__Node__CpuCores` /
                                 `Plexor__Node__RamBytes`
   - cert dir                 → `NodeAgent__Mtls__CertDirectory`

   `.NET` reads `__` as a section separator by default; the Plexor
   convention is `PLX_SECTION_KEY` (see
   `src/shared/infra/Plexor.Shared.Configuration/PlexorEnvironmentVariablesProvider.cs`),
   but `Plexor.NodeAgent` does NOT currently use the PLX_ provider
   so we fall back to standard double-underscore envs here.
8. **mTLS setup** — if `plexor_host_ca_local_path` is set, copies
   the host CA to `/etc/plexor-nodeagent/host-ca.crt` and verifies
   the SHA-256 fingerprint. Then runs `setup-mtls.sh` which
   generates a self-signed placeholder client cert + key with the
   correct Plexor NodeId CN, places the cert triple at
   `/etc/plexor-nodeagent/certs/`, and sets 0600 perms on the key.
   The placeholder is overwritten by
   `Plexor.Shared.Mtls.MtlsCertWriter` on the agent's first
   successful `/join`.
9. **systemd unit** — copies
   `deploy/systemd/plexor-nodeagent.service` to
   `/etc/systemd/system/`, enables + starts the service.

## Verification after deploy

```bash
# Service running?
systemctl status plexor-nodeagent

# Recent logs?
journalctl -u plexor-nodeagent -f

# mTLS triple present?
ls -la /etc/plexor-nodeagent/certs/
# expect: ca.crt (0644), node.crt (0644), node.key (0600)

# /dev/kvm available?
ls -la /dev/kvm

# libvirt access?
sudo -u plexor-nodeagent virsh list --all
# If that fails, the user isn't in the `libvirt` group. The playbook
# adds the Ansible user; add plexor-nodeagent separately or use
# `unix_sock_group = "libvirt"` in /etc/libvirt/libvirtd.conf.

# Smoke test (libvirt + KVM round trip — does NOT need the host)
sudo /opt/plexor-nodeagent/../scripts/smoke-test-vm-create.sh
# (or copy deploy/scripts/smoke-test-vm-create.sh onto the VM)
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `apt_repository` 404s | Wrong Debian codename in the Microsoft repo URL | Confirm `ansible_facts['distribution_version']` is `12`. Update the URL in the playbook if not. |
| Service refuses to start with "permission denied" on `/dev/kvm` | `plexor-nodeagent` user not in `kvm` group | `usermod -aG kvm plexor-nodeagent` + `systemctl restart plexor-nodeagent`. |
| Service starts then exits with `Cannot open libvirt socket` | `plexor-nodeagent` user not in `libvirt` group | `usermod -aG libvirt plexor-nodeagent` + `systemctl restart plexor-nodeagent`. |
| `setup-mtls.sh` fails: "CA fingerprint mismatch" | Wrong `plexor_ca_fingerprint` in the inventory, or wrong `plexor_host_ca_local_path` source | Re-run `openssl x509 -in <ca.crt> -noout -fingerprint -sha256` on the **host** that generated the CA. Update the inventory. |
| Service starts but `journalctl -u plexor-nodeagent -f` shows `Join failed; retrying` | Control plane URL wrong, host unreachable, or `/etc/plexor-nodeagent/env` not loaded | `curl -sf $plexor_host_url/health` from the VM; `systemctl show plexor-nodeagent -p EnvironmentFiles` should list `/etc/plexor-nodeagent/env`. |
| Download fails with checksum mismatch | `plexor_nodeagent_binary_sha256` set but doesn't match the release | Re-fetch the `.sha256` file from the GitHub release page; copy-paste carefully. |
| `sha256:0000000000000000000000000000000000000000000000000000000000000000` always matches | You forgot `plexor_nodeagent_binary_sha256` — the playbook default disables the check | Set the var. Don't ship without integrity verification. |

## Files

```
deploy/
├── ansible/
│   ├── README.md                  ← you are here
│   ├── install-nodeagent.yml      ← the playbook
│   └── inventory.example.yml      ← template; copy + edit
├── scripts/
│   ├── setup-mtls.sh              ← generates placeholder cert, verifies CA fp
│   └── smoke-test-vm-create.sh    ← offline libvirt/KVM round-trip
└── systemd/
    └── plexor-nodeagent.service   ← Type=simple; systemd-notify upgrade TBD
```

## Limitations (v0.1)

- **Microsoft's apt repo**: the .NET 10 Debian packages may not yet
  be on the CDN when you read this. The playbook tries
  `aspnetcore-runtime-10.0` first, then `dotnet-runtime-10.0`. Both
  fail soft right now (`ignore_errors: true`); if both 404, the
  service won't start. Workaround: install .NET 10 by hand first,
  re-run.
- **Release URL**: hardcoded to GitHub releases on
  `dot-stbl/plexor`. Replace `plexor_nodeagent_release_url` if you
  publish elsewhere (S3, internal mirror).
- **No automatic upgrade**: the binary is downloaded once with
  `creates:` guard. To upgrade, bump `plexor_nodeagent_version` and
  re-run; the unarchive task re-extracts on top of the existing
  install dir.
- **systemd Type=simple**: the worker doesn't yet wire
  `Microsoft.Extensions.Hosting.Systemd`. Service is reported "ready"
  the moment the process forks; readiness of the mTLS join is only
  visible via `journalctl`. Upgrade path documented in the unit
  comment.
- **Hardcoded distro**: only Debian 12 is tested. The early
  `apt_repository` URL pins `bookworm`. Adapt for Ubuntu/Debian-13
  by templating the codename on `ansible_facts['distribution_version']`.