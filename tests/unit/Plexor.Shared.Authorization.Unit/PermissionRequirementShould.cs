// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionAuthorizationHandlerShould — behavioural tests for the
// permission handler. Uses synthetic ClaimsPrincipals to simulate
// different caller-permission combinations.
// ============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Plexor.Shared.Authorization.Unit;

/// <summary>
///     Exercises <see cref="PermissionAuthorizationHandler" /> against
///     the four key AND-semantics paths: empty claims, single match,
///     missing single, multiple requirements with one missing.
/// </summary>
public sealed class PermissionAuthorizationHandlerShould
{
    /// <summary>When the principal has no <c>permission</c> claims at
    /// all, the requirement must not succeed.</summary>
    [Fact(DisplayName = "Given a caller with no permission claims, when authorizing, then the requirement fails")]
    public async Task AnonymousPrincipalFailsPermissionCheckAsync()
    {
        var handler = new PermissionAuthorizationHandler(NullLogger<PermissionAuthorizationHandler>.Instance);
        var requirement = new PermissionRequirement("vms.read");
        var context = new AuthorizationHandlerContext(
            [requirement],
            AnonymousUser(),
            resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    /// <summary>When the principal has the required permission, the
    /// requirement must succeed.</summary>
    [Fact(DisplayName = "Given a caller with vms.read, when authorizing vms.read, then the requirement succeeds")]
    public async Task MatchingPermissionSucceedsAsync()
    {
        var handler = new PermissionAuthorizationHandler(NullLogger<PermissionAuthorizationHandler>.Instance);
        var requirement = new PermissionRequirement("vms.read");
        var context = new AuthorizationHandlerContext(
            [requirement],
            ClaimsPrincipal("vms.read"),
            resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    /// <summary>When the principal has a permission but it's different
    /// from what's required, the requirement fails.</summary>
    [Fact(DisplayName = "Given a caller with vms.write only, when authorizing vms.read, then the requirement fails")]
    public async Task WrongPermissionFailsAsync()
    {
        var handler = new PermissionAuthorizationHandler(NullLogger<PermissionAuthorizationHandler>.Instance);
        var requirement = new PermissionRequirement("vms.read");
        var context = new AuthorizationHandlerContext(
            [requirement],
            ClaimsPrincipal("vms.write"),
            resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    /// <summary>When the principal has all permissions referenced by
    /// multiple requirements, every requirement in the policy succeeds.</summary>
    [Fact(DisplayName = "Given a caller with vms.read and vms.write, when authorizing both, then both succeed")]
    public async Task MultiplePermissionsAllPresentSucceedsAsync()
    {
        var handler = new PermissionAuthorizationHandler(NullLogger<PermissionAuthorizationHandler>.Instance);
        var read = new PermissionRequirement("vms.read");
        var write = new PermissionRequirement("vms.write");
        var context = new AuthorizationHandlerContext(
            [read, write],
            ClaimsPrincipal("vms.read", "vms.write"),
            resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    /// <summary>When the principal is missing one of multiple required
    /// permissions, the missing one fails — AND semantics across
    /// requirements attached to the same policy.</summary>
    [Fact(DisplayName = "Given a caller with only vms.read, when authorizing vms.read AND vms.write, then the policy fails")]
    public async Task MultiplePermissionsOneMissingFailsAsync()
    {
        var handler = new PermissionAuthorizationHandler(NullLogger<PermissionAuthorizationHandler>.Instance);
        var read = new PermissionRequirement("vms.read");
        var write = new PermissionRequirement("vms.write");
        var context = new AuthorizationHandlerContext(
            [read, write],
            ClaimsPrincipal("vms.read"),
            resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    /// <summary>Permission comparison is case-insensitive — caller can
    /// have either <c>vms.read</c> or <c>VMS.Read</c> on the claim and
    /// still satisfy a lower-case requirement.</summary>
    [Fact(DisplayName = "Permission comparison is case-insensitive")]
    public async Task PermissionMatchIsCaseInsensitiveAsync()
    {
        var handler = new PermissionAuthorizationHandler(NullLogger<PermissionAuthorizationHandler>.Instance);
        var requirement = new PermissionRequirement("vms.read");
        var context = new AuthorizationHandlerContext(
            [requirement],
            ClaimsPrincipal("VMS.READ"),
            resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    private static ClaimsPrincipal AnonymousUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    private static ClaimsPrincipal ClaimsPrincipal(params string[] permissions)
    {
        var identity = new ClaimsIdentity(
            permissions.Select(static p => new System.Security.Claims.Claim(
                AuthorizationClaimNames.PermissionClaim,
                p)),
            authenticationType: "TestBearer");
        return new ClaimsPrincipal(identity);
    }
}
