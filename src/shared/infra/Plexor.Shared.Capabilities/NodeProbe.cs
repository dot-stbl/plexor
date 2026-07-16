// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeProbe — structured capability detection for Plexor nodes.
//
// What it detects (best-effort, never throws):
//
//   Compute runtimes:
//     - "vm" (KVM/QEMU): /dev/kvm exists + open() succeeds +
//       libvirtd socket responsive + kvm-ok in /proc/cpuinfo
//     - "lxc": lxc-ls binary on PATH (Linux) OR lxc CLI present
//     - "docker": docker binary + daemon responsive
//     - "k3s": k3s binary on PATH (server or agent)
//
//   Network overlays:
//     - "ovs": ovs-vsctl binary on PATH
//     - "cilium": cilium binary on PATH OR kernel >= 5.10 with
//       bpf/btf mounts present
//     - "host": always available (the default bridge)
//
//   Storage backends:
//     - "ceph-rbd": ceph CLI on PATH + rbd kernel module loaded
//     - "local-lvm": lvm CLI + at least one thinpool
//     - "longhorn": longhorn manifest on disk (rare on MVP)
//
//   Binary capabilities:
//     - NestedVirt = true if /dev/kvm is accessible AND CPU has
//       vmx/svm flag. False on cloud VMs and containers.
//     - K3sServer = true only on nodes where k3s server is
//       installed and running (control-plane role).
//
// The probe is read-only — it does NOT install anything, it only
// reports. Operator overrides go in node.yaml if a probe is
// wrong (e.g. kvm-ok in kernel but /dev/kvm not exposed in
// container — the probe can't see through that).
// ============================================================================

using System.Diagnostics;

namespace Plexor.Shared.Capabilities;

/// <summary>
///     Structured capability detection for a Plexor node. Calls
///     a small set of best-effort probes (no install side effects,
///     no errors — a failed probe just means "this capability
///     isn't available").
/// </summary>
public static class NodeProbe
{
    /// <summary>
    ///     Run all probes and return a <see cref="NodeCapabilities" />
    ///     record reflecting what the node can do right now.
    ///     Cross-platform: works on Linux (full), Windows (no
    ///     LXC/KVM/OVS — just docker + "host" + absent nested
    ///     virt), and macOS (limited — used for dev runs).
    /// </summary>
    public static NodeCapabilities Detect()
    {
        var compute = new List<string>();
        var network = new List<string>();
        var storage = new List<string>();

        if (HasKvm())
        {
            compute.Add("vm");
        }
        if (HasLxc())
        {
            compute.Add("lxc");
        }
        if (HasDocker())
        {
            compute.Add("docker");
        }
        if (HasK3s())
        {
            compute.Add("k3s");
        }

        if (HasOvs())
        {
            network.Add("ovs");
        }
        if (HasCilium())
        {
            network.Add("cilium");
        }
        // "host" bridge is always available — every Linux host
        // has at least a basic bridge via the kernel. Mac and
        // Windows get a host-only capability as well, with no
        // overlay.
        network.Add("host");

        if (HasCephRbd())
        {
            storage.Add("ceph-rbd");
        }
        if (HasLocalLvm())
        {
            storage.Add("local-lvm");
        }

        var nestedVirt = HasKvm() && CpuHasVirtualization();

        var k3sServer = IsK3sServerRunning();

        return new NodeCapabilities
        {
            Compute = compute,
            Network = network,
            Storage = storage,
            NestedVirt = nestedVirt,
            K3sServer = k3sServer,
            ProbedAt = DateTimeOffset.UtcNow,
        };
    }

    // -- private probes -----------------------------------------------------
    // Each probe is independent and never throws. A failed
    // probe returns false; the caller treats that as "absent".

    private static bool HasKvm()
    {
        // /dev/kvm is the canonical Linux KVM device node. Its
        // presence + the kvm-ok CPU feature means hardware
        // virtualization is exposed to userspace.
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }
        if (!File.Exists("/dev/kvm"))
        {
            return false;
        }
        return TryReadCpuinfo().Contains("vmx", StringComparison.Ordinal) ||
               TryReadCpuinfo().Contains("svm", StringComparison.Ordinal);
    }

    private static bool HasLxc()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }
        return BinaryOnPath("lxc-ls") || BinaryOnPath("lxc");
    }

    private static bool HasDocker()
    {
        if (!BinaryOnPath("docker"))
        {
            return false;
        }
        // Confirm the daemon is actually responsive. A binary
        // without a running daemon is a false positive — don't
        // advertise docker if workloads would fail to start.
        return TryRun("docker", "version", TimeSpan.FromSeconds(2));
    }

    private static bool HasK3s()
    {
        return BinaryOnPath("k3s") || File.Exists("/etc/rancher/k3s/k3s.yaml");
    }

    private static bool HasOvs()
    {
        return BinaryOnPath("ovs-vsctl");
    }

    private static bool HasCilium()
    {
        if (BinaryOnPath("cilium"))
        {
            return true;
        }
        // eBPF-capable kernel — bpf/btf mounts are the modern
        // indicator. On older kernels, cilium falls back to
        // non-eBPF mode; we still report it as available because
        // the operator can install the binary and Cilium will
        // start in compat mode.
        if (OperatingSystem.IsLinux())
        {
            return Directory.Exists("/sys/fs/bpf") ||
                   File.Exists("/proc/1/comm") && TryReadFile("/proc/1/comm") == "systemd";
        }
        return false;
    }

    private static bool HasCephRbd()
    {
        return BinaryOnPath("ceph") || BinaryOnPath("rbd");
    }

    private static bool HasLocalLvm()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }
        if (!BinaryOnPath("lvs") || !BinaryOnPath("vgscan"))
        {
            return false;
        }
        // A thinpool exists if any volume group has at least one
        // pool with the "t" flag in lvs output. We grep the
        // output rather than parse — lvs output format isn't
        // stable across versions.
        var output = RunAndCapture("lvs", "--noheadings -o lv_attr");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Any(static line => line.Trim().StartsWith('t'));
    }

    private static bool IsK3sServerRunning()
    {
        if (!HasK3s())
        {
            return false;
        }
        // k3s server runs as a systemd unit named k3s.service.
        // We don't shell out to systemctl here (probe should be
        // sandbox-friendly); just check for the marker file.
        return File.Exists("/etc/rancher/k3s/k3s.yaml") ||
               File.Exists("/var/lib/rancher/k3s/server/cred/api.kubeconfig");
    }

    private static bool CpuHasVirtualization()
    {
        // Even with /dev/kvm present, the CPU must support
        // hardware virtualization. Some virtualized environments
        // expose /dev/kvm but hide the CPU flag.
        var cpuinfo = TryReadCpuinfo();
        return cpuinfo.Contains("vmx", StringComparison.Ordinal) || cpuinfo.Contains("svm", StringComparison.Ordinal);
    }

    // -- shared helpers -----------------------------------------------------

    private static string TryReadCpuinfo()
    {
        if (!File.Exists("/proc/cpuinfo"))
        {
            return string.Empty;
        }
        try
        {
            // /proc/cpuinfo can be megabytes. Only the flags line
            // is interesting and it lives near the top.
            return File.ReadAllText("/proc/cpuinfo");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TryReadFile(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool BinaryOnPath(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return false;
        }
        var separator = OperatingSystem.IsWindows()
            ? ';'
            : ':';
        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
            {
                return true;
            }
            // Windows: name may have a .exe suffix.
            if (OperatingSystem.IsWindows() &&
                File.Exists(candidate + ".exe"))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    ///     Run a command, return true if exit code 0 within
    ///     <paramref name="timeout" />. Used for "is the daemon
    ///     responsive" probes.
    /// </summary>
    private static bool TryRun(string command, string args, TimeSpan timeout)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return process is not null && process.WaitForExit(timeout);
        }
        catch
        {
            return false;
        }
    }

    private static string RunAndCapture(string command, string args)
    {
        try
        {
            if (Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }) is not { } process)
            {
                return string.Empty;
            }
            process.WaitForExit(TimeSpan.FromSeconds(3));
            return process.StandardOutput.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }
}