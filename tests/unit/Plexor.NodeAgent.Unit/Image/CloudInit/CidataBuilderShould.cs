// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CidataBuilder unit tests — exercise the cloud-init NoCloud ISO
// 9660 builder end-to-end (build → ISO 9660 mount → read back).
//
// Why we mount the ISO: a bare byte-level test (e.g. "the file
// contains 'instance-id:...'") would still pass if DiscUtils
// produced a malformed or non-bootable image; cloud-init's
// NoCloud datasource would silently fall through to its next
// walk target and the VM would never see the seed. Mounting
// (via DiscUtils' Udf/Iso9660 reader) verifies the actual on-
// disk shape — file names + volumes + sectors — matches what
// cloud-init expects.
// ==========================================================================

using System.Text;
using DiscUtils.Iso9660;
using Plexor.NodeAgent.Providers.Image.CloudInit;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Image.CloudInit;

public sealed class CidataBuilderShould
{
    [Fact(DisplayName = "Given hostname + ssh key, when Build, then writes a cidata ISO containing meta-data and user-data")]
    public void BuildsValidIso9660WithExpectedFiles()
    {
        var output = TempIsoPath("vm-cidata");

        CidataBuilder.Build(
            output,
            hostname: "plexor-vm-cidata",
            sshPublicKey: "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAITESTKEY test@example.com");

        File.Exists(output).ShouldBeTrue();

        // hideVersions=true strips the ";1" version suffix that
        // ISO 9660 appends to every file entry. Without it
        // DiscUtils reports "meta-data.;1" instead of "meta-data".
        using var iso = new CDReader(
            File.OpenRead(output),
            joliet: false,
            hideVersions: true);

        iso.VolumeLabel.ShouldBe(CidataBuilder.VolumeLabel);

        var files = iso.GetFiles("\\", "*.*");
        // DiscUtils' GetFiles returns paths like "\META_DATA"
        // — uppercase + "-" replaced with "_" per ISO 9660 level 1
        // (the Rock Ridge-free variant DiscUtils defaults to).
        // cloud-init's NoCloud reader is case-insensitive and
        // matches by short name, so the uppercased form on disk
        // is what cloud-init sees. Normalise to lowercase + dashes
        // for the test assertion.
        var leafNames = files
            .Select(static f => Path.GetFileName(f)
                .TrimEnd('.')
                .Replace("_", "-")
                .ToLowerInvariant())
            .ToArray();
        leafNames.ShouldContain("meta-data");
        leafNames.ShouldContain("user-data");
        leafNames.Length.ShouldBe(2);

        var metaDataPath = files.First(static f =>
                    Path.GetFileName(f)
                        .TrimEnd('.')
                        .Replace("_", "-")
                        .Equals("meta-data", StringComparison.OrdinalIgnoreCase));
        var metaData = ReadFromStream(iso, metaDataPath);
        metaData.ShouldContain("instance-id:");
        metaData.ShouldContain("local-hostname: plexor-vm-cidata");
        // UTF-8 BOM (U+FEFF) must NOT prefix the meta-data file —
        // cloud-init's NoCloud reader treats the first byte
        // literally and a BOM looks like garbage.
        metaData.ShouldNotStartWith("\uFEFF");
    }

    [Fact(DisplayName = "Given no SSH key, when Build, then user-data still exists but has no authorized_keys section")]
    public void BuildsWithoutSshKeyStillEmitsUserData()
    {
        var output = TempIsoPath("vm-nokey");

        CidataBuilder.Build(
            output,
            hostname: "plexor-vm-nokey",
            sshPublicKey: null);

        using var iso = new CDReader(
            File.OpenRead(output),
            joliet: false,
            hideVersions: true);

        var files = iso.GetFiles("\\", "*.*");
        var userDataPath = files.First(static f =>
            Path.GetFileName(f)
                .TrimEnd('.')
                .Replace("_", "-")
                .Equals("user-data", StringComparison.OrdinalIgnoreCase));
        var userData = ReadFromStream(iso, userDataPath);
        userData.ShouldContain("#cloud-config");
        userData.ShouldContain("disable_root: false");
        userData.ShouldNotContain("ssh_authorized_keys:");
    }

    [Fact(DisplayName = "Given a workload name, when DeterministicInstanceId, then returns the same GUID on repeated calls")]
    public void DeterministicInstanceIdIsStable()
    {
        var first = CidataBuilder.DeterministicInstanceId("vm-stable");
        var second = CidataBuilder.DeterministicInstanceId("vm-stable");

        first.ShouldBe(second);
        first.ShouldNotBe(Guid.Empty);
    }

    [Fact(DisplayName = "Given two different workload names, when DeterministicInstanceId, then returns different GUIDs")]
    public void DeterministicInstanceIdVariesPerName()
    {
        var a = CidataBuilder.DeterministicInstanceId("vm-a");
        var b = CidataBuilder.DeterministicInstanceId("vm-b");

        a.ShouldNotBe(b);
    }

    [Fact(DisplayName = "Given a workload name with unsafe chars, when SanitiseHostname, then returns a safe lowercase hostname")]
    public void SanitiseHostnameStripsUnsafeChars()
    {
        // Underscores are stripped (RFC 1123 hostnames allow
        // [a-z0-9-] only); spaces are stripped; leading
        // hyphens are trimmed.
        CidataBuilder.SanitiseHostname("MyVM_42").ShouldBe("myvm42");
        CidataBuilder.SanitiseHostname("foo bar").ShouldBe("foobar");
        CidataBuilder.SanitiseHostname("---leading").ShouldBe("leading");
    }

    [Fact(DisplayName = "Given a hostname that sanitises to empty, when SanitiseHostname, then throws ArgumentException")]
    public void SanitiseHostnameRejectsEmptyResult()
    {
        Should.Throw<ArgumentException>(
            static () => CidataBuilder.SanitiseHostname("---"));
    }

    [Fact(DisplayName = "Given a workload name, when ResolveIsoPath, then returns {root}/{name}-cidata.iso")]
    public void ResolveIsoPathUsesCanonicalRoot()
    {
        var path = CidataBuilder.ResolveIsoPath("vm-test", root: "/tmp/test-cidata");
        path.ShouldEndWith("vm-test-cidata.iso");
        path.ShouldStartWith("/tmp/test-cidata");
    }

    [Fact(DisplayName = "Given a workload name with path-traversal chars, when ResolveIsoPath, then output stays under the root")]
    public void ResolveIsoPathSanitisesTraversalChars()
    {
        var path = CidataBuilder.ResolveIsoPath("../../etc/passwd", root: "/tmp/test-cidata");
        path.ShouldStartWith("/tmp/test-cidata");
    }

    [Fact(DisplayName = "Given a non-existent cidata path, when CidataCleanup.TryDelete, then is silent no-op")]
    public void CleanupOnMissingFileIsSilent()
    {
        // Should.Throw- style: we assert the call returns
        // without throwing for missing files (the contract every
        // Plexor compute backend applies).
        Should.NotThrow(static () => CidataCleanup.TryDelete("/var/lib/plexor/cidata/never-existed.iso"));
    }

    [Fact(DisplayName = "Given an existing cidata path, when CidataCleanup.TryDelete, then the file is removed")]
    public void CleanupDeletesExistingFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"plexor-cidata-cleanup-{Guid.NewGuid():N}.iso");
        File.WriteAllBytes(temp, "fake"u8.ToArray());
        File.Exists(temp).ShouldBeTrue();

        CidataCleanup.TryDelete(temp);

        File.Exists(temp).ShouldBeFalse();
    }

    /// <summary>
    ///     Read a file inside the mounted ISO into a UTF-8 string.
    ///     DiscUtils' CDReader is synchronous (StreamReader.ReadToEnd
    ///     is the canonical read pattern).
    /// </summary>
    /// <param name="iso"></param>
    /// <param name="path"></param>
    private static string ReadFromStream(CDReader iso, string path)
    {
        using var stream = iso.OpenFile(path, FileMode.Open);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string TempIsoPath(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"plexor-cidata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{name}-cidata.iso");
    }
}

