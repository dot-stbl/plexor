# Providers — каталог и SDK

Plexor отделяет **что делать** (resource contract) от **как делать** (provider
implementation). Каждый ресурс — `I<Resource>Provider`, реализация — NuGet
пакет, устанавливаемый через `plx provider install`.

## Provider SDK — `Plexor.Core.Providers`

```csharp
// Базовый контракт
public interface IProvider
{
    string Id { get; }
    string DisplayName { get; }
    ProviderInfo Info { get; }
    Task<HealthReport> HealthCheckAsync(CancellationToken ct);
    Task InitializeAsync(IProviderContext ctx, CancellationToken ct);
}

// Resource provider (базовый)
public interface IResourceProvider<TResource, TSpec>
{
    Task<TResource> CreateAsync(TSpec spec, CancellationToken ct);
    Task<TResource?> GetAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<TResource>> ListAsync(ResourceQuery query, CancellationToken ct);
    Task<TResource> UpdateAsync(string id, TSpec spec, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
}

// Compute-specific extensions
public interface IComputeProvider : IResourceProvider<VirtualMachine, VmSpec>
{
    Task MigrateAsync(string id, string targetNode, CancellationToken ct);
    Task<ConsoleHandle> OpenConsoleAsync(string id, CancellationToken ct);
    Task<SshKeyHandle> InjectSshKeyAsync(string id, string publicKey, CancellationToken ct);
}

[Flags]
public enum ComputeFeature
{
    None = 0,
    LiveMigration = 1 << 0,
    Snapshots = 1 << 1,
    Console = 1 << 2,
    CloudInit = 1 << 3,
    HotResizeCpu = 1 << 4,
    HotResizeMemory = 1 << 5,
    GpuSupport = 1 << 6,
}

public interface IStorageProvider : IProvider
{
    IComputeProvider? Compute { get; }   // optional — volume attach
    Task<VolumeId> CreateVolumeAsync(VolumeSpec spec, CancellationToken ct);
    Task AttachVolumeAsync(VolumeId v, VmId vm, CancellationToken ct);
    Task<SnapshotId> SnapshotAsync(VolumeId v, CancellationToken ct);
    Task<IReadOnlyList<BucketInfo>> ListBucketsAsync(CancellationToken ct);
    // S3-compatible operations delegated to mc / s3cmd
}

public interface INetworkProvider : IProvider
{
    Task<VpcId> CreateVpcAsync(VpcSpec spec, CancellationToken ct);
    Task<SubnetId> CreateSubnetAsync(SubnetSpec spec, CancellationToken ct);
    Task<SecurityGroupId> CreateSecurityGroupAsync(SgSpec spec, CancellationToken ct);
    Task<FloatingIpId> AllocateFloatingIpAsync(CancellationToken ct);
    Task AttachFloatingIpAsync(FloatingIpId ip, VmId vm, CancellationToken ct);
    Task<LoadBalancerId> CreateLoadBalancerAsync(LbSpec spec, CancellationToken ct);
    Task<DnsZoneId> CreateDnsZoneAsync(DnsSpec spec, CancellationToken ct);
}
```

### Provider metadata

```csharp
[Provider("kvm", Tier = ProviderTier.Production)]
[RequiresBinary("virsh")]
[RequiresBinary("qemu-img")]
[RequiresKernelModule("kvm")]
[RequiresService("libvirtd")]
[Supports(ComputeFeature.LiveMigration | ComputeFeature.Snapshots
        | ComputeFeature.Console | ComputeFeature.CloudInit
        | ComputeFeature.HotResizeCpu)]
public class KvmComputeProvider : IComputeProvider { ... }
```

### Provider Manifest — автодетект при установке

```yaml
# provider-manifest.yaml, лежит в корне NuGet package
apiVersion: plexor.dev/v1
kind: ProviderPackage
metadata:
  name: plexor-providers-compute-kvm
  version: 0.1.0
  description: "KVM/QEMU provider for Plexor"
spec:
  provides:
    compute:
      class: Plexor.Providers.Compute.Kvm.KvmComputeProvider
      tier: production
      capabilities: [live-migration, snapshots, console, cloud-init, hot-resize]
  requires:
    binaries: [virsh, qemu-img]
    kernel-modules: [kvm, vhost_net]
    services: [libvirtd]
  install:
    - name: install-kvm
      type: apt-package
      packages: [qemu-kvm, libvirt-clients, libvirt-daemon-system]
```

## Каталог провайдеров

### Compute (VMs, microVMs, containers, bare metal)

| Provider ID | Primitive | Tier | Capabilities | Статус |
|---|---|---|---|---|
| `kvm` | vm-heavy | production | LiveMig, Snap, Console, CloudInit, HotResize | ✅ MVP |
| `lxd` | system-container | production | Snap, Console, CloudInit, HotResize, Cluster | ✅ MVP |
| `pod` | app-container | development | Snap, fast boot, high density | ✅ MVP (dev only) |
| `firecracker` | vm-micro | production | Console, fast-boot, high-density | Phase 2 |
| `vmware` | vm-heavy | production | LiveMig, Snap, Console, CloudInit, HotResize | Phase 2 (enterprise) |
| `hyperv` | vm-heavy | production | LiveMig, Snap, Console, CloudInit, HotResize | Phase 2 (enterprise) |
| `maas` | bare-metal | production | Snap, OS-deploy, IPMI | Phase 3 |
| `tinkerbell` | bare-metal | production | Snap, OS-deploy, workflows, firmware | Phase 3 |
| `ironic` | bare-metal | production | Snap, OS-deploy, RAID-config | Phase 3 |

### Storage (block, object, snapshots)

| Provider ID | Layer | Tier | Capabilities | Статус |
|---|---|---|---|---|
| `ceph` | block + object + s3 | production | Replication (3x), Snap, S3-compatible | ✅ MVP |
| `minio` | object | production | S3-compatible, single-node | ✅ MVP |
| `local-lvm` | block | production | Snap (thin) | ✅ MVP (fallback) |
| `zfs` | block + snapshots | production | Snap, Replication, thin | Phase 2 |
| `longhorn` | block | production | Snap, Replication, distributed | Phase 2 |

### Network (overlay, SDN, LB, DNS)

| Provider ID | Layer | Tier | Capabilities | Статус |
|---|---|---|---|---|
| `ovs` | overlay-network | production | VXLAN, VLAN, BGP, OVN-control | ✅ MVP |
| `cilium` | overlay-network | production | eBPF, BGP, L7-policy | ✅ MVP |
| `host` | bridge-only | development | No isolation | ✅ MVP (dev only) |
| `haproxy` | load-balancer | production | L4 + L7 (basic), SSL offload | ✅ MVP |
| `metallb` | floating-ip + lb | production | L2/BGP announce | Phase 2 |
| `powerdns` | dns | production | API, dynamic updates | Phase 3 |
| `nsx` | sdn | production | Full SDN (enterprise) | out of scope |

### Identity

| Provider ID | Tier | Notes |
|---|---|---|
| `local` | development | built-in users table, dev only |
| `keycloak` | production | OAuth2/OIDC/OAuth2/SAML/LDAP-federation |
| `authentik` | production | OAuth2/OIDC, simpler than Keycloak |
| `ldap` | production | Direct LDAP bind (no OAuth layer) |

### OS (host OS for bare metal)

| Provider ID | Tier | Notes |
|---|---|---|
| `ubuntu` | production | PXE + preseed + cloud-init |
| `talos` | production | Immutable, API-managed, K8s-first |
| `flatcar` | production | Immutable, Gentoo-based |

### Orchestrator (managed Kubernetes)

| Provider ID | Tier | Notes |
|---|---|---|
| `k3s` | production | Single binary, light, edge-friendly |
| `k0s` | production | Zero-dep binary |
| `nomad` | production | Mixed workloads (containers + VMs + binaries) |

## Как добавить новый provider

### Шаг 1: Установить SDK

```bash
dotnet new classlib -n Plexor.Providers.Compute.Firecracker -o src/providers/Plexor.Providers.Compute.Firecracker --framework net10.0
```

### Шаг 2: Реализовать интерфейс

```csharp
[Provider("firecracker", Tier = ProviderTier.Production)]
[RequiresBinary("firecracker")]
[RequiresKernelModule("kvm")]
[Supports(ComputeFeature.Console | ComputeFeature.HotResize | ComputeFeature.FastBoot)]
public sealed class FirecrackerComputeProvider : IComputeProvider
{
    public string Id => "firecracker";
    public string DisplayName => "Firecracker microVM";
    public ProviderInfo Info { get; } = FirecrackerInfo.Create();

    public async Task<VmId> CreateAsync(VmSpec spec, CancellationToken ct)
    {
        // 1. Generate firecracker config (JSON on disk)
        var config = new FirecrackerConfigBuilder(spec).Build();
        var configPath = $"/var/lib/plexor/vms/{spec.Name}.json";
        await File.WriteAllTextAsync(configPath, config.ToJson(), ct);

        // 2. Write rootfs + kernel refs to paths
        // 3. Launch jailer + firecracker
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/jailer",
            Arguments = $"--id {spec.Name} --exec {configPath}"
        });
        // 4. Return VmId, monitor via vsock
        return new VmId(spec.Name);
    }
    // ... other interface methods
}
```

### Шаг 3: Provider manifest (provider-manifest.yaml)

```yaml
apiVersion: plexor.dev/v1
kind: ProviderPackage
metadata:
  name: plexor-providers-compute-firecracker
  version: 0.1.0
spec:
  provides:
    compute:
      class: Plexor.Providers.Compute.Firecracker.FirecrackerComputeProvider
      tier: production
      capabilities: [console, hot-resize, fast-boot]
  requires:
    binaries: [firecracker, jailer]
    kernel-modules: [kvm]
    config:
      - /dev/kvm exists and is rw
```

### Шаг 4: Опубликовать как NuGet

```bash
dotnet pack src/providers/Plexor.Providers.Compute.Firecracker -c Release
dotnet nuget push bin/Release/Plexor.Providers.Compute.Firecracker.0.1.0.nupkg -s nuget.org
```

### Шаг 5: Установка через plx

```bash
# Онлайн-установка с NuGet.org
plx provider install Plexor.Providers.Compute.Firecracker --version 0.1.0

# Или из локального файла
plx provider install ./plexor-providers-compute-firecracker-0.1.0.nupkg

# Air-gapped
plx provider install --offline ./providers/
```

После install:
- DLL кладётся в `/var/lib/plexor/providers/`
- `plx provider list` показывает firecracker как доступный
- Control plane автоматически подхватывает через DI scan

## Capability negotiation

Когда control plane решает «какой провайдер использовать»:

```
User request: VM с live-migration
  ↓
resolver queries all available IComputeProvider
  ↓
each provider's [Supports(...)] attribute matches against requested features
  ↓
scored:
  - tier  (production > development)
  - capability match (LiveMigration ✓)
  - environment availability (KVM есть, firecracker нет)
  - user override (если в plexor.yaml указан provider=KVM)
  ↓
winner = best match
```

## Provider discovery — как autodetect работает

`plx init` запускает пять стадий (см. operations/install.md):
1. **Discovery** — `SystemProbe` проверяет что есть в системе
2. **Provider probes** — каждый plugin-provider запускает свой `HealthCheck`
3. **Resolver** — выбирает лучшие провайдеры по feature/tier/availability
4. **Plan** — генерирует последовательность шагов установки
5. **Apply** — выполняет идемпотентно с возможностью прерывания

## См. также

- [architecture.md](architecture.md) — общая архитектура
- [operations/install.md](operations/install.md) — install flow
- [yandex-cloud-parity.md](yandex-cloud-parity.md) — какие сервисы уже покрыты