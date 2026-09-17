#!/bin/bash
# ============================================================================
# smoke-test-vm-create.sh — libvirt smoke test for the Plexor test VM.
#
# Creates a tiny Alpine VM via libvirt on the test VM. Verifies that
# (a) libvirt is installed and running, (b) /var/lib/libvirt is writable
# by the deploy user (or sudo works), (c) the default network bridge
# virbr0 is up. Run by .github/workflows/smoke-test.yml after a successful
# deploy to confirm the VM is fit to host Plexor workloads.
#
# Limitations: this is NOT a true NodeAgent end-to-end test. It exercises
# libvirt directly, not via Plexor.Host → Plexor.NodeAgent → libvirt.
# A real E2E would require a deploy of Plexor.Host alongside the agent,
# then POSTing a workload and observing the agent create the VM.
# That's a multi-system test — out of scope for this MVP.
#
# Idempotent: the trap-on-EXIT block removes the VM, the qcow2 disk,
# and the downloaded ISO regardless of how the script exits. Re-running
# is safe.
#
# Usage:
#   sudo ./smoke-test-vm-create.sh
#
# Exit codes:
#   0 — VM created successfully (cleanup ran on EXIT).
#   1 — libvirt missing, ISO download failed, or virt-install failed.
# ============================================================================

set -euo pipefail

# ---- Configuration ----------------------------------------------------------
VM_NAME="plexor-smoke-$(date +%s)"
VCPU=1
RAM_MB=512
DISK_GB=2
IMAGE_URL="https://dl-cdn.alpinelinux.org/alpine/v3.18/releases/x86_64/alpine-virt-3.18.4-x86_64.iso"
IMAGE_PATH="/tmp/alpine-virt.iso"
DISK_PATH="/var/lib/libvirt/images/${VM_NAME}.qcow2"

# ---- Cleanup on exit --------------------------------------------------------
# Runs on every exit (success, failure, signal). Best-effort — if a cleanup
# command itself fails, log and continue. The orchestrator (CI workflow)
# inspects the script's exit code, which is set by the body, not by trap.
cleanup() {
    local rc=$?
    if [ -n "${VM_NAME:-}" ]; then
        # `virsh destroy` is idempotent — fails on an already-stopped VM.
        virsh destroy "$VM_NAME" >/dev/null 2>&1 || true
        virsh undefine "$VM_NAME" >/dev/null 2>&1 || true
    fi
    [ -n "${DISK_PATH:-}" ] && [ -e "$DISK_PATH" ] && rm -f "$DISK_PATH" || true
    [ -n "${IMAGE_PATH:-}" ] && [ -e "$IMAGE_PATH" ] && rm -f "$IMAGE_PATH" || true
    # Don't override the script's real exit code with cleanup failures.
    exit "$rc"
}
trap cleanup EXIT

# ---- Preflight --------------------------------------------------------------
# Bail early if libvirt isn't even there. Don't want a cryptic
# "virsh: command not found" 30 seconds into the run.
for cmd in virsh virt-install qemu-img curl; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
        echo "FAIL: required command '$cmd' is not installed"
        exit 1
    fi
done

if ! virsh net-info default >/dev/null 2>&1; then
    echo "FAIL: libvirt 'default' network is not active (run: virsh net-start default)"
    exit 1
fi

# ---- Run --------------------------------------------------------------------
echo "Downloading Alpine ISO..."
curl -fsSL -o "$IMAGE_PATH" "$IMAGE_URL" || { echo "FAIL: ISO download"; exit 1; }

echo "Creating qcow2 disk..."
qemu-img create -f qcow2 -o preallocation=metadata "$DISK_PATH" "${DISK_GB}G"

echo "Defining VM..."
virt-install \
  --name "$VM_NAME" \
  --vcpus="$VCPU" \
  --ram="$RAM_MB" \
  --disk "path=$DISK_PATH,format=qcow2" \
  --cdrom "$IMAGE_PATH" \
  --os-variant alpinelinux3.18 \
  --network bridge=virbr0 \
  --graphics none \
  --import \
  --noautoconsole || { echo "FAIL: virt-install"; exit 1; }

# Give libvirtd a beat to register the domain before we tear it down
# (otherwise `virsh destroy` can race the registration and return "not found").
sleep 1

echo "PASS: VM '$VM_NAME' created"
echo "Cleanup will run automatically on script exit."
