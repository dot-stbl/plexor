// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfBrandingServiceHelpers — file-static helpers pulled out of
// EfBrandingService to satisfy the no-private-methods convention
// (class-layout-and-tooling.md §1a / §9.2 — Repository / port
// implementation). The service class is a thin orchestration
// surface; the per-method logic lives here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Persistence;

namespace Plexor.Modules.Branding.Infrastructure.Branding;

/// <summary>
///     Per-method helpers for <see cref="EfBrandingService" />. Pure
///     functions over <see cref="BrandingDbContext" /> + the domain
///     entities — extracted to satisfy the no-private-methods rule.
/// </summary>
/// <remarks>
///     <para><b>Why the dance.</b> The domain entities use
///     <c>init</c>-only properties (per the C# coding rules) —
///     post-load mutation of an EF-tracked entity is therefore a
///     compile error. The upsert path Detaches the tracked row and
///     inserts a fresh entity with the new values merged in; EF
///     treats the Detach + Add as a clean UPDATE the next time
///     SaveChanges runs (the row's PK is preserved).</para>
/// </remarks>
internal static class EfBrandingServiceHelpers
{
    /// <summary>
    ///     Read the singleton global row. Returns a freshly-constructed
    ///     sentinel with the documented defaults when the row doesn't
    ///     exist yet (first boot before the seeder ran) — the
    ///     controller returns 200 with the defaults instead of 404.
    /// </summary>
    /// <remarks>
    ///     The sentinel's <c>UpdatedAt</c> uses <see cref="DateTimeOffset.UtcNow" />
    ///     rather than a <see cref="TimeProvider" />: the read path doesn't
    ///     take a clock (no need to thread it just for the never-realistic
    ///     "row missing" fallback), and the seeder
    ///     (<c>BrandingGlobalSeederHostedService</c>) inserts the real row
    ///     on first boot before any HTTP request lands.
    /// </remarks>
    public static async Task<GlobalThemeConfig> GetGlobalInternalAsync(
        BrandingDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.GlobalThemeConfig
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? new GlobalThemeConfig
            {
                UpdatedAt = DateTimeOffset.UtcNow,
            };
    }

    /// <summary>
    ///     Upsert the singleton global row. The seeder inserts a
    ///     default row on first boot; this method updates it in
    ///     place. Concurrent writers hit the UNIQUE partial index
    ///     and surface as a 500 — callers retry.
    /// </summary>
    public static async Task<GlobalThemeConfig> UpsertGlobalInternalAsync(
        BrandingDbContext db,
        TimeProvider clock,
        GlobalThemeConfig config,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var existing = await db.GlobalThemeConfig
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            var seeded = new GlobalThemeConfig
            {
                Id = GlobalThemeConfig.SingletonId,
                BrandName = config.BrandName,
                BrandLogoUrl = config.BrandLogoUrl,
                BrandFaviconUrl = config.BrandFaviconUrl,
                DefaultPresetId = config.DefaultPresetId,
                CustomAccent = config.CustomAccent,
                UpdatedBy = actorUserId,
                UpdatedAt = now,
            };
            await db.GlobalThemeConfig.AddAsync(seeded, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return seeded;
        }

        // init-only properties mean we can't mutate the tracked entity
        // in-place. Detach it, build a fresh entity with the merged
        // values, and let EF Core turn the Detach + Add into an UPDATE
        // (the PK matches the tracked row).
        var entry = db.Entry(existing);
        entry.State = EntityState.Detached;

        var updated = new GlobalThemeConfig
        {
            Id = existing.Id,
            BrandName = config.BrandName,
            BrandLogoUrl = config.BrandLogoUrl,
            BrandFaviconUrl = config.BrandFaviconUrl,
            DefaultPresetId = config.DefaultPresetId,
            CustomAccent = config.CustomAccent,
            UpdatedBy = actorUserId,
            UpdatedAt = now,
        };
        await db.GlobalThemeConfig.AddAsync(updated, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    ///     Read the per-org override row. Returns null when no row
    ///     exists — callers fall back to the operator defaults.
    /// </summary>
    public static async Task<OrgThemeConfig?> GetOrgOverrideInternalAsync(
        BrandingDbContext db,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        return await db.OrgThemeConfig
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.OrgId == orgId, cancellationToken);
    }

    /// <summary>
    ///     Upsert the per-org override row. Inserts when no row
    ///     exists; updates in place when one does. The UNIQUE
    ///     index on <c>org_id</c> backs the read with a hard
    ///     invariant against racing writers.
    /// </summary>
    public static async Task<OrgThemeConfig> UpsertOrgOverrideInternalAsync(
        BrandingDbContext db,
        TimeProvider clock,
        Guid orgId,
        OrgThemeConfig config,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var existing = await db.OrgThemeConfig
            .FirstOrDefaultAsync(row => row.OrgId == orgId, cancellationToken);

        if (existing is null)
        {
            var inserted = new OrgThemeConfig
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                PresetId = config.PresetId,
                CustomAccent = config.CustomAccent,
                BrandName = config.BrandName,
                BrandLogoUrl = config.BrandLogoUrl,
                BrandFaviconUrl = config.BrandFaviconUrl,
                UpdatedBy = actorUserId,
                UpdatedAt = now,
                CreatedAt = now,
            };
            await db.OrgThemeConfig.AddAsync(inserted, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return inserted;
        }

        // init-only properties — detach + re-Add for UPDATE.
        var entry = db.Entry(existing);
        entry.State = EntityState.Detached;

        var updated = new OrgThemeConfig
        {
            Id = existing.Id,
            OrgId = existing.OrgId,
            PresetId = config.PresetId,
            CustomAccent = config.CustomAccent,
            BrandName = config.BrandName,
            BrandLogoUrl = config.BrandLogoUrl,
            BrandFaviconUrl = config.BrandFaviconUrl,
            UpdatedBy = actorUserId,
            UpdatedAt = now,
            CreatedAt = existing.CreatedAt,
        };
        await db.OrgThemeConfig.AddAsync(updated, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    ///     Delete the per-org override row. Returns true when a row
    ///     was actually removed; false when no row existed
    ///     (idempotent).
    /// </summary>
    public static async Task<bool> DeleteOrgOverrideInternalAsync(
        BrandingDbContext db,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        var existing = await db.OrgThemeConfig
            .FirstOrDefaultAsync(row => row.OrgId == orgId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        db.OrgThemeConfig.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    ///     Resolve the merged boot config: read the global row,
    ///     optionally read the per-org override, merge
    ///     (org-wins-on-non-null). When <paramref name="orgId" />
    ///     is null, only the global row contributes.
    /// </summary>
    public static async Task<ResolvedBrandingConfig> ResolveForOrgInternalAsync(
        BrandingDbContext db,
        Guid? orgId,
        CancellationToken cancellationToken)
    {
        var global = await db.GlobalThemeConfig
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? new GlobalThemeConfig();

        OrgThemeConfig? orgOverride = null;
        if (orgId.HasValue)
        {
            orgOverride = await db.OrgThemeConfig
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.OrgId == orgId.Value, cancellationToken);
        }

        return new ResolvedBrandingConfig
        {
            BrandName = orgOverride?.BrandName ?? global.BrandName,
            BrandLogoUrl = orgOverride?.BrandLogoUrl ?? global.BrandLogoUrl,
            BrandFaviconUrl = orgOverride?.BrandFaviconUrl ?? global.BrandFaviconUrl,
            DefaultPresetId = orgOverride?.PresetId ?? global.DefaultPresetId,
            CustomAccent = orgOverride?.CustomAccent ?? global.CustomAccent,
        };
    }
}