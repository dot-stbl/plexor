# Plexor — Prepared-Host Runbook

> **Audience:** operator standing up a Plexor compute node from scratch.
> **Scope:** every step from "I have a Debian box" to "Plexor.NodeAgent
> is running and ready to host VMs". Intentionally prescriptive — the
> shape of the host is what makes the KVM/Libvirt provider work.
>
> **For:** `P4-1 · Prepared-host runbook + dedicated KVM VM` (#23).
> **Companions:** `deploy/ansible/install-nodeagent.yml` (Ansible
> provisioner), `deploy/scripts/hardware-smoke.sh` (acceptance smoke),
> `deploy/scripts/acceptance-gate.sh` (CI gate).

## 1. Prerequisites

### 1.1 Hardware

| Profile | Spec | When |
|---------|------|------|
| **Minimum** | 1 vCPU, 2 GB RAM, 10 GB disk, **VT-x / AMD-V enabled in BIOS** | Smoke-test only (one throwaway VM). |
| **Recommended** | 4+ cores, 8 GB+ RAM, 40 GB SSD, dedicated NIC | Production-like host for the NodeAgent with 5-10 guest VMs. |
| **Production** | ECC RAM, redundant power, out-of-band IPMI | Bare-metal deploy. |

> **KVM is non-negotiable.** The NodeAgent uses KVM for all VM workloads.
> If `/dev/kvm` is missing the agent falls back to TCG (software
> emulation) — functional but ~10-50x slower. Always check that
> virtualization is enabled in the BIOS/firmware before installing.

### 1.2 Network

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| 22 | TCP | inbound | Operator SSH (Ansible control + smoke-test runner). |
| 16509 | TCP | localhost  | libvirtd TLS (used by NodeAgent). Bound to `localhost` only; no external access. |
| 5900+ | TCP | inbound (optional) | VNC for direct console access to VMs. Off by default; opt-in per VM. |
| 48001 | TCP | outbound | Plexor.Host control plane (HTTP) — the NodeAgent polls here. |
| 48002 | TCP | outbound | Plexor.Host control plane (mTLS) — the NodeAgent joins here. |
| 53 / 67 / 68 | UDP | local dnsmasq | libvirt's `default` network — DHCP for guest VMs. |

> **Egress to `dl-cdn.alpinelinux.org` is required** for the smoke
> test (downloads the Alpine ISO at run time). For an air-gapped setup,
> pre-place the ISO in `/var/lib/libvirt/images/` and patch
> `IMAGE_URL` in the smoke script.

### 1.3 Software

- **OS:** Debian 12 (bookworm) — the only distro the Ansible playbook
  validates. Ubuntu 24.04 works identically; adapt the apt repo URL
  in `install-nodeagent.yml` to your distro's codename.
- **Outbound internet** for `apt-get` + Ansible Galaxy (if used).
- **Sudo rights** for the operator account (or root).
- **SSH key** — public key copied to `~/.ssh/authorized_keys` for the
  Ansible controller.

### 1.4 Secrets you will need

| Secret | Source | When |
|--------|--------|------|
| `plexor_join_token` | Plexor.Host operator (out-of-band) | Before running the Ansible playbook. |
| `plexor_node_id` | Host (`/api/v1/nodes/{id}` once first heartbeat has fired) | After the agent's first `/join`. Regenerate-able. |
| `plexor_ca_fingerprint` | `openssl x509 -in host-ca.crt -noout -fingerprint -sha256` on the Plexor.Host | Before the playbook, if you want mTLS enforced. |
| `plexor_host_ca_local_path` | Copy of the host's `dev-certs/ca.crt` (or `/var/lib/plexor/ca.crt` in prod) to the controller | Before the playbook. |
| `plexor_nodeagent_binary_sha256` | GitHub release page (the `.sha256` file) | Before the playbook, for integrity verification. |

## 2. Provisioning

### 2.1 Install KVM and libvirt

```bash
sudo apt-get update
sudo apt-get install -y \
  qemu-kvm \
  libvirt-daemon-system \
  libvirt-clients \
  dnsmasq-base \
  bridge-utils \
  virtinst \
  qemu-utils
```

What each package gives you:

- `qemu-kvm` — the userland QEMU + the kernel `kvm` module. Provides
  `/dev/kvm`.
- `libvirt-daemon-system` — the `libvirtd` service that brokers access
  to QEMU via a stable API.
- `libvirt-clients` — `virsh` + the libvirt client libraries.
- `dnsmasq-base` — needed by the `default` virtual network for DHCP/DNS.
- `bridge-utils` — `brctl` for managing Linux bridges (used by the
  `default` network).
- `virtinst` — `virt-install`, used by the smoke test.
- `qemu-utils` — `qemu-img`, used by the smoke test to create qcow2.

### 2.2 Add the operator to the `libvirt` + `kvm` groups

```bash
# Operator + the agent user both need group access. Create the agent
# user if you haven't yet (the playbook does this too).
sudo usermod -aG libvirt,kvm "$USER"
sudo usermod -aG libvirt,kvm plexor-nodeagent
```

Group membership is consulted at login. Either `newgrp libvirt` for the
current shell or log out and back in. Verify with:

```bash
groups "$USER"           # includes libvirt, kvm
sudo -u plexor-nodeagent virsh list --all   # may fail before first service start
```

### 2.3 Enable + start libvirtd

```bash
sudo systemctl enable --now libvirtd
sudo systemctl status libvirtd   # expect "active (running)"
```

Start the default virtual network if it isn't already up:

```bash
sudo virsh net-start default
sudo virsh net-autostart default
sudo virsh net-list --all        # default should be 'active' + 'yes' (autostart)
```

### 2.4 Verify KVM is exposed

```bash
ls -la /dev/kvm     # expect: crw-rw---- 1 root kvm ...  /dev/kvm
lscpu | grep -E 'Virtualization|Hypervisor'   # expect: Virtualization: VT-x (or AMD-V)
```

If `/dev/kvm` is absent: the kernel module isn't loaded, or VT is off
in firmware. Try `sudo modprobe kvm && sudo modprobe kvm_intel`
(or `kvm_amd`) and re-check. If that fails too, reboot into firmware
settings and enable CPU virtualization.

## 3. Install Plexor.NodeAgent

The Ansible playbook does all of this idempotently. From the Ansible
controller:

```bash
cp deploy/ansible/inventory.example.yml deploy/ansible/inventory.yml
$EDITOR deploy/ansible/inventory.yml
# Fill in: plexor_host_url, plexor_join_token, plexor_node_id,
#          plexor_nodeagent_version, plexor_ca_fingerprint,
#          plexor_host_ca_local_path, plexor_nodeagent_binary_sha256.

ansible-playbook -i deploy/ansible/inventory.yml \
  deploy/ansible/install-nodeagent.yml --ask-become-pass
```

What it does (full detail in `deploy/ansible/README.md`):

1. **Validates** the inventory (`plexor_node_id` wire-format regex,
   distro is Debian, host URL parses).
2. **apt install** — `qemu-kvm`, `libvirt-daemon`, `libvirt-clients`,
   `openssl`, `ca-certificates`, `curl`, `rsync`.
3. **.NET 10 runtime** — from Microsoft's apt repo.
4. **`/dev/kvm` check** — warns (does not fail) if missing.
5. **System user** — `plexor-nodeagent`, no login, no home.
6. **Directories** — `/opt/plexor-nodeagent` (binary),
   `/etc/plexor-nodeagent` (config + env + certs),
   `/var/lib/plexor-nodeagent` (data).
7. **Binary** — downloads the GitHub release tarball, SHA-256 verifies
   if `plexor_nodeagent_binary_sha256` is set, extracts, sets
   `0750` on the binary.
8. **Environment file** at `/etc/plexor-nodeagent/env` — control plane
   URL, hostname, CPU/RAM caps, cert directory.
9. **mTLS triple** at `/etc/plexor-nodeagent/certs/` — CA root
   (from pre-shared `host-ca.crt`),
   placeholder client cert + key (overwritten on first successful `/join`).
10. **systemd unit** at `/etc/systemd/system/plexor-nodeagent.service`
    — `Type=simple`, enables + starts the service.

Re-running the playbook is safe — every task is `creates:`-gated and
the systemd handlers only fire on change.

### 3.1 Quick start (Ansible bypass)

If you can't run Ansible (air-gap, custom orchestration), the steps
above translate directly into shell. Order matters: deps before
binary, env before service, mTLS before first `/join`.

## 4. Network setup

The smoke test relies on the libvirt **default** network. If you want
a custom bridge (multiple subnets, isolated tenant traffic), create it
up-front and tell the NodeAgent:

```bash
# Show the default
sudo virsh net-dumpxml default | head -40

# Default values:
#  - name:    default
#  - bridge:  virbr0
#  - forward: nat (iptables-based)
#  - DHCP:    192.168.122.2 - 192.168.122.254
#  - DNS:     192.168.122.1 (libvirtd forwards to host's resolver)

# Optional: create a named bridge instead
sudo virsh net-define /dev/stdin <<'XML'
<network>
  <name>plexor-bridge</name>
  <forward mode='nat'/>
  <bridge name='virbr1' stp='on' delay='0'/>
  <ip address='192.168.124.1' netmask='255.255.255.0'>
    <dhcp>
      <range start='192.168.124.2' end='192.168.124.254'/>
    </dhcp>
  </ip>
</network>
XML
sudo virsh net-start plexor-bridge
sudo virsh net-autostart plexor-bridge

# Verify VM-to-VM reachability across the bridge (should be ping-able
# in <2 ms on loopback, <5 ms on a real bridge):
sudo virt-install --name probe --vcpus 1 --ram 512 \
  --disk none --boot hd --network bridge=virbr1 --graphics none \
  --import --noautoconsole --wait -1 \
  --cdrom /var/lib/libvirt/images/alpine-virt.iso 2>/dev/null || true
sudo virsh destroy probe && sudo virsh undefine probe
```

For a dedicated bridge you'll also need to set
`Plexor__Node__DefaultNetwork=plexor-bridge` in `/etc/plexor-nodeagent/env`.

## 5. First-boot verification

```bash
# Service unit started?
sudo systemctl status plexor-nodeagent --no-pager
# expect: Active: active (running)

# Recent logs?
sudo journalctl -u plexor-nodeagent -n 100 --no-pager
# expect: first few lines show a successful /join + periodic heartbeats

# Process running as plexor-nodeagent?
ps -o user,comm -p "$(pgrep -f Plexor.NodeAgent | head -1)"
# expect: plexor-nodeagent ... dotnet

# mTLS triple present?
ls -la /etc/plexor-nodeagent/certs/
# expect:
#   ca.crt   0644  plexor-nodeagent plexor-nodeagent
#   node.crt 0644  plexor-nodeagent plexor-nodeagent
#   node.key 0600  plexor-nodeagent plexor-nodeagent

# After first /join (a few seconds), the cert subject should change
# from "self-signed placeholder" to a Plexor-CA-signed cert — verify:
openssl x509 -in /etc/plexor-nodeagent/certs/node.crt -noout -issuer
# expect: issuer CN = ... Plexor CA
```

The NodeAgent is a Worker Service (no HTTP listener), so there is
**no `/health` endpoint to curl**. Health is `systemctl is-active
plexor-nodeagent` + the journal. A future control-plane integration
will expose NodeAgent health via the Host's `/api/v1/nodes/{id}`
endpoint — out of scope for this MVP.

## 6. Validation matrix

| Check | Command | Expected |
|-------|---------|----------|
| KVM device | `ls -la /dev/kvm` | `crw-rw---- 1 root kvm ... /dev/kvm` |
| KVM in CPU | `lscpu \| grep Virtualization` | `Virtualization: VT-x` (or AMD-V) |
| libvirtd running | `systemctl is-active libvirtd` | `active` |
| libvirtd enabled | `systemctl is-enabled libvirtd` | `enabled` |
| `virsh list` works | `sudo virsh list --all` | Header (`Id Name State`) then `running` for any active VM, or empty if none. Exits 0. |
| Default network | `sudo virsh net-list --all` | `default active yes` |
| Default bridge up | `ip -br link show virbr0` | `virbr0 ... UP` |
| DHCP range | `sudo virsh net-dumpxml default \| grep dhcp` | `<range start='192.168.122.2' end='192.168.122.254'/>` |
| Image dir writable | `sudo -u plexor-nodeagent touch /var/lib/libvirt/images/.write && sudo rm /var/lib/libvirt/images/.write` | exits 0 |
| Service running | `systemctl is-active plexor-nodeagent` | `active` |
| Service enabled | `systemctl is-enabled plexor-nodeagent` | `enabled` |
| Certs in place | `ls /etc/plexor-nodeagent/certs/` | `ca.crt  node.crt  node.key` |
| Key permission | `stat -c '%a %n' /etc/plexor-nodeagent/certs/node.key` | `600 /etc/plexor-nodeagent/certs/node.key` |
| Join success | `journalctl -u plexor-nodeagent --since '5 min ago' \| grep -i join` | "join succeeded" (no stack traces after the first /join) |
| Heartbeat | `journalctl -u plexor-nodeagent --since '5 min ago' \| grep -i heartbeat` | periodic heartbeat lines at the configured interval |
| Hardware smoke | `sudo deploy/scripts/hardware-smoke.sh` | `PASS   total: <Ns>` exit 0 |

If every row above returns its expected value, the host is fit for
production Plexor workloads.

## 7. Troubleshooting

### 7.1 `/dev/kvm` is missing

| Symptom | Cause | Fix |
|---------|-------|-----|
| `ls /dev/kvm` → "No such file" | VT-x/AMD-V off in firmware | Reboot → BIOS/UEFI → CPU config → enable `Intel VT-x` / `AMD-V` / `SVM Mode`. |
| Device file absent but `lscpu` shows virtualization supported | Kernel module not loaded | `sudo modprobe kvm && sudo modprobe kvm_intel` (or `kvm_amd`). Persist via `/etc/modules-load.d/kvm.conf`. |
| Inside a VM, `/dev/kvm` is missing even after `modprobe` | Nested virtualization off in the hypervisor | Nested virt is **host-firmware dependent**, not OS-configurable. Bare metal or a hypervisor that exposes it. |
| Device present but agent log shows `KVM: falling back to TCG` | Permission denied on `/dev/kvm` | `sudo usermod -aG kvm plexor-nodeagent && systemctl restart plexor-nodeagent`. |
| `ls /dev/kvm` → permission denied | Not in `kvm` group | `sudo usermod -aG kvm <user> && newgrp kvm`. |

### 7.2 `libvirtd` won't start

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Job for libvirtd.service failed because the control process exited` | Conflicting libvirt from another package | `sudo apt-get remove libvirt-bin` (the legacy init-script package on older distros). |
| `libvirtd: cannot bind to 0.0.0.0:16509` | Address already in use | `sudo ss -ltnp \| grep 16509` to find the conflict. Most often, two libvirt daemons running (`systemd` + sysv). Disable one. |
| `Permission denied` on `/var/run/libvirt/` | `libvirt` group ownership wrong | `sudo chown root:libvirt /var/run/libvirt && sudo systemctl restart libvirtd`. |
| Service stays in `activating (auto-restart)` | Underlying qemu failure | `journalctl -u libvirtd -n 200` then `journalctl -u qemu-guest-agent` (on the guest). |

### 7.3 mTLS enrolment fails

| Symptom | Cause | Fix |
|---------|-------|-----|
| `setup-mtls.sh` exits 1 with "CA fingerprint mismatch" | Inventory var `plexor_ca_fingerprint` differs from the actual host CA | Re-run `openssl x509 -in <ca.crt> -noout -fingerprint -sha256` on the Plexor.Host and copy-paste the value (lower-case hex, no colons). |
| Agent logs `TLS handshake: alert certificate unknown` | The agent's placeholder cert is being rejected by the host | Confirm the host is using the same CA that you pre-shared. If not, copy the right CA into the agent's `/etc/plexor-nodeagent/certs/ca.crt` and restart. |
| Agent logs `Permission denied` reading `node.key` | Cert dir owned by wrong user / mode too loose | `sudo install -d -m 0750 -o plexor-nodeagent -g plexor-nodeagent /etc/plexor-nodeagent/certs && sudo install -m 0600 -o plexor-nodeagent -g plexor-nodeagent node.key /etc/plexor-nodeagent/certs/`. |
| Agent logs `Connection refused` to control plane | Host URL wrong, or `Plexor.Host` not running on port 48002 | `curl -svk https://plexor.example.com:48002/healthz` from the VM. If unreachable, fix DNS/firewall/network; do not relax TLS verification as a workaround. |
| Agent logs `Hostname mismatch` | `Plexor__Node__Hostname` in `/etc/plexor-nodeagent/env` doesn't match the host's `hostname -f` | Edit `/etc/plexor-nodeagent/env`, then `sudo systemctl restart plexor-nodeagent`. |

### 7.4 Network misbehaviour

| Symptom | Cause | Fix |
|---------|-------|-----|
| `sudo virsh net-start default` → "network default is not active" | dnsmasq down / no lease file | `sudo systemctl restart dnsmasq` (or `libvirtd` — it manages dnsmasq for its networks on Debian). |
| VM gets 169.254.x.x (link-local) instead of 192.168.122.x | dnsmasq not running on the bridge | `ps aux \| grep dnsmasq` — should show `dnsmasq --conf-file=/var/lib/libvirt/dnsmasq/default.conf`. If absent, `sudo virsh net-destroy default && sudo virsh net-start default`. |
| VM-to-VM ping fails between two guests | Bridge `stp` cost, or one VM attached to wrong network | `brctl show virbr0` — both VM `vnet*` interfaces should be listed. If only one, the second VM's network didn't bind. Check the `virt-install --network` argument. |
| No egress from VM to the internet | `iptables` filter dropped forwarding on the libvirt chain | `sudo iptables -L FORWARD -n -v` — look for a `REJECT` or `DROP` rule. `/usr/sbin/libvirtd` adds an `ACCEPT` for the bridge by default; if it's missing, `sudo virsh net-destroy default && sudo virsh net-start default` re-creates it. |

### 7.5 Disk / storage

| Symptom | Cause | Fix |
|---------|-------|-----|
| `qemu-img create: permission denied` on `/var/lib/libvirt/images/` | `plexor-nodeagent` user not in `libvirt` group, or `images/` is mode 700 | `sudo chmod 755 /var/lib/libvirt/images && sudo usermod -aG libvirt plexor-nodeagent && systemctl restart plexor-nodeagent`. |
| Out of disk | `/var/lib/libvirt/images/` filled by orphaned qcow2s | `sudo virsh list --all --with-snapshot` then `sudo virsh undefine ... --remove-all-storage` per VM. The NodeAgent does not delete qcow2s on undefine — that's by design for forensic review. |
| Storage pool reports "no space left" but `df` shows space | Pool metadata is stale | `sudo virsh pool-refresh default`. |

## 8. References

- `deploy/ansible/README.md` — playbook variables and invocation.
- `deploy/ansible/install-nodeagent.yml` — the actual provisioner.
- `deploy/scripts/setup-mtls.sh` — mTLS triple installer (referenced
  by the playbook).
- `deploy/scripts/smoke-test-vm-create.sh` — legacy "does libvirt
  work" smoke (instant-defines + tears down a tiny Alpine VM);
  succeeded-by `deploy/scripts/hardware-smoke.sh` for full lifecycle.
- `deploy/scripts/hardware-smoke.sh` — `P4-2` lifecycle smoke
  (create → boot → reachable → snapshot → restore → destroy).
- `deploy/scripts/acceptance-gate.sh` — `P4-3` CI gate (the smoke +
  timing + JSON emission).
- `.agents/docs/architecture/traffic.md` — how the NodeAgent, Host,
  and CLI wire together at the protocol level (mTLS join + heartbeats).
