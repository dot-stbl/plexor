// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditActions — stable wire names for the audit event log. Every audit
// row in atlas.audit_entries carries one of these strings on its `action`
// column; consumers (admin UI 5.3, log aggregators, the retention sweeper)
// branch on the string, never on the row id.
//
// The constant set is the single source of truth. Adding a new event = adding
// a new constant here + emitting through IAuditEmitter at the right call
// site. Renaming an existing constant is a breaking change — consumers will
// silently lose the old name. Document each new entry inline.
//
// Lives in Plexor.Shared.Kernel because every module that emits events
// (Quotas via IQuotaAuditEmitter, Realm via OrgAuthProvidersController in
// 5.2, future modules) imports only the kernel — the modules don't depend
// on each other. Same placement discipline as IQuotaAuditEmitter.
// ============================================================================

namespace Plexor.Shared.Kernel.Audit;

/// <summary>
///     Stable dot.case wire names for the audit event log. Each
///     event MUST have a unique value here — duplicate names mean two
///     distinct events collide on the <c>audit_entries.action</c>
///     column and the admin UI's event-type filter becomes ambiguous.
/// </summary>
public static class AuditActions
{
    // -------------------------------------------------------------------
    // Quotas (Phase 4.5)
    // -------------------------------------------------------------------

    /// <summary>
    ///     <c>PUT /api/v1/quotas/assignments</c> created or updated
    ///     an assignment. Emitted by
    ///     <c>QuotasController.UpsertAssignmentAsync</c>. Payload
    ///     keys: <c>definition_key</c>, <c>scope_kind</c>,
    ///     <c>scope_id</c>, <c>assignment_id</c>.
    /// </summary>
    public const string QuotasAssignmentChanged = "quotas.assignment.changed";

    /// <summary>
    ///     <c>DELETE /api/v1/quotas/assignments/{id}</c> removed an
    ///     assignment. Emitted by
    ///     <c>QuotasController.DeleteAssignmentAsync</c>. Payload
    ///     keys: <c>definition_key</c>, <c>scope_kind</c>,
    ///     <c>scope_id</c>, <c>assignment_id</c>.
    /// </summary>
    public const string QuotasAssignmentRemoved = "quotas.assignment.removed";

    /// <summary>
    ///     The enforcer returned <c>Denied</c> — the request was over
    ///     the effective limit. Emitted by
    ///     <c>EfQuotaEnforcer.CheckAndReserveAsync</c>. Payload
    ///     keys: <c>definition_key</c>, <c>scope_kind</c>,
    ///     <c>scope_id</c>, <c>used</c>, <c>limit</c>,
    ///     <c>requested</c>.
    /// </summary>
    public const string QuotasUsageExceeded = "quotas.usage.exceeded";

    /// <summary>
    ///     The enforcer returned <c>AllowedWithWarning</c> — the
    ///     request succeeded but crossed the 80% threshold. Emitted
    ///     by <c>EfQuotaEnforcer.CheckAndReserveAsync</c>. Payload
    ///     keys: <c>definition_key</c>, <c>scope_kind</c>,
    ///     <c>scope_id</c>, <c>used</c>, <c>limit</c>,
    ///     <c>requested</c>, <c>threshold_pct</c>.
    /// </summary>
    public const string QuotasLimitApproaching = "quotas.limit.approaching";

    // -------------------------------------------------------------------
    // Auth-providers (Phase 5.2 — declared now so the generic
    // IAuditEmitter contract is stable from the start)
    // -------------------------------------------------------------------

    /// <summary>
    ///     An org's auth-provider config was upserted via
    ///     <c>PUT /api/v1/orgs/{orgId}/auth-providers</c>. Emitted
    ///     by <c>OrgAuthProvidersController</c>. Payload keys:
    ///     <c>old_provider</c>, <c>new_provider</c>.
    /// </summary>
    public const string OrgAuthProviderChanged = "org.auth_provider.changed";

    // -------------------------------------------------------------------
    // Theme marketplace (Phase 5+)
    // -------------------------------------------------------------------

    /// <summary>
    ///     <c>PUT /api/v1/branding/theme</c> activated or re-activated
    ///     a community theme for the caller's tenant. Emitted by
    ///     <c>ThemeInstallationsController.UpsertAsync</c>. Payload
    ///     keys: <c>theme_id</c>, <c>signature</c>.
    /// </summary>
    public const string ThemeInstalledActivated = "theme_installed.activated";

    /// <summary>
    ///     <c>DELETE /api/v1/branding/theme</c> removed the
    ///     per-tenant community-theme installation (the tenant
    ///     reverts to the resolved operator defaults). Emitted by
    ///     <c>ThemeInstallationsController.DeleteAsync</c> only
    ///     when a row was actually removed — idempotent no-op
    ///     deletes (no row present) do not emit. Payload keys:
    ///     <c>theme_id</c>.
    /// </summary>
    public const string ThemeInstalledDeactivated = "theme_installed.deactivated";
}
