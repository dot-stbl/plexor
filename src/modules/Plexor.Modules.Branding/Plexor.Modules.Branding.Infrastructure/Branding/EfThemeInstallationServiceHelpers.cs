// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfThemeInstallationServiceHelpers — file-static helpers pulled
// out of EfThemeInstallationService to satisfy the no-private-
// methods convention (class-layout-and-tooling.md §1a / §9.1).
// The service class is a thin orchestration surface; the
// per-method logic lives here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Persistence;
using Plexor.Modules.Branding.Infrastructure.ThemeManifests;
using PlexorThemeManifest = Plexor.Modules.Branding.Application.Branding.ThemeManifest;
using PlexorThemeManifestVerificationException = Plexor.Modules.Branding.Application.Branding.ThemeManifestVerificationException;
using PlexorUnknownThemeException = Plexor.Modules.Branding.Application.Branding.UnknownThemeException;

namespace Plexor.Modules.Branding.Infrastructure.Branding;

/// <summary>
///     Per-method helpers for <see cref="EfThemeInstallationService" />.
///     Pure functions over the DbContext + manifest verifier +
///     registry; the verify → upsert dance and the read / delete
///     paths all live here.
/// </summary>
internal static class EfThemeInstallationServiceHelpers
{
    /// <summary>
    ///     Read the per-org installation row. Returns null when
    ///     no row exists — the controller maps that to a 404 and
    ///     the boot script falls back to the resolved operator
    ///     defaults.
    /// </summary>
    public static async Task<ThemeInstallation?> GetForOrgInternalAsync(
        BrandingDbContext db,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        return await db.ThemeInstallations
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.OrgId == orgId, cancellationToken);
    }

    /// <summary>
    ///     Upsert the per-org installation row. The verifier runs
    ///     before <c>SaveChanges</c> so a tampered manifest never
    ///     reaches the database. The UNIQUE constraint on
    ///     <c>org_id</c> backs the upsert with a hard invariant
    ///     against racing writers.
    /// </summary>
    /// <exception cref="PlexorUnknownThemeException">
    ///     The supplied <paramref name="themeId" /> doesn't match
    ///     any entry in the registry (mapped to 404).
    /// </exception>
    /// <exception cref="PlexorThemeManifestVerificationException">
    ///     The supplied <paramref name="signature" /> does not
    ///     match a fresh HMAC over the canonical manifest bytes
    ///     (mapped to 400).
    /// </exception>
    public static async Task<ThemeInstallation> UpsertInternalAsync(
        BrandingDbContext db,
        TimeProvider clock,
        CommunityThemeRegistry registry,
        IThemeManifestVerifier verifier,
        Guid orgId,
        string themeId,
        PlexorThemeManifest manifest,
        string signature,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        // Theme must exist in the host registry. The FE bundle
        // and the host registry stay in lock-step so the host
        // doesn't have to trust a client-supplied id.
        if (registry.TryFind(themeId) is null)
        {
            throw new PlexorUnknownThemeException(themeId);
        }

        // Verifier runs second — verifyManifest throws on a bad
        // signature, so no DB write ever reaches SaveChanges when
        // the signature doesn't match. The ThemeManifest we pass
        // here is the canonical record the FE signed; the
        // hub-side verifier never re-reads the manifest from
        // disk before re-signing, so the canonical bytes are
        // exactly what the publisher signed.
        verifier.VerifyManifest(manifest, signature);

        var now = clock.GetUtcNow();
        var existing = await db.ThemeInstallations
            .FirstOrDefaultAsync(row => row.OrgId == orgId, cancellationToken);

        if (existing is null)
        {
            var inserted = new ThemeInstallation
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                ThemeId = themeId,
                ManifestSignature = signature,
                ActivatedAt = now,
                ActivatedBy = actorUserId,
            };
            await db.ThemeInstallations.AddAsync(inserted, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return inserted;
        }

        // init-only properties — Detach the tracked row first
        // (so the InMemory provider's identity map forgets the
        // original entry), then build a fresh entity with the
        // merged values and Update() it. The row id is preserved
        // so external observers that captured the original
        // installation id continue to see the same row; Update()
        // marks all properties as Modified, which translates to a
        // full-column UPDATE both on Postgres and on the InMemory
        // provider used by the unit tests.
        var entry = db.Entry(existing);
        entry.State = EntityState.Detached;

        var updated = new ThemeInstallation
        {
            Id = existing.Id,
            OrgId = existing.OrgId,
            ThemeId = themeId,
            ManifestSignature = signature,
            ActivatedAt = now,
            ActivatedBy = actorUserId,
        };
        db.ThemeInstallations.Update(updated);
        await db.SaveChangesAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    ///     Delete the per-org installation row. Returns true when
    ///     a row was actually removed; false when no row existed
    ///     (idempotent reset to operator defaults).
    /// </summary>
    public static async Task<bool> DeleteInternalAsync(
        BrandingDbContext db,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        var existing = await db.ThemeInstallations
            .FirstOrDefaultAsync(row => row.OrgId == orgId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        db.ThemeInstallations.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
