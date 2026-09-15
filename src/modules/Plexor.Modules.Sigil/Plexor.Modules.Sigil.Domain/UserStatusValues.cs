namespace Plexor.Modules.Sigil.Domain;

/// <summary>
///     Canonical status string values for the
///     <c>sigil.users.status</c> column and the parallel Realm scope
///     records (<c>realm.organizations.status</c>,
///     <c>realm.teams.status</c>, <c>realm.folders.status</c>).
/// </summary>
public static class UserStatusValues
{
    /// <summary><c>active</c> — the user can sign in.</summary>
    public const string Active = "active";

    /// <summary><c>suspended</c> — the user cannot sign in.</summary>
    public const string Suspended = "suspended";
}
