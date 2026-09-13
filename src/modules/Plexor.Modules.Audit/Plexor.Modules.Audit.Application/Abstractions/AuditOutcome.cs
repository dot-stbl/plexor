namespace Plexor.Modules.Audit.Application.Abstractions;

/// <summary>
///     Result of the audited action. Stored as text in the database
///     (<c>atlas.audit_entries.outcome</c>) for queryability without
///     numeric coupling.
/// </summary>
public enum AuditOutcome
{
    /// <summary>Action completed without error.</summary>
    Succeeded = 0,

    /// <summary>Action attempted but failed (5xx, internal error, exception).</summary>
    Failed = 1,

    /// <summary>Action blocked by authorization (401/403, permission denied).</summary>
    Denied = 2,
}
