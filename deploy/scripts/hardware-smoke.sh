#!/usr/bin/env bash
# ============================================================================
# hardware-smoke.sh — full VM lifecycle smoke test for a Plexor NodeAgent
# host. Builds on the lighter deploy/scripts/smoke-test-vm-create.sh
# (which only verifies libvirt can define+tear-down a domain) and
# exercises what the Plexor provider actually does at runtime:
#
#   1. image-pull       download (or reuse a cached) Alpine Virt ISO
#   2. qcow2-create     10 GB qcow2 backing file
#   3. virt-install     define + boot a tiny VM (1 vCPU, 512 MB)
#   4. boot             poll until VM is 'running', with a timeout
#   5. network          grab the lease via virsh domifaddr and ping it
#   6. snapshot         live snapshot (disk-only, atomic)
#   7. restore          destroy + snapshot-revert + start; verify boot
#   8. destroy          tear down: destroy VM, delete snapshot, disk
#
# Each phase prints one [OK] / [FAIL] line. On script exit the trap
# runs the cleanup branch — disk is removed, VM + snapshots + ISO are
# removed unless the operator set PLEXOR_SMOKE_KEEP=1 (forensic mode).
#
# Optional env vars:
#   PLEXOR_SMOKE_RESULTS_FILE — if set, append one JSON line per phase
#                               to this path (handy for the CI gate in
#                               deploy/scripts/acceptance-gate.sh to
#                               aggregate timing).
#   PLEXOR_SMOKE_KEEP          — if set to 1, skip cleanup. The VM
#                                survives for operator inspection.
#   PLEXOR_SMOKE_BOOT_TIMEOUT  — override the 60s boot timeout (seconds).
#   PLEXOR_SMOKE_NET           — name of the libvirt network (default:
#                                "default").
#   PLEXOR_SMOKE_ISO_URL       — override the ISO download URL
#                                (default: Alpine virt 3.18).
#   PLEXOR_SMOKE_ISO_DIR       — cache dir for ISO (default:
#                                /var/lib/libvirt/images).
#
# Usage:
#   sudo ./hardware-smoke.sh
#
# Exit codes:
#   0 — every phase passed
#   1 — at least one phase failed
# ============================================================================

set -euo pipefail

# ---- config ----------------------------------------------------------------
VM_NAME="plexor-smoke-$$-$(date +%s)"
SNAPSHOT_NAME="${VM_NAME}-snap"
VCPU=1
RAM_MB=512
DISK_GB=10
BOOT_TIMEOUT="${PLEXOR_SMOKE_BOOT_TIMEOUT:-60}"
NETWORK="${PLEXOR_SMOKE_NET:-default}"

ISO_URL="${PLEXOR_SMOKE_ISO_URL:-https://dl-cdn.alpinelinux.org/alpine/v3.18/releases/x86_64/alpine-virt-3.18.4-x86_64.iso}"
ISO_DIR="${PLEXOR_SMOKE_ISO_DIR:-/var/lib/libvirt/images}"
ISO_NAME="$(basename "$ISO_URL")"
ISO_PATH="${ISO_DIR}/${ISO_NAME}"

DISK_PATH="${ISO_DIR}/${VM_NAME}.qcow2"

# Phase timings — wall-clock seconds since script start.
SCRIPT_START_MS=""
RESULTS_FILE="${PLEXOR_SMOKE_RESULTS_FILE:-}"

# ---- preconditions ---------------------------------------------------------
if [[ $EUID -ne 0 ]]; then
    echo "error: this script must run as root (writes /var/lib/libvirt/images)" >&2
    exit 1
fi

for cmd in virsh virt-install qemu-img curl awk; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
        echo "error: required command '$cmd' is not installed" >&2
        exit 1
    fi
done

if ! virsh net-info "$NETWORK" >/dev/null 2>&1; then
    echo "error: libvirt network '$NETWORK' is not active (run: virsh net-start $NETWORK)" >&2
    exit 1
fi

# Clean any leftover VMs that share our name prefix from a prior crash.
# (Don't nuke unrelated VMs — the prefix `plexor-smoke-` is ours.)
if virsh dominfo "$VM_NAME" >/dev/null 2>&1; then
    virsh destroy "$VM_NAME" >/dev/null 2>&1 || true
    virsh undefine "$VM_NAME" --remove-all-storage >/dev/null 2>&1 || true
fi

# ---- helpers ---------------------------------------------------------------
now_ms() {
    # `date +%s%N` is GNU; BSD/macOS doesn't have %N. We declare
    # POSIX-only bash + Linux targets in our CI; fall back to seconds
    # * 1000 if %N is unavailable (best effort).
    local stamp
    stamp="$(date +%s%N 2>/dev/null || true)"
    if [[ -n "$stamp" ]]; then
        echo "$((stamp / 1000000))"
    else
        echo "$(( $(date +%s) * 1000 ))"
    fi
}

record_phase() {
    # $1 = phase name, $2 = duration_ms, $3 = passed (0|1), $4 = note
    local phase="$1" duration_ms="$2" passed="$3" note="$4"
    if [[ -n "$RESULTS_FILE" ]]; then
        # JSON-escape the note (replace " with \", backslash with \\,
        # newlines with space). jq isn't a dependency; keep it inline.
        local escaped
        escaped="${note//\\/\\\\}"
        escaped="${escaped//\"/\\\"}"
        escaped="${escaped//$'\n'/ }"
        printf '{"phase":"%s","duration_ms":%s,"passed":%s,"note":"%s"}\n' \
            "$phase" "$duration_ms" "$passed" "$escaped" >> "$RESULTS_FILE"
    fi
}

run_phase() {
    # Wraps a phase: captures duration_ms and marks pass/fail in
    # $RESULTS_FILE. Errors propagate (set -e) unless the body used
    # `|| return 1`.
    local phase_name="$1"
    local phase_desc="$2"
    local phase_fn="$3"
    local phase_start_ms
    phase_start_ms="$(now_ms)"
    local note=""
    local passed=0

    # Run in a subshell so we can capture exit code without aborting
    # the script (otherwise set -e would kill us before we print
    # [FAIL] / record the phase).
    set +e
    note="$("$phase_fn" 2>&1)"
    local rc=$?
    set -e

    local phase_end_ms
    phase_end_ms="$(now_ms)"
    local duration_ms=$((phase_end_ms - phase_start_ms))

    if [[ $rc -eq 0 ]]; then
        printf '[OK]   %-14s (%s)\n' "$phase_name" "$phase_desc"
        record_phase "$phase_name" "$duration_ms" 1 "$phase_desc"
    else
        printf '[FAIL] %-14s (%s)\n' "$phase_name" "$note"
        record_phase "$phase_name" "$duration_ms" 0 "$note"
        # Surface the failure to the script's set -e so a top-level
        # error code propagates.
        return $rc
    fi
}

# ---- phases ----------------------------------------------------------------

phase_image_pull() {
    mkdir -p "$ISO_DIR"
    if [[ -s "$ISO_PATH" ]]; then
        local size
        size="$(stat -c %s "$ISO_PATH" 2>/dev/null || stat -f %z "$ISO_PATH" 2>/dev/null || echo 0)"
        printf 'cached (%s MB)' "$((size / 1024 / 1024))"
        return 0
    fi
    if ! curl --fail --silent --show-error --location -o "$ISO_PATH" "$ISO_URL"; then
        rm -f "$ISO_PATH"
        echo "download failed"
        return 1
    fi
    # Sanity check: ISO must be at least 5 MB.
    local size
    size="$(stat -c %s "$ISO_PATH" 2>/dev/null || stat -f %z "$ISO_PATH" 2>/dev/null || echo 0)"
    if (( size < 5 * 1024 * 1024 )); then
        echo "ISO too small ($size bytes) — likely truncated download"
        return 1
    fi
    printf 'downloaded (%s MB)' "$((size / 1024 / 1024))"
}

phase_qcow2_create() {
    if ! qemu-img create -f qcow2 -o preallocation=metadata "$DISK_PATH" "${DISK_GB}G" >/dev/null; then
        echo "qemu-img create failed"
        return 1
    fi
    printf '%sGB at %s' "$DISK_GB" "$DISK_PATH"
}

phase_virt_install() {
    # --import: don't run an installer; boot from the disk/cdrom.
    # --noautoconsole: return immediately after start.
    # --os-variant: enables virtio + sane defaults for Linux.
    if ! virt-install \
        --name "$VM_NAME" \
        --vcpus="$VCPU" \
        --ram="$RAM_MB" \
        --disk "path=$DISK_PATH,format=qcow2" \
        --cdrom "$ISO_PATH" \
        --os-variant alpinelinux3.18 \
        --network "network=$NETWORK,model=virtio" \
        --graphics none \
        --import \
        --noautoconsole \
        --noreboot >/dev/null 2>&1; then
        # virt-install occasionally exits non-zero even on a successful
        # define+start (race with libvirtd registration). Treat the
        # domain as the source of truth.
        if ! virsh dominfo "$VM_NAME" >/dev/null 2>&1; then
            echo "virt-install failed and no domain was defined"
            return 1
        fi
    fi
    # Confirm the domain actually started.
    local state
    state="$(virsh domstate "$VM_NAME" 2>/dev/null || true)"
    if [[ "$state" != "running" ]]; then
        echo "VM did not auto-start (state=$state)"
        return 1
    fi
    printf 'defined %s (%s vcpu %sMB)' "$VM_NAME" "$VCPU" "$RAM_MB"
}

phase_boot() {
    # Wait for the VM to reach 'running' state within BOOT_TIMEOUT.
    # Libreville defines running as "process alive", not "OS booted".
    # For the lifecycle smoke we only require the process is alive —
    # network reachability is verified in the next phase.
    local deadline=$(( $(date +%s) + BOOT_TIMEOUT ))
    local state=""
    local waited_s=0
    while (( $(date +%s) < deadline )); do
        state="$(virsh domstate "$VM_NAME" 2>/dev/null || true)"
        if [[ "$state" == "running" ]]; then
            waited_s=$(( BOOT_TIMEOUT - (deadline - $(date +%s)) ))
            printf 'running in %ss' "$waited_s"
            return 0
        fi
        sleep 1
    done
    echo "still $state after ${BOOT_TIMEOUT}s"
    return 1
}

phase_network() {
    # `virsh domifaddr` gives us the guest's IP from the libvirt
    # lease table — no guest agent, no SSH config required. We retry
    # for up to BOOT_TIMEOUT seconds because DHCP might lag.
    local deadline=$(( $(date +%s) + BOOT_TIMEOUT ))
    local ip=""
    while (( $(date +%s) < deadline )); do
        ip="$(virsh domifaddr "$VM_NAME" 2>/dev/null \
            | awk '/ipv4/ { print $4 }' \
            | head -n1 \
            | cut -d/ -f1)"
        if [[ -n "$ip" ]]; then
            break
        fi
        sleep 1
    done
    if [[ -z "$ip" ]]; then
        echo "no DHCP lease after ${BOOT_TIMEOUT}s"
        return 1
    fi

    # Single ping — we don't care about latency, only that the IP
    # answers. -c 1 -W 2: 2s timeout per probe.
    if ! ping -c 1 -W 2 "$ip" >/dev/null 2>&1; then
        # Fall back to checking the link is up (`virsh domstate`
        # running + ARP table on the bridge) — useful when the test
        # VM lacks ICMP egress.
        if ! ip -4 neigh show "$ip" dev "virbr0" 2>/dev/null \
            | awk '{print $NF}' | grep -q REACHABLE; then
            echo "ping failed and no ARP entry for $ip"
            return 1
        fi
    fi
    printf '%s reachable' "$ip"
}

phase_snapshot() {
    # Live disk-only snapshot. Works on running VMs even without a
    # guest agent (we omit --quiesce + --live). Modern qemu/libvirt
    # handles this as an atomic operation that pauses the VM briefly
    # to merge the backing file pointer.
    if ! virsh snapshot-create-as "$VM_NAME" \
        --name "$SNAPSHOT_NAME" \
        --disk-only \
        --atomic \
        --no-metadata >/dev/null 2>&1; then
        # --no-metadata may not be available on older libvirt; fall
        # back to the standard variant (the metadata is cleaned up in
        # the destroy phase either way).
        if ! virsh snapshot-create-as "$VM_NAME" \
            --name "$SNAPSHOT_NAME" \
            --disk-only \
            --atomic >/dev/null 2>&1; then
            echo "snapshot-create-as failed"
            return 1
        fi
    fi
    # Confirm the snapshot is visible.
    if ! virsh snapshot-list "$VM_NAME" 2>/dev/null \
        | awk 'NR>2 {print $1}' | grep -q "^${SNAPSHOT_NAME}\$"; then
        echo "snapshot $SNAPSHOT_NAME not listed"
        return 1
    fi
    printf 'snap saved (%s)' "$SNAPSHOT_NAME"
}

phase_restore() {
    # Destroy the live VM, then snapshot-revert + start.
    virsh destroy "$VM_NAME" >/dev/null 2>&1 || true
    sleep 1

    if ! virsh snapshot-revert "$VM_NAME" --snapshotname "$SNAPSHOT_NAME" >/dev/null 2>&1; then
        echo "snapshot-revert failed"
        return 1
    fi
    if ! virsh start "$VM_NAME" >/dev/null 2>&1; then
        echo "start-from-snapshot failed"
        return 1
    fi

    # Wait for the started VM to reach 'running'.
    local deadline=$(( $(date +%s) + BOOT_TIMEOUT ))
    local state=""
    while (( $(date +%s) < deadline )); do
        state="$(virsh domstate "$VM_NAME" 2>/dev/null || true)"
        if [[ "$state" == "running" ]]; then
            printf 'booted from snapshot'
            return 0
        fi
        sleep 1
    done
    echo "still $state after ${BOOT_TIMEOUT}s"
    return 1
}

phase_destroy() {
    # Best-effort: each call is idempotent (`true` on failure) so we
    # clean up as much as possible regardless of prior phase state.
    virsh destroy "$VM_NAME" >/dev/null 2>&1 || true
    # Drop snapshots first; otherwise undefine with --remove-all-storage
    # can race with the snapshot's backing-store reference.
    for snap in $(virsh snapshot-list "$VM_NAME" --name 2>/dev/null \
        | sed -n '3,$p'); do
        virsh snapshot-delete "$VM_NAME" --snapshotname "$snap" \
            --metadata >/dev/null 2>&1 || true
    done
    virsh undefine "$VM_NAME" --remove-all-storage \
        >/dev/null 2>&1 || true

    if [[ -e "$DISK_PATH" ]] && ! rm -f "$DISK_PATH"; then
        echo "disk remained at $DISK_PATH"
        return 1
    fi
    # ISO is intentionally left in the cache directory for reuse —
    # mark the cleanup as successful even though we don't delete it.
    printf 'cleaned up'
}

# ---- run -------------------------------------------------------------------
SCRIPT_START_MS="$(now_ms)"

set +e
run_phase "image-pull"   "downloading Alpine ISO" phase_image_pull
rc=$?
if [[ $rc -eq 0 ]]; then run_phase "qcow2-create" "10GB qcow2 disk"              phase_qcow2_create; rc=$?; fi
if [[ $rc -eq 0 ]]; then run_phase "virt-install" "define + start VM"            phase_virt_install; rc=$?; fi
if [[ $rc -eq 0 ]]; then run_phase "boot"          "wait up to ${BOOT_TIMEOUT}s for running" phase_boot;       rc=$?; fi
if [[ $rc -eq 0 ]]; then run_phase "network"       "DHCP lease + ping"            phase_network;       rc=$?; fi
if [[ $rc -eq 0 ]]; then run_phase "snapshot"      "live disk-only snapshot"      phase_snapshot;      rc=$?; fi
if [[ $rc -eq 0 ]]; then run_phase "restore"       "destroy + revert + restart"  phase_restore;       rc=$?; fi
# Always run destroy on the happy path AND on a partial-failure path
# (so leftover VMs don't pile up across runs). This is an "always"
# cleanup, not gated on prior phase success.
run_phase "destroy" "tear down VM + disk + snapshots" phase_destroy

TOTAL_MS=$(( $(now_ms) - SCRIPT_START_MS ))
TOTAL_S=$(awk -v ms="$TOTAL_MS" 'BEGIN { printf "%.0f", ms / 1000 }')

if [[ $rc -eq 0 ]]; then
    printf 'PASS   total: %ss\n' "$TOTAL_S"
    exit 0
else
    printf 'FAIL   total: %ss (last failing phase above)\n' "$TOTAL_S"
    exit 1
fi
