// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaScope — polymorphic (Kind, Id, OrgId) scope value. Shared between
// Plexor.Modules.Quotas (catalog / walker / enforcer) and the modules
// that construct QuotaScope values (Compute / Storage / Network
// resource-create handlers); the type is therefore in Plexor.Shared.Kernel,
// not in a single module.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Polymorphic quota scope — the (Kind, Id, OrgId) triple the
///     enforcer uses to walk the Realm hierarchy and resolve the
///     effective assignment value.
/// </summary>
/// <remarks>
///     <para><b>Polymorphic on Kind.</b> <see cref="Id" /> refers to an
///     Organization, Team, or Folder depending on <see cref="Kind" />.
///     The DB does not enforce an FK (the scope walker validates at
///     runtime); see
///     <c>openspec/changes/phase-4-5-quotas/specs/quotas/spec.md</c>
///     Requirement "Migration order".</para>
///     <para><b>OrgId denormalization.</b> Always populated — even on
///     folder / team scopes the OrgId is the tenant boundary for
///     authorization and audit filtering.</para>
/// </remarks>
/// <param name="Kind">Org / Team / Folder.</param>
/// <param name="Id">Id of the matching Realm entity.</param>
/// <param name="OrgId">Tenant the scope belongs to.</param>
public sealed record QuotaScope(QuotaScopeKind Kind, Guid Id, Guid OrgId)
{
    /// <summary>Construct an org-scoped quota scope.</summary>
    /// <param name="orgId">Organization id.</param>
    /// <returns>Scope with <see cref="OrgId" /> = <paramref name="orgId" />.</returns>
    public static QuotaScope Org(Guid orgId)
    {
        return new QuotaScope(QuotaScopeKind.Org, orgId, orgId);
    }

    /// <summary>Construct a team-scoped quota scope.</summary>
    /// <param name="teamId">Team id.</param>
    /// <param name="orgId">Organization id (denormalized).</param>
    /// <returns>Scope with <see cref="Kind" /> = Team.</returns>
    public static QuotaScope Team(Guid teamId, Guid orgId)
    {
        return new QuotaScope(QuotaScopeKind.Team, teamId, orgId);
    }

    /// <summary>Construct a folder-scoped quota scope.</summary>
    /// <param name="folderId">Folder id.</param>
    /// <param name="orgId">Organization id (denormalized).</param>
    /// <param name="teamId">Team id (unused today; reserved for future scope-walker hints).</param>
    /// <returns>Scope with <see cref="Kind" /> = Folder.</returns>
    public static QuotaScope Folder(Guid folderId, Guid orgId, Guid? teamId)
    {
        _ = teamId;
        return new QuotaScope(QuotaScopeKind.Folder, folderId, orgId);
    }
}
