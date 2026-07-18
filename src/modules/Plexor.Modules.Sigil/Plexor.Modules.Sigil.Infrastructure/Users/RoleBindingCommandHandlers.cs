// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RoleBindingCommandHandlers — bind/unbind users to roles.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Mappers;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Users;

/// <summary>
///     Create a role binding. The (user, role, team, folder) tuple
///     must be unique; duplicates surface as
///     <see cref="IdentityExceptions.InvalidPermission" />.
/// </summary>
/// <param name="db"></param>
public sealed class CreateRoleBindingCommandHandler(
    IdentityDbContext db) : ICommandHandler<CreateRoleBindingCommand, CreateRoleBindingResult>
{
    /// <inheritdoc />
    public async Task<CreateRoleBindingResult> HandleAsync(
        CreateRoleBindingCommand command,
        CancellationToken cancellationToken = default)
    {

        // Verify the role exists in the org before creating the binding.
        var roleExists = await db.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == command.RoleId && r.OrgId == command.OrgId, cancellationToken);
        if (!roleExists)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "Role not found in this org.");
        }

        var userExists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == command.UserId && u.OrgId == command.OrgId, cancellationToken);
        if (!userExists)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "User not found in this org.");
        }

        var binding = new RoleBinding
        {
            Id = Guid.NewGuid(),
            OrgId = command.OrgId,
            UserId = command.UserId,
            RoleId = command.RoleId,
            TeamId = null,
            FolderId = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.RoleBindings.AddAsync(binding, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new CreateRoleBindingResult(binding.Id);
    }
}

/// <summary>
///     Remove a role binding by id.
/// </summary>
/// <param name="db"></param>
public sealed class DeleteRoleBindingCommandHandler(
    IdentityDbContext db) : ICommandHandler<DeleteRoleBindingCommand, DeleteRoleBindingResult>
{
    /// <inheritdoc />
    public async Task<DeleteRoleBindingResult> HandleAsync(
        DeleteRoleBindingCommand command,
        CancellationToken cancellationToken = default)
    {

        var rows = await db.RoleBindings
            .Where(binding => binding.Id == command.BindingId)
            .ExecuteDeleteAsync(cancellationToken);
        if (rows == 0)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "Role binding not found.");
        }

        return new DeleteRoleBindingResult(command.BindingId);
    }
}

/// <summary>List role bindings for a user (all scopes).</summary>
/// <param name="db"></param>
/// <param name="mapper"></param>
public sealed class ListRoleBindingsQueryHandler(
    IdentityDbContext db, ISigilMapper mapper) : ICommandHandler<ListRoleBindingsQuery, IReadOnlyCollection<RoleBindingSummary>>
{
    /// <inheritdoc />
    public Task<IReadOnlyCollection<RoleBindingSummary>> HandleAsync(
        ListRoleBindingsQuery command,
        CancellationToken cancellationToken = default)
    {
        return db.RoleBindings
            .AsNoTracking()
            .Where(binding => binding.UserId == command.UserId)
            .OrderByDescending(binding => binding.CreatedAt)
            .Select(binding => mapper.ToRoleBindingSummary(binding))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(
                static task => (IReadOnlyCollection<RoleBindingSummary>)task.Result,
                cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Current);
    }
}

/// <summary>Result of DeleteRoleBindingCommand.</summary>
/// <param name="BindingId">The binding that was deleted.</param>
public sealed record DeleteRoleBindingResult(Guid BindingId);
