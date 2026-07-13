// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionPolicyProviderShould — tests the dynamic
// IAuthorizationPolicyProvider: prefix routing, multi-permission
// splits, token trimming, and the null/unknown-policy fallback.
// ============================================================================

using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Plexor.Shared.Authorization.Unit;

/// <summary>
///     Verifies the dynamic policy name → policy resolution in
///     <see cref="PermissionPolicyProvider" />. Policies whose name
///     doesn't start with <see cref="AuthorizationPolicyNames.Prefix" />
///     are out-of-scope and the provider must return <c>null</c> so
///     other policy resolvers can try.
/// </summary>
public sealed class PermissionPolicyProviderShould
{
    /// <summary>Non-permission policy names must fall through (return
    /// <c>null</c>) so the rest of the framework can try other
    /// providers.</summary>
    [Fact(DisplayName = "Given a policy name without the permission prefix, GetPolicyAsync returns null")]
    public async Task UnknownPrefixReturnsNull()
    {
        var provider = new PermissionPolicyProvider(NullLogger<PermissionPolicyProvider>.Instance);

        var policy = await provider.GetPolicyAsync("something-else");

        policy.ShouldBeNull();
    }

    /// <summary>A permission policy name with a single permission
    /// builds a policy containing exactly that one requirement.</summary>
    [Fact(DisplayName = "Given 'permission:foo', GetPolicyAsync returns a policy with one requirement")]
    public async Task SinglePermissionBuildsOneRequirement()
    {
        var provider = new PermissionPolicyProvider(NullLogger<PermissionPolicyProvider>.Instance);

        var policy = await provider.GetPolicyAsync("permission:vms.read");

        policy.ShouldNotBeNull();
        var requirement = policy!.Requirements.OfType<PermissionRequirement>().Single();
        requirement.Permission.ShouldBe("vms.read");
    }

    /// <summary>A comma-separated permission policy name builds one
    /// requirement per token.</summary>
    [Fact(DisplayName = "Given 'permission:foo,bar', GetPolicyAsync returns a policy with two requirements")]
    public async Task MultiplePermissionsBuildMultipleRequirements()
    {
        var provider = new PermissionPolicyProvider(NullLogger<PermissionPolicyProvider>.Instance);

        var policy = await provider.GetPolicyAsync("permission:vms.read,vms.write");

        policy.ShouldNotBeNull();
        var requirements = policy!.Requirements.OfType<PermissionRequirement>().ToList();
        requirements.Count.ShouldBe(2);
        requirements.Select(static r => r.Permission).ShouldBe(TwoPermissions);
    }

    /// <summary>Leading/trailing whitespace on each comma-separated
    /// token is silently trimmed so copy-paste from config files
    /// doesn't break authorization.</summary>
    [Fact(DisplayName = "Whitespace around tokens is trimmed before resolution")]
    public async Task TokenWhitespaceIsTrimmed()
    {
        var provider = new PermissionPolicyProvider(NullLogger<PermissionPolicyProvider>.Instance);

        var policy = await provider.GetPolicyAsync("permission:vms.read , vms.write ");

        var requirements = policy!.Requirements.OfType<PermissionRequirement>().ToList();
        requirements.Select(static r => r.Permission).ShouldBe(TwoPermissions);
    }

    /// <summary>A permission policy with no permissions after the prefix
    /// (e.g. <c>permission:</c>) returns null so the framework can fall
    /// through rather than building a do-nothing policy.</summary>
    [Fact(DisplayName = "Given an empty permission list, GetPolicyAsync returns null")]
    public async Task EmptyPermissionListReturnsNull()
    {
        var provider = new PermissionPolicyProvider(NullLogger<PermissionPolicyProvider>.Instance);

        var policy = await provider.GetPolicyAsync("permission:");

        policy.ShouldBeNull();
    }

    /// <summary>Default policy request returns a non-null policy
    /// requiring authenticated users — mirrors the framework default
    /// so adding this provider doesn't accidentally relax
    /// <c>[AllowAnonymous]</c> behaviour.</summary>
    [Fact(DisplayName = "GetDefaultPolicyAsync returns a non-null authentication policy")]
    public async Task DefaultPolicyRequiresAuthentication()
    {
        var provider = new PermissionPolicyProvider(NullLogger<PermissionPolicyProvider>.Instance);

        var policy = await provider.GetDefaultPolicyAsync();

        policy.ShouldNotBeNull();
        policy!.Requirements.Count.ShouldBeGreaterThan(0);
    }

    private static readonly string[] TwoPermissions =
    [
        "vms.read", "vms.write",
    ];
}
