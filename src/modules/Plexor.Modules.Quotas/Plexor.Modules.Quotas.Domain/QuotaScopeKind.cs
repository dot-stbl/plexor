namespace Plexor.Modules.Quotas.Domain;

/// <summary>
///     Which level of the Realm hierarchy a quota scope targets.
/// </summary>
/// <remarks>
///     <para><b>Scope walker.</b> The enforcer in 4.5.b walks
///     <see cref="Folder" /> → <see cref="Team" /> → <see cref="Org" />
///     and returns the minimum value found. v1 scopes are polymorphic
///     on the (Kind, Id) tuple — there is no FK to a specific Realm
///     table at the DB level.</para>
///     <para><b>Stored as string.</b> The enum is mapped to
///     <c>varchar(16)</c> in PostgreSQL via the entity configuration;
///     the wire value is the lowercase member name (e.g. <c>"folder"</c>).
///     Forward-compatible: new scope kinds (e.g. <c>"region"</c> in
///     Phase 7+) do not require a migration.</para>
/// </remarks>
public enum QuotaScopeKind
{
    /// <summary>Org-scoped quota — applies to every team/folder in the org.</summary>
    Org = 0,

    /// <summary>Team-scoped quota — applies to every folder in the team.</summary>
    Team = 1,

    /// <summary>Folder-scoped quota — applies to a single folder.</summary>
    Folder = 2,
}
