// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HttpContextCurrentUserShould — exercises the ICurrentUser adapter
// that reads claims from the per-request HttpContext. No DB, no
// I/O — the only collaborator is IHttpContextAccessor, exercised
// against DefaultHttpContext.
// ============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Infrastructure.CurrentUser;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Infrastructure;

/// <summary>
///     Behavioural tests for <see cref="HttpContextCurrentUser" />.
///     Each test wires a real <see cref="DefaultHttpContext" /> via
///     an <see cref="IHttpContextAccessor" /> stub and asserts the
///     anonymous defaults or the claim-resolved values.
/// </summary>
public sealed class HttpContextCurrentUserShould
{
    /// <summary>Verifies that an unauthenticated context returns the
    /// anonymous defaults — empty GUIDs, empty collections, and
    /// <c>IsService</c> = <c>false</c>.</summary>
    [Fact(DisplayName = "Given unauthenticated context, when properties read, then return anonymous defaults")]
    public void AnonymousContextReturnsDefaults()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext());
        var user = new HttpContextCurrentUser(accessor);

        user.UserId.ShouldBe(Guid.Empty);
        user.TenantId.ShouldBe(Guid.Empty);
        user.ProjectId.ShouldBeNull();
        user.Roles.ShouldBeEmpty();
        user.Permissions.ShouldBeEmpty();
        user.IsService.ShouldBeFalse();
    }

    /// <summary>Verifies that the sub + tid claims resolve to the
    /// <see cref="ICurrentUser.UserId" /> and
    /// <see cref="ICurrentUser.TenantId" /> properties. The pid claim
    /// (optional) resolves to <see cref="ICurrentUser.ProjectId" />.</summary>
    [Fact(DisplayName = "Given principal with sub + tid + pid claims, when properties read, then UserId, TenantId and ProjectId resolve")]
    public void AuthenticatedContextResolvesCoreClaims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [
                new Claim(IdentityClaims.UserId, userId.ToString()),
                new Claim(IdentityClaims.TenantId, tenantId.ToString()),
                new Claim(IdentityClaims.ProjectId, projectId.ToString()),
            ],
            authenticationType: "test");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        var user = new HttpContextCurrentUser(accessor);

        user.UserId.ShouldBe(userId);
        user.TenantId.ShouldBe(tenantId);
        user.ProjectId.ShouldBe(projectId);
    }

    /// <summary>Verifies that <see cref="ICurrentUser.Roles" /> and
    /// <see cref="ICurrentUser.Permissions" /> aggregate all
    /// <c>role</c> / <c>permission</c> claims on the principal.</summary>
    [Fact(DisplayName = "Given principal with multiple role + permission claims, when properties read, then collections aggregate")]
    public void AuthenticatedContextAggregatesRolesAndPermissions()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(IdentityClaims.Roles, "admin"),
                new Claim(IdentityClaims.Roles, "editor"),
                new Claim(IdentityClaims.Permission, "compute.vms.read"),
                new Claim(IdentityClaims.Permission, "compute.vms.write"),
                new Claim(IdentityClaims.Permission, "iam.users.read"),
            ],
            authenticationType: "test");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        var user = new HttpContextCurrentUser(accessor);

        user.Roles.ShouldBe(["admin", "editor"]);
        user.Permissions.ShouldBe(["compute.vms.read", "compute.vms.write", "iam.users.read"]);
    }

    /// <summary>Verifies that <see cref="ICurrentUser.IsService" />
    /// is <c>true</c> when the principal carries an
    /// <c>is_service</c> claim with value <c>"true"</c> — the API-key
    /// auth path sets this flag so authorization can distinguish
    /// machine callers from human users.</summary>
    [Fact(DisplayName = "Given is_service claim, when IsService read, then returns true")]
    public void IsServiceFlagResolvesFromClaim()
    {
        var identity = new ClaimsIdentity(
            [new Claim(IdentityClaims.IsService, "true")],
            authenticationType: "test");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        var user = new HttpContextCurrentUser(accessor);

        user.IsService.ShouldBeTrue();
    }
}
