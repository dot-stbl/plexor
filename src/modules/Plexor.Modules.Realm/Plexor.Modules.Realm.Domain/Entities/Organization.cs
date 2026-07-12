using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Realm.Domain.Entities;

/// <summary>
///     One organization = the top-level billing + auth boundary. A
///     multi-org deploy has multiple rows here; a single-org install
///     has one. Identity FKs into this row; resources declare it as
///     the outermost scope.
/// </summary>
/// <remarks>
///     <para><b>Slug.</b> URL-safe lowercase kebab-case identifier
///     (<c>stbl</c>, <c>cloudhybrid</c>). Unique globally — used in
///     login requests to resolve tenant before the password check,
///     matching the FE ScopeSwitcher hierarchy.</para>
///     <para><b>v0.1 status values.</b> <c>"active"</c> | <c>"suspended"</c>.
///     No <c>"archived"</c> yet — that lands in Phase 2 with a real
///     delete operation.</para>
/// </remarks>
public sealed class Organization : ICreatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Display name shown in UI + audit entries.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>URL-safe lowercase kebab-case identifier
    /// (<c>stbl</c>). Unique globally.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Organization status. <c>"active"</c> by default;
    /// transitions to <c>"suspended"</c> on admin action.</summary>
    public string Status { get; init; } = "active";

    /// <summary>Creation time (UTC). See
    /// <see cref="Plexor.Shared.Kernel.Common.ICreatedAt" />.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}