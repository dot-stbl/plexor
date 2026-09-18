# Tasks: libvirt-provider

Numbered checklist.

## LIB.1 — LibvirtComputeProvider

- [x] `Plexor.NodeAgent.Compute.LibvirtComputeProvider` —
      `internal sealed`, implements `IComputeProvider`.
- [x] `StartAsync` builds + runs `virt-install` with the
      Plexor-spec-derived flags.
- [x] `StopAsync` runs `virsh shutdown` with grace-period
      fallback to `virsh destroy`.
- [x] `GetStateAsync` parses `virsh dominfo`.
- [x] `DeleteAsync` runs `virsh undefine --nvram` + volume
      cleanup.
- [x] `ProviderId = "libvirt"`.
- [x] Unit tests: `LibvirtShellShould` (4 cases — happy
      path, non-zero exit, cancellation, timeout).

## LIB.2 — LibvirtShell helper

- [x] `Plexor.NodeAgent.Compute.LibvirtShell` —
      `internal static class`.
- [x] `RunAsync(args, timeout, ct)` — `Process.Start` with
      `UseShellExecute = false`; kills process tree on
      cancellation.
- [x] Surfaces full `stderr` on non-zero exit.
- [x] Logs every invocation at `Debug` with the full arg
      list.

## LIB.3 — Configuration + validation

- [x] `LibvirtComputeOptions` class (sealed, init-only
      properties, `[Required]` + `[Range]` annotations).
- [x] `SectionName = "Clusters:Compute:Libvirt"` constant.
- [x] `ValidateDataAnnotations().ValidateOnStart()` wired
      in `ComputeInstaller`.
- [x] Unit tests: `LibvirtComputeOptionsValidatorShould`
      (3 cases).

## LIB.4 — Conditional DI registration

- [x] `ComputeInstaller` reads the config section. If
      present, registers
      `services.AddSingleton<IComputeProvider,
      LibvirtComputeProvider>()`; else leaves the
      NoOp default in place.
- [x] Unit tests: `ComputeInstallerShould` (4 cases —
      both branches + missing section + invalid config).

## LIB.5 — Base image provisioner

- [x] `BaseImageProvisioner` —
      `Plexor.NodeAgent.Compute.BaseImageProvisioner`.
- [x] `EnsureBaseImageAsync(imageRef, ct)` checks local
      pool for a matching checksum; downloads on miss.
- [x] Integration smoke test (Testcontainers-style with a
      real libvirt host) — out of scope for unit tests;
      manual smoke documented in
      `.agents/docs/operations/install.md`.

## LIB.6 — Merge

- [x] `merge libvirt-provider` commit lands on `main`.
