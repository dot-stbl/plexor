// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UserCommandHandlers — CreateUser / UpdateUser / DisableUser /
// GetUser / ListUsers. Co-located in one file because every handler
// depends on the same IdentityDbContext + password hasher + refresh
// store and they're < 80 lines each.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Mappers;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Users;

/// <summary>
///     Create a user. Hashes the password, persists the entity, and
///     returns the new id. Email uniqueness is enforced by the
///     <c>ix_sigil_users_org_id_email</c> index — duplicates surface
///     as <see cref="IdentityException" /> with
///     <see cref="IdentityExceptions.InvalidEmail" />.
/// </summary>
/// <param name="db"></param>
/// <param name="passwordHasher"></param>
public sealed class CreateUserCommandHandler(
    IdentityDbContext db,
    IPasswordHasher passwordHasher) : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    /// <inheritdoc />
    public async Task<CreateUserResult> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(command.Email) || !command.Email.Contains('@', StringComparison.Ordinal))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidEmail,
                "Email is required and must contain a domain.");
        }

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPasswordHash,
                "Password must be at least 8 characters.");
        }

        var email = new Email(command.Email);
        var existing = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.OrgId == command.OrgId && user.Email.Value == email.Value,
                cancellationToken);
        if (existing is not null)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidEmail,
                "Email is already in use within this org.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrgId = command.OrgId,
            Email = email,
            DisplayName = command.DisplayName,
            Status = "active",
            PasswordHash = new PasswordHash(passwordHasher.HashPassword(
                new User { Id = Guid.NewGuid() }, command.Password)),
            FailedLoginCount = 0,
            LockedUntil = null,
            LastLoginAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await db.Users.AddAsync(user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new CreateUserResult(user.Id);
    }
}

/// <summary>
///     Update an existing user's display metadata + status. Email
///     changes are rejected with
///     <see cref="IdentityExceptions.InvalidEmail" />.
/// </summary>
/// <param name="db"></param>
public sealed class UpdateUserCommandHandler(
    IdentityDbContext db,
    ISigilMapper mapper) : ICommandHandler<UpdateUserCommand, UserSummary>
{
    /// <inheritdoc />
    public async Task<UserSummary> HandleAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {

        var exists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == command.UserId, cancellationToken);
        if (!exists)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "User not found.");
        }

        if (command.Status is { } newStatus && newStatus is not "active" and not "suspended")
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                $"Unknown status '{command.Status}'; expected 'active' or 'suspended'.");
        }

        await db.Users
            .Where(u => u.Id == command.UserId)
            .ExecuteUpdateAsync(
                setters =>
                {
                    if (command.DisplayName is not null)
                    {
                        setters.SetProperty(u => u.DisplayName, command.DisplayName);
                    }
                    if (command.Status is not null)
                    {
                        setters.SetProperty(u => u.Status, command.Status);
                    }
                    setters.SetProperty(u => u.UpdatedAt, DateTimeOffset.UtcNow);
                },
                cancellationToken);

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Id == command.UserId)
            .Select(u => mapper.ToUserSummary(u))
            .FirstAsync(cancellationToken);
    }
}

/// <summary>
///     Soft-delete a user: status = "suspended" + revoke every
///     refresh token in every family the user owns.
/// </summary>
/// <param name="db"></param>
/// <param name="refreshTokens"></param>
public sealed class DisableUserCommandHandler(
    IdentityDbContext db,
    IRefreshTokenStore refreshTokens,
    ISigilMapper mapper) : ICommandHandler<DisableUserCommand, UserSummary>
{
    /// <inheritdoc />
    public async Task<UserSummary> HandleAsync(
        DisableUserCommand command,
        CancellationToken cancellationToken = default)
    {

        if (!await db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == command.UserId, cancellationToken)
)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "User not found.");
        }

        await db.Users
            .Where(u => u.Id == command.UserId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.Status, "suspended")
                    .SetProperty(u => u.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        // Revoke every refresh token the user owns. Done by walking
        // the user's token family ids and nuking each one.
        var familyIds = await db.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == command.UserId)
            .Select(token => token.FamilyId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        foreach (var familyId in familyIds)
        {
            await refreshTokens.RevokeFamilyAsync(familyId, cancellationToken);
        }

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Id == command.UserId)
            .Select(u => mapper.ToUserSummary(u))
            .FirstAsync(cancellationToken);
    }
}

/// <summary>
///     Fetch a single user summary by id.
/// </summary>
/// <param name="db"></param>
public sealed class GetUserQueryHandler(
    IdentityDbContext db,
    ISigilMapper mapper) : ICommandHandler<GetUserQuery, UserSummary>
{
    /// <inheritdoc />
    public async Task<UserSummary> HandleAsync(
        GetUserQuery query,
        CancellationToken cancellationToken = default)
    {

        var summary = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == query.UserId)
            .Select(u => mapper.ToUserSummary(u))
            .FirstOrDefaultAsync(cancellationToken);

        return summary ?? throw new IdentityException(
            IdentityExceptions.InvalidCredentials,
            "User not found.");
    }
}

/// <summary>
///     Page through users within an organization. Page is 1-based;
///     PageSize is bounded to 1..200 to keep query latency bounded.
/// </summary>
/// <param name="db"></param>
public sealed class ListUsersQueryHandler(
    IdentityDbContext db, ISigilMapper mapper) : ICommandHandler<ListUsersQuery, UserPage>
{
    /// <summary>Hard cap on page size — protects against accidental
    /// full-table dumps.</summary>
    private const int MaxPageSize = 200;

    /// <inheritdoc />
    public async Task<UserPage> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken = default)
    {

        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, query.Page);

        var baseQuery = db.Users.AsNoTracking().Where(u => u.OrgId == query.OrgId);
        var total = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => mapper.ToUserSummary(u))
            .ToArrayAsync(cancellationToken);

        return new UserPage(items, total, page, pageSize);
    }
}
