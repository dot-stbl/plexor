// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RoleCommandHandlers — CRUD on sigil.roles. Built-in roles are
// immutable; the handler enforces this in every write path.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Mappers;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Users;

/// <summary>
///     Create a custom role. Built-in roles are duplicated-error
///     protected by the (org_id, name) unique index.
/// </summary>
/// <param name="db"></param>
public sealed class CreateRoleCommandHandler(
    IdentityDbContext db) : ICommandHandler<CreateRoleCommand, CreateRoleResult>
{
    /// <inheritdoc />
    public async Task<CreateRoleResult> HandleAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "Role name is required.");
        }

        var permissions = command.Permissions
            .Select(static value => new PermissionScope(value))
            .ToArray();

        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrgId = command.OrgId,
            Name = command.Name,
            Description = command.Description,
            Permissions = permissions,
            BuiltIn = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await db.Roles.AddAsync(role, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new CreateRoleResult(role.Id);
    }
}

/// <summary>
///     Update an existing role. Built-in roles are immutable; the
///     handler refuses updates with
///     <see cref="IdentityExceptions.InvalidPermission" />.
/// </summary>
/// <param name="db"></param>
public sealed class UpdateRoleCommandHandler(
    IdentityDbContext db, ISigilMapper mapper) : ICommandHandler<UpdateRoleCommand, RoleSummary>
{
    /// <inheritdoc />
    public async Task<RoleSummary> HandleAsync(
        UpdateRoleCommand command,
        CancellationToken cancellationToken = default)
    {

        if (await db.Roles
                .FirstOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken)
            is not { } role)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "Role not found.");
        }

        if (role.BuiltIn)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "Built-in roles cannot be modified.");
        }

        await db.Roles
            .Where(r => r.Id == command.RoleId)
            .ExecuteUpdateAsync(
                setters =>
                {
                    if (command.Description is not null)
                    {
                        setters.SetProperty(r => r.Description, command.Description);
                    }
                    if (command.Permissions is not null)
                    {
                        var perms = command.Permissions
                            .Select(static value => new PermissionScope(value))
                            .ToArray();
                        setters.SetProperty(r => r.Permissions, perms);
                    }
                    setters.SetProperty(r => r.UpdatedAt, DateTimeOffset.UtcNow);
                },
                cancellationToken);

        return await db.Roles
            .AsNoTracking()
            .Where(r => r.Id == command.RoleId)
            .Select(r => mapper.ToRoleSummary(r))
            .FirstAsync(cancellationToken);
    }
}

/// <summary>
///     Delete a custom role. Built-in roles are protected —
///     <see cref="IdentityExceptions.InvalidPermission" />.
/// </summary>
/// <param name="db"></param>
public sealed class DeleteRoleCommandHandler(
    IdentityDbContext db) : ICommandHandler<DeleteRoleCommand, DeleteRoleResult>
{
    /// <inheritdoc />
    public async Task<DeleteRoleResult> HandleAsync(
        DeleteRoleCommand command,
        CancellationToken cancellationToken = default)
    {

        if (await db.Roles
                .FirstOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken)
            is not { } role)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "Role not found.");
        }

        if (role.BuiltIn)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "Built-in roles cannot be deleted.");
        }

        await db.RoleBindings
            .Where(binding => binding.RoleId == command.RoleId)
            .ExecuteDeleteAsync(cancellationToken);

        await db.Roles
            .Where(r => r.Id == command.RoleId)
            .ExecuteDeleteAsync(cancellationToken);

        return new DeleteRoleResult(command.RoleId);
    }
}

/// <summary>Fetch a single role by id.</summary>
/// <param name="db"></param>
public sealed class GetRoleQueryHandler(
    IdentityDbContext db,
    ISigilMapper mapper) : ICommandHandler<GetRoleQuery, RoleSummary>
{
    /// <inheritdoc />
    public async Task<RoleSummary> HandleAsync(
        GetRoleQuery query,
        CancellationToken cancellationToken = default)
    {

        var summary = await db.Roles
            .AsNoTracking()
            .Where(r => r.Id == query.RoleId)
            .Select(r => mapper.ToRoleSummary(r))
            .FirstOrDefaultAsync(cancellationToken);

        return summary ?? throw new IdentityException(
            IdentityExceptions.InvalidPermission,
            "Role not found.");
    }
}

/// <summary>List roles in an organization.</summary>
/// <param name="db"></param>
public sealed class ListRolesQueryHandler(
    IdentityDbContext db,
    ISigilMapper mapper) : ICommandHandler<ListRolesQuery, IReadOnlyCollection<RoleSummary>>
{
    /// <inheritdoc />
    public Task<IReadOnlyCollection<RoleSummary>> HandleAsync(
        ListRolesQuery query,
        CancellationToken cancellationToken = default)
    {
        return db.Roles
            .AsNoTracking()
            .Where(r => r.OrgId == query.OrgId)
            .OrderBy(r => r.BuiltIn ? 0 : 1)
            .ThenBy(r => r.Name)
            .Select(r => mapper.ToRoleSummary(r))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(static task => (IReadOnlyCollection<RoleSummary>)task.Result,
                cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Current);
    }
}

/// <summary>Result of DeleteRoleCommand.</summary>
/// <param name="RoleId">The role that was deleted.</param>
public sealed record DeleteRoleResult(Guid RoleId);
