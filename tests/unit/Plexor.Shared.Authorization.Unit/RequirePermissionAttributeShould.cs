// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RequirePermissionAttributeShould — verifies the attribute produces a
// well-formed permission policy name and trims whitespace.
// ============================================================================

using Plexor.Shared.Authorization;
using Shouldly;
using Xunit;

namespace Plexor.Shared.Authorization.Unit;

/// <summary>
///     Tests <see cref="RequirePermissionAttribute" /> constructor
///     logic: trimming, multi-permission encoding, empty-args rejection.
///     The policy string format is the contract that
///     <see cref="PermissionPolicyProvider" /> consumes.
/// </summary>
public sealed class RequirePermissionAttributeShould
{
    /// <summary>Single permission encodes to <c>permission:&lt;name&gt;</c>.</summary>
    [Fact(DisplayName = "Given one permission, the policy string is 'permission:<name>'")]
    public void SinglePermissionGeneratesPermissionPrefixedPolicyString()
    {
        var attribute = new RequirePermissionAttribute("vms.read");

        attribute.Policy.ShouldBe("permission:vms.read");
    }

    /// <summary>Multiple permissions encode to a comma-separated
    /// permission policy string that the provider parses back into
    /// one requirement per token.</summary>
    [Fact(DisplayName = "Given multiple permissions, the policy string is 'permission:a,b,c'")]
    public void MultiplePermissionsGenerateCsvPolicyString()
    {
        var attribute = new RequirePermissionAttribute(RwPermissions);

        attribute.Policy.ShouldBe("permission:vms.read,vms.write,vms.delete");
    }

    /// <summary>Permissions array exposes the original (trimmed)
    /// tokens so tests / analyzers can audit which permissions are
    /// gated without parsing the policy string.</summary>
    [Fact(DisplayName = "Permissions array exposes the trimmed tokens")]
    public void PermissionsArrayExposesTrimmedTokens()
    {
        var attribute = new RequirePermissionAttribute("vms.read", "  vms.write  ");

        attribute.Permissions.ShouldBe(TrimmedExpected);
    }

    /// <summary>Empty / whitespace-only token lists throw — leaving an
    /// endpoint unguarded at runtime is the failure mode we're trying
    /// to prevent.</summary>
    [Fact(DisplayName = "Given an empty list of permissions, the constructor throws")]
    public void EmptyPermissionsListThrows()
    {
        Should.Throw<System.ArgumentException>(
            () => new RequirePermissionAttribute());
    }

    /// <summary>The <see cref="AttributeUsageAttribute" /> allows the
    /// attribute on both classes and methods with
    /// <see cref="AttributeTargets.Method" /> + <c>| Class</c>; this is
    /// what makes controller-level gating work without a per-action
    /// attribute on every method.</summary>
    [Fact(DisplayName = "AttributeTargets include Class and Method with AllowMultiple = true")]
    public void AttributeTargetsAllowClassAndMethod()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RequirePermissionAttribute),
            typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeTrue();
        ((int)(usage.ValidOn & AttributeTargets.Class)).ShouldNotBe(0);
        ((int)(usage.ValidOn & AttributeTargets.Method)).ShouldNotBe(0);
    }

    private static readonly string[] RwPermissions =
    {
        "vms.read", "vms.write", "vms.delete",
    };

    private static readonly string[] TrimmedExpected =
    {
        "vms.read", "vms.write",
    };
}
