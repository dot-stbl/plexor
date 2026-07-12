namespace Plexor.Modules.Tenants.Domain;

/// <summary>
///     One tenant (= organization in YC terminology). Identity schema
///     references tenants via FK; cross-tenant users are Phase 2.
/// </summary>
/// <remarks>
///     <para><b>Slug.</b> Lowercase kebab-case identifier (<c>acme-corp</c>)
///     used in URLs and login requests. Unique globally so users can
///     resolve their tenant before supplying credentials.</para>
///     <para><b>v0.1 status values.</b> <c>"active"</c> | <c>"suspended"</c>.
///     No <c>"archived"</c> yet — Phase 2 when delete becomes a real
///     operation.</para>
/// </remarks>
public sealed class Tenant
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Display name shown in UI + audit entries.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>URL-safe lowercase kebab-case identifier
    /// (<c>acme-corp</c>). Unique globally.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Tenant status. <c>"active"</c> by default; transitions
    /// to <c>"suspended"</c> on admin action.</summary>
    public string Status { get; init; } = "active";

    /// <summary>Account creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }
}