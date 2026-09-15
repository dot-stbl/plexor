namespace Plexor.Modules.Audit.Application.Abstractions;

/// <summary>
///     Who initiated the audited action. Drives UI labels, retention
///     rules, and authorization audits (e.g. "show every action that
///     a Node principal attempted in the last 7 days").
/// </summary>
public enum AuditActor
{
    /// <summary>Authenticated human user (JWT bearer).</summary>
    User = 0,

    /// <summary>Service account / API key principal.</summary>
    Service = 1,

    /// <summary>NodeAgent (per-node worker runtime).</summary>
    Node = 2,

    /// <summary>System-internal actor (scheduled task, bootstrapper, hosted service).</summary>
    System = 3,
}
