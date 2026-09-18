# Spec delta: clusters (libvirt-provider)

This file is the additive delta for the clusters capability
introduced by the change `openspec/changes/libvirt-provider/`.
Existing clusters Requirements are unchanged — only new
requirements are added.

When the change lands, this delta is promoted into
`openspec/specs/clusters/spec.md` under `## Requirements`,
and the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: LibvirtComputeProvider

The system SHALL ship `LibvirtComputeProvider`
(`Plexor.NodeAgent.Compute.LibvirtComputeProvider`) as the
libvirt implementation of `IComputeProvider`
(see `openspec/changes/fe-vm-lifecycle/specs/clusters/spec.md`
§"IComputeProvider seam").

`LibvirtComputeProvider` SHALL:

- `ProviderId = "libvirt"`.
- `StartAsync(spec, ct)` — build a `virt-install`
  command line from `ComputeWorkloadSpec`
  (`--name`, `--uuid`, `--vcpus`, `--ram`, `--disk
  size=`, `--import`, `--os-variant`), run it via the
  shell helper, and return a
  `ComputeWorkloadHandle` with `LocalId = <libvirt-uuid>`.
  Timeout: `LibvirtComputeOptions.StartTimeoutSeconds`
  (default 300, range 60–1800).
- `StopAsync(handle, ct)` — `virsh shutdown <uuid>` with
  grace-period fallback. After
  `LibvirtComputeOptions.StopGracePeriodSeconds`
  (default 30, range 5–120), the provider falls back to
  `virsh destroy <uuid>`.
- `GetStateAsync(handle, ct)` — `virsh dominfo <uuid>`,
  parses the `State:` line, maps to
  `ComputeWorkloadState` (`running` / `shut off` / `paused`
  / `crashed` / `pmsuspended`).
- `DeleteAsync(handle, ct)` — `virsh undefine <uuid>
  --nvram` (NVRAM cleanup for UEFI guests) + `virsh
  vol-delete` for the backing storage if the volume was
  provisioned by Plexor. A failed undefine is logged; the
  workload row is deleted regardless (best-effort
  cleanup).

### Requirement: LibvirtShell helper

The system SHALL ship `LibvirtShell`
(`Plexor.NodeAgent.Compute.LibvirtShell`) — an
`internal static class` that wraps every `virsh` /
`virt-install` invocation:

```csharp
public static Task<LibvirtCommandResult> RunAsync(
    string[] args, TimeSpan timeout, CancellationToken ct);
```

`LibvirtShell` SHALL:

- Use `Process.Start` with `UseShellExecute = false`
  (per `cross-platform.md` §8).
- Kill the process tree on cancellation.
- Surface full `stderr` on non-zero exit (no truncation).
- Log every invocation at `LogLevel.Debug` with the full
  arg list (no secrets — `virt-install` arguments don't
  carry secrets).

`LibvirtCommandResult` is a record
`(ExitCode : int, Stdout : string, Stderr : string)`.

### Requirement: LibvirtComputeOptions

The system SHALL expose `LibvirtComputeOptions`
(`Plexor.NodeAgent.Configuration.LibvirtComputeOptions`,
`SectionName = "Clusters:Compute:Libvirt"`):

- `LibvirtUri : string` (default `"qemu:///system"`).
- `DefaultPool : string` (default `"default"`).
- `BaseImageDir : string`
  (default `"/var/lib/libvirt/images"`).
- `StartTimeoutSeconds : int` (range 60–1800, default
  300).
- `StopGracePeriodSeconds : int` (range 5–120, default
  30).

`ComputeInstaller.AddComputeProvider` SHALL bind +
validate the options with
`ValidateDataAnnotations().ValidateOnStart()`. A missing
or malformed section fails the NodeAgent boot, not the
first workload start.

### Requirement: Conditional DI registration

The NodeAgent SHALL register `LibvirtComputeProvider` as
the `IComputeProvider` implementation ONLY when the
`[Clusters:Compute:Libvirt]` config section is present.
Otherwise the `NoOpComputeProvider` default (see
`fe-vm-lifecycle`) stays registered.

The conditional registration lives in
`Plexor.NodeAgent.Installers.ComputeInstaller.AddComputeProvider`:
the installer reads the section via
`config.GetSection(LibvirtComputeOptions.SectionName).Exists()`,
branches, and registers the appropriate provider.

### Requirement: Base image provisioner

The system SHALL ship `BaseImageProvisioner`
(`Plexor.NodeAgent.Compute.BaseImageProvisioner`) that
ensures a libvirt volume exists for a given image
reference:

```csharp
public Task<LocalImageHandle> EnsureBaseImageAsync(
    string imageRef, CancellationToken ct);
```

The provisioner SHALL:

1. Compute the SHA-256 of the published image.
2. Check the local libvirt pool for a volume with the
   matching checksum.
3. On a hit, return the existing volume handle.
4. On a miss, download the image from
   `[Clusters:Compute:ImageRegistry]` via the default
   `IHttpClientFactory` resilience pipeline, verify the
   checksum, and create a libvirt volume from the file.
5. Return the new volume handle.

The first workload that references a new image pays the
download cost; subsequent workloads reuse the volume.
The download path uses `IHttpClientFactory` with a
5-minute timeout. Authenticated pulls are Phase 5+.

### Requirement: Graceful shutdown vs forced destroy

The system SHALL prefer ACPI shutdown (`virsh shutdown`)
to forced destroy (`virsh destroy`). The provider runs
the ACPI shutdown first; if the guest is still running
after `StopGracePeriodSeconds`, the provider invokes
`virsh destroy` as a fallback.

The grace period is configurable per-host via
`LibvirtComputeOptions.StopGracePeriodSeconds`. An
operator who knows their workload is ACPI-broken can
shorten the grace (min 5s); an operator with slow-shutdown
guests can extend it (max 120s). The default of 30s is
the right balance for typical Linux guests.

### Requirement: NVRAM cleanup for UEFI guests

The system SHALL pass `--nvram` to `virsh undefine` so
NVRAM storage is deleted alongside the domain definition.
A failed undefine (e.g. NVRAM file locked by a stuck qemu)
is logged at `LogLevel.Warning` and the workload row is
deleted regardless (the libvirt domain is best-effort
cleanup; the workload row is the source of truth).

### Requirement: Workload.LocalId equals the libvirt UUID

The system SHALL mint a UUID v7 in C# and pass it via
`virt-install --uuid <UUID>`. The provider populates
`Workload.LocalId` with the same UUID — Plexor's row and
the libvirt domain are the same string, no translation
table. `virsh domuuid <name>` returns the same value if
verified post-create.

### Requirement: Migration order + DI registration order

The libvirt provider SHALL NOT modify any database schema
(no migration). The DI registration lives in the
NodeAgent's installer; it does NOT touch the Host
process. The Host process keeps the `IComputeProvider`
seam for future providers but does NOT register an
implementation (the NodeAgent is the side that talks to
the hypervisor).
