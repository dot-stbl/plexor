# Change: libvirt-provider

## Why

`fe-vm-lifecycle` shipped the `IComputeProvider` seam and
the `NoOpComputeProvider` default. The seam exists so the
real compute backends can plug in behind one interface,
but the v0.1 default doesn't actually create VMs — a
self-hosted deploy that wants real KVM / QEMU workloads
has to wire a real provider.

This change ships the libvirt provider. It's the first
concrete `IComputeProvider` implementation:
`LibvirtComputeProvider : IComputeProvider`, implemented
via `virsh` / `virt-install` shell-out. An operator who
turns on the libvirt provider on a host with KVM, libvirtd,
and a base image pool installed sees workloads become real
VMs — the state machine transitions the same way (Pending
→ Provisioning → Running), but `LocalId` is now a libvirt
UUID and `virsh dominfo $UUID` returns the live state.

## What

### LibvirtComputeProvider

`Plexor.NodeAgent.Compute.LibvirtComputeProvider` —
the libvirt implementation of `IComputeProvider`:

- `ProviderId = "libvirt"`.
- `StartAsync(spec, ct)` —
  - Builds a `virt-install` command from
    `ComputeWorkloadSpec` (`--vcpus`, `--ram`,
    `--disk size=...`, `--import` for raw images,
    `--os-variant` from the image metadata).
  - Runs `virt-install --name <vm-name> --uuid
    <generated-uuid> ...` via the shell-out helper.
  - Returns a `ComputeWorkloadHandle` with
    `LocalId = <libvirt-uuid>` and `ProviderId =
    "libvirt"`.
  - Times out at 5 minutes; surfaces stderr on failure.
- `StopAsync(handle, ct)` — `virsh shutdown
  <uuid>` (ACPI shutdown; falls back to `virsh destroy`
  after 30s if the ACPI doesn't take).
- `GetStateAsync(handle, ct)` — `virsh dominfo <uuid>`,
  parses the `State:` line, maps to
  `ComputeWorkloadState` (`running` / `shut off` /
  `paused` / `crashed`).
- `DeleteAsync(handle, ct)` — `virsh undefine <uuid>
  --nvram` (NVRAM cleanup for UEFI guests) + `virsh
  vol-delete` for the backing storage if the volume was
  provisioned by Plexor.

The provider is `internal sealed`; the seam stays
`public` (the interface).

### Shell-out helper

`Plexor.NodeAgent.Compute.LibvirtShell` —
`internal static class` wrapping every `virsh` /
`virt-install` call:

```csharp
internal static class LibvirtShell
{
    public static Task<LibvirtCommandResult> RunAsync(
        string[] args,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
```

`LibvirtCommandResult` carries `(ExitCode, Stdout,
Stderr)`. The shell helper:

- Uses `Process.Start` with `UseShellExecute = false`
  (per `cross-platform.md` §8 — no shell interpolation).
- Kills the process tree on cancellation.
- Surfaces `stderr` on non-zero exit (no truncation; full
  stderr for debugging).
- Logs every invocation at `Debug` with the full arg
  list (no secrets — `virt-install` arguments don't
  carry secrets).

### Conditional DI registration

The NodeAgent's `Program.cs` reads
`[Clusters:Compute:Libvirt]` from configuration:

```toml
[clusters.compute]
libvirt_uri = "qemu:///system"
default_pool = "default"
base_image_dir = "/var/lib/libvirt/images"
```

If the section is present, `LibvirtComputeProvider` is
registered; otherwise the NoOp default stays. The DI
override happens in the NodeAgent's installer extension
(`Plexor.NodeAgent/Installers/ComputeInstaller.cs`).

### Configuration validation

`LibvirtComputeOptions`
(`Plexor.NodeAgent.Configuration.LibvirtComputeOptions`):

- `LibvirtUri : string` (default `"qemu:///system"`).
- `DefaultPool : string` (default `"default"`).
- `BaseImageDir : string` (default `"/var/lib/libvirt/images"`).
- `StartTimeoutSeconds : int` (range 60–1800, default
  300).
- `StopGracePeriodSeconds : int` (range 5–120, default
  30).

`ValidateDataAnnotations().ValidateOnStart()` is wired
in the installer — a missing `LibvirtUri` fails at startup,
not on the first workload start.

### Base image lifecycle

`Plexor.NodeAgent` ships a base-image downloader that
fetches a Plexor-published image set on first use:

```csharp
internal sealed class BaseImageProvisioner
{
    public Task<LocalImageHandle> EnsureBaseImageAsync(
        string imageRef, CancellationToken ct);
}
```

The provisioner checks the local pool for a volume with
the matching checksum; if missing, downloads from the
configured registry (`[Clusters:Compute:ImageRegistry]`)
and creates the volume. The first workload that references
a new image pays the download cost; subsequent workloads
reuse the volume.

## Impact

- **`clusters` capability** — additive delta:
  - `LibvirtComputeProvider` requirement (this change).
  - `IComputeProvider` implementation registration
    semantics.
  - `LibvirtComputeOptions` config.
  - `BaseImageProvisioner` requirement.
  See `openspec/changes/libvirt-provider/specs/clusters/spec.md`.
- **`Plexor.NodeAgent`** — adds the `Compute` folder
  with the provider, the shell helper, and the base
  image provisioner. `ComputeInstaller.cs` is the
  conditional registration entry point.

## Out of scope

- **LXC provider** — `Plexor.NodeAgent` already ships a
  `LibvirtLxcProvider` (Phase D Tier 3.5); migrating it
  to `IComputeProvider` is Phase 5+.
- **QEMU direct (without libvirt)** — Phase 5+.
- **k3s provider** — Phase 5+.
- **Image registry authentication** — the default
  registry is open; authenticated pulls land with the
  `ImageRegistry` provider abstraction.
- **Multi-host libvirt pools** — v0.1 uses the local
  pool on the NodeAgent host. Distributed pools
  (Ceph RBD, NetFS) land with the storage module's
  Phase 5+ provider.
- **Live migration** — Phase 5+.
