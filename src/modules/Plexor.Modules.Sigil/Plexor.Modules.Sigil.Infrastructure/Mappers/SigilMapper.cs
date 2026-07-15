// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigilMappers — Mapperly source-generated implementation of
// ISigilMapper. Entity → DTO translations for the Sigil module.
//
// DTOs are sealed partial class (init-only properties) for
// Mapperly + EF Core LINQ compatibility. Per the project mapping rule
// (`.agents/rules/coding/mapping.md`), each method here is a single
// line of intent ("ToUserSummary") — the source generator emits the
// field-by-field copy at build time.
// ==========================================================================

using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Plexor.Modules.Sigil.Infrastructure.Mappers;

/// <summary>
///     Mapperly-generated mapper. Implements <see cref="ISigilMapper" />.
///     Registered in DI as a singleton via
///     <c>AddSingleton&lt;ISigilMapper, SigilMappers&gt;()</c> —
///     generated bodies are stateless.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SigilMapper : ISigilMapper
{
    /// <summary>Map <see cref="User" /> → <see cref="UserSummary" />.</summary>
    public partial UserSummary ToUserSummary(User source);

    /// <summary>Map <see cref="ApiKey" /> → <see cref="ApiKeySummary" />.</summary>
    public partial ApiKeySummary ToApiKeySummary(ApiKey source);

    /// <summary>Map <see cref="SshKey" /> → <see cref="SshKeySummary" />.</summary>
    public partial SshKeySummary ToSshKeySummary(SshKey source);

    /// <summary>Map <see cref="Role" /> → <see cref="RoleSummary" />.</summary>
    public partial RoleSummary ToRoleSummary(Role source);

    /// <summary>Map <see cref="RoleBinding" /> → <see cref="RoleBindingSummary" />.</summary>
    public partial RoleBindingSummary ToRoleBindingSummary(RoleBinding source);
}
