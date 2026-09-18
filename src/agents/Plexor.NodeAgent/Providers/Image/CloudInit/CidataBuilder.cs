// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CidataBuilder — file-static helper that produces a cloud-init
// NoCloud "cidata" ISO 9660 image. The image is attached as a
// read-only IDE CD-ROM to a KVM domain; cloud-init's NoCloud
// datasource walks every disk, finds the one with a
// "cidata" volume label, and reads /meta-data + /user-data
// from the seed.
//
// Wire format:
//   - meta-data : YAML (cloud-init requires this exact name)
//     {
//           instance-id: <uuid>,
//           local-hostname: <hostname>
//         }
//   - user-data : cloud-config YAML (or a shell script). We always
//     emit cloud-config with one module — write_files for the SSH
//     authorized_keys, plus disable_root: false so the operator can
//     SSH in as root.
//
// Why ISO 9660 and not a vfat or raw disk: cloud-init's NoCloud
// datasource is the only one that supports the cidata label out
// of the box across Ubuntu / Debian / RHEL / Alma / Fedora cloud
// images. Joliet extensions are intentionally OFF so the image
// stays small (Ubuntu cloud images can't read Joliet via the
// NoCloud datasource anyway — it walks by short-name only).
//
// DiscUtils.CDBuilder does the heavy lifting; we own the
// file-name + content contract.
// ==========================================================================

using System.Text;
using DiscUtils.Iso9660;

namespace Plexor.NodeAgent.Providers.Image.CloudInit;

/// <summary>
///     Build a cloud-init NoCloud cidata ISO. Pure-function
///     (no DI, no I/O outside the output stream); lives in its
///     own file per the class-decomposition rule.
/// </summary>
public static class CidataBuilder
{
    /// <summary>
    ///     The volume label cloud-init's NoCloud datasource matches
    ///     against when walking attached disks. DiscUtils uses this
    ///     string verbatim in the Primary Volume Descriptor; do NOT
    ///     localise it.
    /// </summary>
    public const string VolumeLabel = "cidata";

    /// <summary>
    ///     Build a cidata ISO that injects <paramref name="sshPublicKey" />
    ///     as the <c>root</c> user's authorized key. The ISO is
    ///     written to <paramref name="outputPath" />; the directory
    ///     is created if missing. The caller owns cleanup (delete
    ///     the ISO when the VM is deleted).
    /// </summary>
    /// <param name="outputPath">
    ///     Absolute path to the ISO file. The directory is
    ///     created if missing. Recommended convention:
    ///     <c>/var/lib/plexor/cidata/{vm-name}-cidata.iso</c>.
    /// </param>
    /// <param name="hostname">
    ///     Hostname set in /etc/hostname on first boot. Cloud-init
    ///     requires the field name <c>local-hostname</c> (hyphen,
    ///     not underscore) — see
    ///     https://cloudinit.readthedocs.io/en/latest/explanation/instancedata.html.
    /// </param>
    /// <param name="sshPublicKey">
    ///     OpenSSH public key content (single line). Embedded
    ///     verbatim into user-data's <c>ssh_authorized_keys</c>
    ///     list. Null / empty: cloud-init still gets the
    ///     meta-data + user-data seeds (without an authorized key)
    ///     — useful when the operator doesn't want SSH and
    ///     prefers to drive the VM out-of-band.
    /// </param>
    /// <param name="instanceId">
    ///     Stable per-VM identifier cloud-init uses to dedupe
    ///     re-runs. Defaults to a fresh <see cref="Guid.NewGuid()" />
    ///     when null — the first boot creates a unique-id, every
    ///     subsequent boot sees the same seed and treats it as a
    ///     no-op.
    /// </param>
    /// <param name="userDataScript">
    ///     Optional <c>#cloud-config</c> YAML to append to the
    ///     generated user-data. When null / empty, the generated
    ///     user-data carries only the SSH-key module.
    /// </param>
    public static void Build(
        string outputPath,
        string hostname,
        string? sshPublicKey,
        Guid? instanceId = null,
        string? userDataScript = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        var id = instanceId ?? Guid.NewGuid();

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var metaData = BuildMetaData(id, hostname);
        var userData = BuildUserData(sshPublicKey, userDataScript);

        using var output = File.Create(outputPath);
        var builder = new CDBuilder
        {
            VolumeIdentifier = VolumeLabel,
            UseJoliet = false
        };

        // DiscUtils streams the bytes out as it's called; the
        // input streams are fully drained by Build. We use
        // MemoryStream wrappers so the strings stay readable
        // (StreamWriter + UTF-8 without BOM is the cloud-init
        // convention).
        builder.AddFile("meta-data", ToStream(metaData));
        builder.AddFile("user-data", ToStream(userData));
        builder.Build(output);
    }

    /// <summary>
    ///     Build the cloud-init meta-data YAML. Cloud-init expects
    ///     the instance-id + local-hostname fields by these exact
    ///     names; renaming them causes cloud-init to skip the
    ///     NoCloud seed entirely.
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="hostname"></param>
    private static string BuildMetaData(Guid instanceId, string hostname)
    {
        var sb = new StringBuilder();
        sb.Append("instance-id: ").Append(instanceId.ToString()).Append('\n')
            .Append("local-hostname: ").Append(hostname).Append('\n');
        return sb.ToString();
    }

    /// <summary>
    ///     Build the cloud-config user-data YAML. When
    ///     <paramref name="sshPublicKey" /> is non-null, it's the
    ///     only authorized_keys entry under the root user (uid 0).
    ///     When <paramref name="userDataScript" /> is non-null,
    ///     it's appended as additional YAML (the caller is
    ///     responsible for it being a valid #cloud-config
    ///     fragment).
    /// </summary>
    /// <param name="sshPublicKey"></param>
    /// <param name="userDataScript"></param>
    private static string BuildUserData(string? sshPublicKey, string? userDataScript)
    {
        var sb = new StringBuilder();
        sb.Append("#cloud-config\n");
        sb.Append("disable_root: false\n");

        if (!string.IsNullOrWhiteSpace(sshPublicKey))
        {
            sb.Append("ssh_authorized_keys:\n");
            sb.Append("  - ").Append(sshPublicKey.Trim()).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(userDataScript))
        {
            sb.Append('\n');
            sb.Append(userDataScript.Trim()).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Wrap a string in a MemoryStream backed by UTF-8 bytes
    ///     (no BOM — cloud-init's NoCloud reader doesn't strip
    ///     the BOM and chokes on a UTF-8 preamble at the start of
    ///     meta-data).
    /// </summary>
    /// <param name="content"></param>
    private static MemoryStream ToStream(string content)
    {
        // UTF-8 without BOM — Encoding.UTF8 *prepends* a BOM by
        // default in .NET; use the explicit BOM-less constructor.
        return new MemoryStream(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));
    }

    /// <summary>
    ///     Sanitise a hostname into a cloud-init-safe value: ASCII
    ///     letters / digits / hyphens, no leading hyphen, ≤ 63
    ///     chars. Used by the provider before passing to
    ///     <see cref="Build" /> so a malformed spec doesn't poison
    ///     the cloud-init seed.
    /// </summary>
    /// <param name="raw"></param>
    /// <exception cref="ArgumentException"></exception>
    public static string SanitiseHostname(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var trimmed = raw.Length > 63 ? raw[..63] : raw;
        var buffer = new char[trimmed.Length];
        var length = 0;
        foreach (var c in trimmed)
        {
            if (char.IsLetterOrDigit(c) || c is '-')
            {
                buffer[length++] = char.ToLowerInvariant(c);
            }
        }

        // Strip leading hyphens — cloud-init writes the value
        // verbatim into /etc/hostname and the kernel rejects
        // hostnames with a leading hyphen.
        var startIndex = 0;
        while (startIndex < length && buffer[startIndex] is '-')
        {
            startIndex++;
        }

        return startIndex >= length
            ? throw new ArgumentException(
                $"Hostname '{raw}' sanitises to an empty string.",
                nameof(raw))
            : new string(buffer, startIndex, length - startIndex);
    }

    /// <summary>
    ///     Stable, deterministic instance-id for a given workload
    ///     name. Same input → same output across agent restarts,
    ///     so cloud-init treats subsequent boots as a no-op
    ///     (otherwise it would re-run user-data on every boot).
    /// </summary>
    /// <param name="workloadName">Libvirt domain name / workload id.</param>
    public static Guid DeterministicInstanceId(string workloadName)
    {
        var bytes = Encoding.UTF8.GetBytes("plexor.cidata:" + workloadName);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);

        // SHA256 → GUID (take the first 16 bytes). RFC 4122
        // version-5 would set variant/version bits, but for a
        // stable opaque id the raw first-16-bytes works (cloud-
        // init treats instance-id as an opaque string).
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    /// <summary>
    ///     Resolve the canonical cidata ISO path for a workload.
    ///     Convention: <c>/var/lib/plexor/cidata/{name}-cidata.iso</c>.
    ///     The directory must exist when the ISO is written —
    ///     <see cref="Build" /> creates it on demand.
    /// </summary>
    /// <param name="workloadName"></param>
    /// <param name="root"></param>
    public static string ResolveIsoPath(string workloadName, string root = "/var/lib/plexor/cidata")
    {
        var safe = string.Concat(workloadName.Where(static c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' or '.'));
        return Path.Combine(root, safe + "-cidata.iso");
    }
}

