// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LocalDirStorage unit tests — exercise the surface contract
// (idempotent delete, cross-backend handle rejection) without
// shelling out to qemu-img. The actual qemu-img invocation is
// covered by integration tests against a real qemu-img binary
// on the host filesystem.
// ==========================================================================

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.NodeAgent.Providers.Storage;
using Plexor.Shared.Compute;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Storage;

public sealed class LocalDirStorageShould
{
    [Fact(DisplayName = "Given a volume with another backend's handle, when DeleteAsync, then throws ArgumentException")]
    public async Task DeleteRejectsForeignHandle()
    {
        var sut = NewStorage(out _);
        var foreignHandle = new VolumeHandle(
            BackendName: "ceph-rbd",
            Reference: "pool1/vm-1");

        await Should.ThrowAsync<ArgumentException>(
            () => sut.DeleteAsync(foreignHandle, CancellationToken.None));
    }

    [Fact(DisplayName = "Given a missing volume, when DeleteAsync, then no-op (idempotent)")]
    public async Task DeleteIsIdempotent()
    {
        var root = TempDir();
        var sut = NewStorage(root, out _);

        var handle = new VolumeHandle(LocalDirStorage.BackendName, Path.Combine(root, "missing.qcow2"));
        await sut.DeleteAsync(handle, CancellationToken.None);
    }

    [Fact(DisplayName = "Given the canonical BackendName, then it equals 'local-dir'")]
    public void BackendNameIsStable()
    {
        LocalDirStorage.BackendName.ShouldBe("local-dir");
    }

    /// <summary>
    ///     The CreateAsync → qemu-img round-trip is environment-
    ///     dependent (requires qemu-img on PATH). Skipped in unit
    ///     context; the integration suite covers it.
    /// </summary>
    [Fact(DisplayName = "Given a known imageRef, when CreateAsync, then handle points at {root}/{name}.qcow2",
          Skip = "Requires qemu-img on PATH — covered by integration test suite.")]
    public async Task CreateReturnsHandlePointingAtQcow2()
    {
        var root = TempDir();
        var sut = NewStorage(root, out _);
        var spec = new VolumeSpec(
            Name: "vm-test",
            SizeBytes: 10L * 1024L * 1024L * 1024L,
            BaseImageRef: null,
            Format: VolumeFormat.Qcow2);

        var handle = await sut.CreateAsync(spec, CancellationToken.None);
        handle.BackendName.ShouldBe(LocalDirStorage.BackendName);
        handle.Reference.ShouldEndWith(".qcow2");
        handle.Reference.ShouldContain("vm-test");
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"plexor-na-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static LocalDirStorage NewStorage(string root, out IImageRegistry registry)
    {
        registry = Substitute.For<IImageRegistry>();
        return new LocalDirStorage(root, registry, NullLogger<LocalDirStorage>.Instance);
    }

    private static LocalDirStorage NewStorage(out IImageRegistry registry)
    {
        return NewStorage(TempDir(), out registry);
    }
}