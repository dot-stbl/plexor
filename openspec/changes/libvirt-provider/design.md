# Design: libvirt-provider

Technical decisions for the libvirt compute provider.

## Why shell-out, not a libvirt .NET binding

Two options for talking to libvirt from .NET:

1. **Native shell-out via `Process.Start`** to `virsh` /
   `virt-install`. Chosen.
2. **A .NET libvirt binding** (`Libvirt.NET`,
   `libvirt-csharp`). Considered, rejected.

Reasons:

- **`virsh` is the canonical surface.** Every libvirt
  feature is reachable via `virsh`; the .NET bindings
  lag (the Libvirt.NET project is mostly stale).
- **Operator-facing tooling matches.** When a workload
  misbehaves, the operator SSHes into the host and runs
  `virsh dominfo <uuid>` — the same command the provider
  uses. No impedance mismatch.
- **Process tree management is easier.** `Process.Start`
  + a child-kill helper is two screens of code; the
  P/Invoke surface for `virsh` is an order of magnitude
  more work.
- **No native dependency at build time.** A P/Invoke
  binding requires `libvirt` headers at build + the
  shared library at runtime; shell-out only requires
  `virsh` on the operator's `PATH`.

The shell-out helper (`LibvirtShell`) centralizes every
process-management concern: cancellation, timeout, stderr
capture, exit-code propagation.

## virsh vs virt-install

`virt-install` is the canonical "create a guest" tool;
`virsh` handles everything else (lifecycle, info, undefine).
The split matches the surface:

- **Create** → `virt-install --name X --uuid Y ...`.
- **State transitions** (shutdown, start, pause) → `virsh`.
- **Info** (state, IP, console) → `virsh dominfo`.
- **Delete** → `virsh undefine --nvram` + `virsh
  vol-delete`.

`virt-install` internally calls `virsh create` under the
hood — using it directly avoids duplicating the
XML-generation logic.

## UUID generation — Plexor owns the UUID, libvirt echoes it

`virt-install --uuid <UUID>` accepts a UUID. The provider
generates a UUID v7 in C# and passes it through; `virsh
domuuid` returns the same value. The advantage: Plexor's
`Workload.LocalId` and the libvirt domain UUID are the
same string — no translation table.

A naive `virt-install` without `--uuid` lets libvirt mint
its own UUID; the provider would then have to read it back
via `virsh domuuid` to populate `LocalId`. The `--uuid`
flag avoids the round-trip.

## Graceful shutdown vs forced destroy

`virsh destroy` is the immediate equivalent of pulling the
power cord — the guest's filesystem is at risk of
corruption. The provider uses a two-phase shutdown:

1. `virsh shutdown <uuid>` (ACPI shutdown; clean filesystem
   flush, takes 5–30 seconds for a Linux guest).
2. If the guest is still running after
   `StopGracePeriodSeconds` (default 30s), `virsh destroy`
   is invoked.

The grace period is configurable via
`LibvirtComputeOptions.StopGracePeriodSeconds` (range
5–120). An operator who knows their workload is ACPI-broken
can shorten the grace; the default of 30s is the right
balance for most Linux guests.

## NVRAM cleanup for UEFI guests

`virsh undefine <uuid>` alone leaks NVRAM storage for
UEFI guests. The provider always passes `--nvram` to
`virsh undefine` to ensure NVRAM is deleted alongside the
domain definition. A failed undefine (e.g. NVRAM file
locked by a stuck qemu) is logged + retried on the next
delete; the workload row is deleted regardless (the
libvirt domain is best-effort cleanup).

## Base image provisioner — Plexor publishes images, agents cache

The `BaseImageProvisioner` checks the local libvirt pool
for a volume with the matching SHA-256 checksum. The
checksum is the Plexor-published image's contract; the
provider doesn't compare contents, it compares checksums.

On a miss, the provisioner downloads the image from the
configured registry
(`[Clusters:Compute:ImageRegistry]`), verifies the
checksum, and creates a libvirt volume from the file. The
provisioner caches the local volume handle; subsequent
workloads that reference the same image reuse the volume.

The download path uses `IHttpClientFactory` with the
default resilience pipeline (per
`http-resilience-refit.md`); the download is a one-shot
GET with a 5-minute timeout. Image registry authentication
(Phase 5+) plugs in via the bearer handler.

## Configuration — TOML, not env

`[Clusters:Compute:Libvirt]` is a TOML section in
`/etc/plexor/install.yaml` (the install-time config), not
an env var. The reason: the libvirt URI + base image dir
are operator decisions that should be visible in the
install manifest; env vars hide them.

```toml
[clusters.compute]
libvirt_uri = "qemu:///system"
default_pool = "default"
base_image_dir = "/var/lib/libvirt/images"
start_timeout_seconds = 300
stop_grace_period_seconds = 30
```

`ValidateOnStart()` fails the NodeAgent boot if the section
is malformed — a missing `libvirt_uri` doesn't silently
fall back to the default; the operator must opt in
explicitly.

## DI override — opt-in per host

The NodeAgent registers `NoOpComputeProvider` by default.
The libvirt registration replaces it only when the
`[Clusters:Compute:Libvirt]` section is present in the
config:

```csharp
public static class ComputeInstaller
{
    public static IServiceCollection AddComputeProvider(
        this IServiceCollection services, IConfiguration config)
    {
        if (config.GetSection(LibvirtComputeOptions.SectionName).Exists())
        {
            services.AddOptions<LibvirtComputeOptions>()
                .Bind(config.GetSection(LibvirtComputeOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<IComputeProvider, LibvirtComputeProvider>();
            services.AddSingleton<BaseImageProvisioner>();
        }
        else
        {
            services.AddSingleton<IComputeProvider, NoOpComputeProvider>();
        }
        return services;
    }
}
```

A self-hosted deploy without libvirt gets the NoOp
provider for free; a deploy with libvirt installed gets
the real provider by adding the TOML section.

## Open questions deferred

- **LXC provider migration.** `LibvirtLxcProvider`
  already exists in `Plexor.NodeAgent`; migrating it to
  `IComputeProvider` is Phase 5+.
- **Image registry authentication.** The default registry
  is open; authenticated pulls land with the
  `ImageRegistry` provider abstraction.
- **Multi-host libvirt pools.** v0.1 uses the local pool
  on the NodeAgent host. Distributed pools (Ceph RBD,
  NetFS) land with the storage module's Phase 5+
  provider.
- **Live migration.** Phase 5+.
- **GPU passthrough.** Phase 5+.
