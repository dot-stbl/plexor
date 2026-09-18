// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProviderConfig — per-organization authentication provider
// configuration. One row per org (UNIQUE on OrgId) — exactly one of
// Sigil (default) or Oidc.
//
// v1: every Plexor deployment ships with this table; the Migrator
// seeds Sigil rows for every existing org on first boot
// (OrgAuthProviderSeeder). Switching to Oidc is a per-org admin
// decision via PUT /api/v1/iam/orgs/{orgId}/auth-provider.
// ============================================================================

using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Realm.Domain.Entities;

/// <summary>
///     One row per organization, declaring which authentication
///     backend serves logins for that org. Default is
///     <see cref="OrgAuthProvider.Sigil" /> (local email +
///     password against <c>sigil.users.password_hash</c>); an
///     org admin can switch the row to
///     <see cref="OrgAuthProvider.Oidc" /> and point it at an
///     external OIDC provider (Keycloak, Authentik, Dex, etc.).
/// </summary>
/// <remarks>
///     <para><b>Schema.</b> Lives in the <c>realm</c> PostgreSQL
///     schema (architecture theme — see root
///     <c>AGENTS.md</c>); table <c>org_auth_provider_configs</c>.
///     The C# concept name <c>OrgAuthProviderConfig</c> matches
///     the operator-facing vocabulary; the schema name
///     <c>realm</c> matches the architecture-theme convention.</para>
///     <para><b>Invariant.</b> Exactly one row per
///     <see cref="OrgId" />, enforced by the UNIQUE index on the
///     <c>org_id</c> column. The Migrator's
///     <c>OrgAuthProviderSeeder</c> ensures every existing org has
///     a Sigil row on first boot; new orgs get a Sigil row at
///     creation time (Phase 2+ SaaS flow).</para>
///     <para><b>OIDC fields.</b> When
///     <see cref="Provider" /> = <see cref="OrgAuthProvider.Oidc" />,
///     <see cref="OidcAuthority" />, <see cref="OidcClientId" />,
///     and <see cref="OidcClientSecretProtected" /> are populated.
///     When <see cref="Provider" /> = <see cref="OrgAuthProvider.Sigil" />,
///     all three are <c>null</c>. The PUT validator enforces this
///     invariant at the boundary.</para>
///     <para><b>Encryption-at-rest.</b> The OIDC client secret is
///     encrypted via <c>IDataProtector</c> (Microsoft.AspNetCore.DataProtection)
///     before the row hits disk; the controller never decrypts it
///     for the GET response (only for the <c>/test</c> endpoint).
///     Keyring location is configured in
///     <c>Plexor.Host/Program.cs</c>.</para>
/// </remarks>
public sealed class OrgAuthProviderConfig : ICreatedAt, IUpdatedAt
{
    /// <summary>UUID v7 PK.</summary>
    public Guid Id { get; init; }

    /// <summary>FK to <c>realm.organizations.id</c> (denormalized
    /// + UNIQUE — exactly one row per org).</summary>
    public Guid OrgId { get; init; }

    /// <summary>Provider discriminator. <see cref="OrgAuthProvider.Sigil" />
    /// = local email+password; <see cref="OrgAuthProvider.Oidc" />
    /// = external IDP via OIDC.</summary>
    public OrgAuthProvider Provider { get; init; }

    /// <summary>OIDC issuer URL —
    /// <c>https://keycloak.plexor.example.com/realms/plexor</c>.
    /// Null when <see cref="Provider" /> = <see cref="OrgAuthProvider.Sigil" />.
    /// Validated as an absolute HTTPS URL on PUT.</summary>
    public string? OidcAuthority { get; init; }

    /// <summary>OIDC confidential client id registered at the
    /// OIDC provider. Null when
    /// <see cref="Provider" /> = <see cref="OrgAuthProvider.Sigil" />.</summary>
    public string? OidcClientId { get; init; }

    /// <summary>OIDC confidential client secret — encrypted at
    /// rest via <c>IDataProtector</c>. Null when
    /// <see cref="Provider" /> = <see cref="OrgAuthProvider.Sigil" />.
    /// Never returned in the GET response; only the <c>/test</c>
    /// endpoint decrypts it for an outbound call to the
    /// configured authority.</summary>
    public string? OidcClientSecretProtected { get; init; }

    /// <summary>Default OIDC scopes. v1 always includes
    /// <c>openid</c> + <c>profile</c> + <c>email</c>; the admin
    /// MAY extend via PUT.</summary>
    public IReadOnlyList<string> OidcScopes { get; init; } = ["openid", "profile", "email"];

    /// <summary>UTC. See <see cref="ICreatedAt" />.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC. See <see cref="IUpdatedAt" />.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
///     Discriminator for the per-org authentication backend.
///     Stored as a small int on the wire; the JSON shape is the
///     enum name (PascalCase, bound via the
///     <c>JsonStringEnumConverter</c> registered in
///     <c>Plexor.Host/Program.cs</c>).
/// </summary>
public enum OrgAuthProvider
{
    /// <summary>Local email + password against
    /// <c>sigil.users.password_hash</c>. Default value; every
    /// existing Plexor org is on this backend until an admin
    /// changes it.</summary>
    Sigil = 0,

    /// <summary>External OIDC — Plexor validates the
    /// IDP-issued JWT against the per-tenant config.</summary>
    Oidc = 1,
}
