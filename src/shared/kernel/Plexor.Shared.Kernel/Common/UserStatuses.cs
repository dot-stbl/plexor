namespace Plexor.Shared.Kernel.Common;

/// <summary>
///     Canonical status string values for the <c>status</c> column shared
///     by <c>sigil.users</c> and the parallel Realm scope records
///     (<c>realm.organizations.status</c>, <c>realm.teams.status</c>,
///     <c>realm.folders.status</c>). All four columns hold the same
///     closed set — kept in one place so a new status lights up in
///     every table and every check at the same time.
/// </summary>
public static class UserStatuses
{
    /// <summary><c>active</c> — the user or scope can sign in / be used.</summary>
    public const string Active = "active";

    /// <summary><c>suspended</c> — the user or scope is disabled and must not be used.</summary>
    public const string Suspended = "suspended";
}
