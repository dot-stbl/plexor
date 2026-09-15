// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorSigilApiServiceCollectionExtensions — DI registration for the
// Sigil API layer. Hosts compose it as
//   builder.Services.AddPlexorSigilApi();
// after AddSigilApplicationCore + AddSigilInfrastructureCore.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Errors;
using Plexor.Modules.Sigil.Infrastructure.Mappers;
using Plexor.Modules.Sigil.Infrastructure.Users;

namespace Plexor.Modules.Sigil.Api;

/// <summary>
///     DI registration helpers for the Sigil API project. Wires
///     authentication command/query handlers (scoped per-request) and
///     the global <see cref="IdentityExceptionHandler" /> that maps
///     domain exceptions to RFC 7807 ProblemDetails responses.
/// </summary>
public static class PlexorSigilApiServiceCollectionExtensions
{
    /// <summary>
    ///     Register the Sigil API layer's services. Application-layer
    ///     interfaces (<see cref="Application.Auth.IPermissionResolver" />,
    ///     <see cref="Application.Auth.ITokenIssuer" />,
    ///     <see cref="Application.Auth.IPasswordHasher" />) are
    ///     expected to be registered upstream by
    ///     <c>AddSigilInfrastructureCore</c>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddPlexorSigilApi(this IServiceCollection services)
    {

        // Handlers — scoped per request so DbContext is reused across
        // the multi-DB-roundtrip pipeline (login does user lookup,
        // password verify, failed-login update, refresh issue,
        // permission resolve, access issue). Bound via ICommandHandler so
        // the controllers can be unit-tested with NSubstitute.
        services.AddScoped<ICommandHandler<LoginCommand, LoginResult>, LoginCommandHandler>();
        services.AddScoped<ICommandHandler<RefreshCommand, LoginResult>, RefreshCommandHandler>();
        services.AddScoped<ICommandHandler<LogoutCommand, LogoutResult>, LogoutCommandHandler>();
        services.AddScoped<ICommandHandler<MeQuery, MeResult>, MeQueryHandler>();

        // User CRUD handlers — same scoped lifetime (DbContext reuse).
        services.AddScoped<ICommandHandler<CreateUserCommand, CreateUserResult>, CreateUserCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateUserCommand, UserSummary>, UpdateUserCommandHandler>();
        services.AddScoped<ICommandHandler<DisableUserCommand, UserSummary>, DisableUserCommandHandler>();
        services.AddScoped<ICommandHandler<ChangePasswordCommand, ChangePasswordResult>, ChangePasswordCommandHandler>();
        services.AddScoped<ICommandHandler<GetUserQuery, UserSummary>, GetUserQueryHandler>();
        services.AddScoped<ICommandHandler<ListUsersQuery, UserPage>, ListUsersQueryHandler>();

        // Role + role-binding CRUD handlers.
        services.AddScoped<ICommandHandler<CreateRoleCommand, CreateRoleResult>, CreateRoleCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateRoleCommand, RoleSummary>, UpdateRoleCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteRoleCommand, DeleteRoleResult>, DeleteRoleCommandHandler>();
        services.AddScoped<ICommandHandler<GetRoleQuery, RoleSummary>, GetRoleQueryHandler>();
        services.AddScoped<ICommandHandler<ListRolesQuery, IReadOnlyCollection<RoleSummary>>, ListRolesQueryHandler>();
        services.AddScoped<ICommandHandler<CreateRoleBindingCommand, CreateRoleBindingResult>, CreateRoleBindingCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteRoleBindingCommand, DeleteRoleBindingResult>, DeleteRoleBindingCommandHandler>();
        services.AddScoped<ICommandHandler<ListRoleBindingsQuery, IReadOnlyCollection<RoleBindingSummary>>, ListRoleBindingsQueryHandler>();

        // Credential (API key + SSH key) handlers.
        services.AddScoped<ICommandHandler<IssueApiKeyCommand, IssueApiKeyResult>, IssueApiKeyCommandHandler>();
        services.AddScoped<ICommandHandler<RevokeApiKeyCommand, RevokeApiKeyResult>, RevokeApiKeyCommandHandler>();
        services.AddScoped<ICommandHandler<ListApiKeysQuery, IReadOnlyCollection<ApiKeySummary>>, ListApiKeysQueryHandler>();
        services.AddScoped<ICommandHandler<AddSshKeyCommand, SshKeySummary>, AddSshKeyCommandHandler>();
        services.AddScoped<ICommandHandler<RevokeSshKeyCommand, RevokeSshKeyResult>, RevokeSshKeyCommandHandler>();
        services.AddScoped<ICommandHandler<ListSshKeysQuery, IReadOnlyCollection<SshKeySummary>>, ListSshKeysQueryHandler>();

        // User lookup — read-only, scoped.
        services.AddScoped<IUserLookup, EfUserLookup>();

        // Mapperly source-generated DTO mapper. Singleton — generated
        // methods are stateless. Interface (ISigilMapper) decouples
        // handlers from the concrete generated class.
        services.AddSingleton<ISigilMapper, SigilMapper>();

        // Global exception handler. Order matters: this one runs
        // first; non-IdentityException passes through to the default
        // 500 handler.
        services.AddExceptionHandler<IdentityExceptionHandler>();

        return services;
    }
}
