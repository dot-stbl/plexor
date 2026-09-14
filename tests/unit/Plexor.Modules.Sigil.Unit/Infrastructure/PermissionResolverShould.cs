// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionResolverShould — exercises the LINQ implementation of
// IPermissionResolver. Joins role_bindings → roles, flattens the
// roles' PermissionScope.Value list, deduplicates via Distinct().
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Infrastructure;

/// <summary>
///     Behavioural tests for <see cref="PermissionResolver" />. Each
///     test seeds a role with a known permission set, binds it to a
///     user, and asserts the resolver returns the expected union +
///     deduplication.
/// </summary>
public sealed class PermissionResolverShould
{
    /// <summary>Verifies that <see cref="PermissionResolver.ResolveAsync" />
    /// returns the bound role's permissions verbatim.</summary>
    [Fact(DisplayName = "Given user bound to one role with 2 permissions, when ResolveAsync, then returns those 2 permissions")]
    public async Task SingleBindingReturnsRolePermissionsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var role = await SeedRoleAsync(
            db,
            orgId,
            "admin",
            [new PermissionScope("compute.vms.read"), new PermissionScope("iam.users.read")]);
        await SeedBindingAsync(db, orgId, userId, role.Id);

        var resolver = new PermissionResolver(db);
        var permissions = await resolver.ResolveAsync(userId, orgId);

        permissions.ShouldBe(["compute.vms.read", "iam.users.read"]);
    }

    /// <summary>Verifies that <see cref="PermissionResolver.ResolveAsync" />
    /// unions permissions across multiple roles and deduplicates the
    /// overlap. The DISTINCT in the LINQ ensures each permission
    /// string appears at most once.</summary>
    [Fact(DisplayName = "Given user bound to 2 roles with overlapping permissions, when ResolveAsync, then returns union deduplicated")]
    public async Task MultipleBindingsAreUnionedAndDeduplicatedAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var admin = await SeedRoleAsync(
            db,
            orgId,
            "admin",
            [new PermissionScope("compute.vms.read"), new PermissionScope("iam.users.read")]);
        var editor = await SeedRoleAsync(
            db,
            orgId,
            "editor",
            [new PermissionScope("compute.vms.read"), new PermissionScope("compute.vms.write")]);
        await SeedBindingAsync(db, orgId, userId, admin.Id);
        await SeedBindingAsync(db, orgId, userId, editor.Id);

        var resolver = new PermissionResolver(db);
        var permissions = await resolver.ResolveAsync(userId, orgId);

        permissions.ShouldBe(["compute.vms.read", "iam.users.read", "compute.vms.write"]);
    }

    /// <summary>Verifies that <see cref="PermissionResolver.ResolveAsync" />
    /// returns an empty collection when the user has no role bindings
    /// — the auth controller treats this as "no permissions, deny
    /// every authorization check".</summary>
    [Fact(DisplayName = "Given user with no bindings, when ResolveAsync, then returns empty collection")]
    public async Task NoBindingsReturnsEmptyAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var resolver = new PermissionResolver(db);

        var permissions = await resolver.ResolveAsync(Guid.NewGuid(), Guid.NewGuid());

        permissions.ShouldBeEmpty();
    }

    private static async Task<Role> SeedRoleAsync(
        IdentityDbContext db,
        Guid orgId,
        string name,
        IReadOnlyList<PermissionScope> permissions)
    {
        var now = DateTimeOffset.UtcNow;
        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = name,
            Permissions = permissions,
            BuiltIn = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Roles.AddAsync(role);
        await db.SaveChangesAsync();
        return role;
    }

    private static async Task SeedBindingAsync(
        IdentityDbContext db,
        Guid orgId,
        Guid userId,
        Guid roleId)
    {
        await db.RoleBindings.AddAsync(new RoleBinding
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            UserId = userId,
            RoleId = roleId,
            TeamId = null,
            FolderId = null,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
