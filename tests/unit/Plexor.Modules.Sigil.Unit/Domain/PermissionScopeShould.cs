// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionScopeShould — exercises the PermissionScope value object's
// RBAC format validator, equality semantics, super-admin wildcard
// check, and constructor error paths. Security-critical: a permissive
// IsWellFormed would let privilege-escalation tokens slip past the
// signer / authorization handler.
// ============================================================================

using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Domain;

/// <summary>
///     Verifies the RBAC permission format invariant
///     (<c>&lt;service&gt;.&lt;resource&gt;.&lt;action&gt;[.&lt;qualifier&gt;]</c>)
///     plus the super-admin wildcard. Every rejection path has at
///     least one positive test so a regression in any branch surfaces
///     immediately.
/// </summary>
public sealed class PermissionScopeShould
{
    /// <summary>Canonical valid forms are accepted: 3-segment
    /// service.resource.action, with the literal <c>*</c> as the
    /// super-admin shortcut.</summary>
    /// <param name="raw"></param>
    [Theory(DisplayName = "Given a well-formed permission string, IsWellFormed returns true")]
    [InlineData("compute.vms.read")]
    [InlineData("iam.users.list")]
    [InlineData("compute.vms.snapshot")]
    [InlineData("compute.vms.read.with_qualifier")]
    [InlineData("*")]
    public void IsWellFormed_ValidFormat_ReturnsTrue(string raw)
    {
        PermissionScope.IsWellFormed(raw).ShouldBeTrue();
    }

    /// <summary>Empty / whitespace / control-char-only strings are
    /// rejected outright — they can't carry any permission meaning.</summary>
    /// <param name="raw"></param>
    [Theory(DisplayName = "Given empty or whitespace input, IsWellFormed returns false")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsWellFormed_EmptyOrWhitespace_ReturnsFalse(string raw)
    {
        PermissionScope.IsWellFormed(raw).ShouldBeFalse();
    }

    /// <summary>Without a dot, the string isn't a permission at all —
    /// even if every char is otherwise legal. Prevents accidental
    /// single-token grants.</summary>
    /// <param name="raw"></param>
    [Theory(DisplayName = "Given input without a dot separator, IsWellFormed returns false")]
    [InlineData("compute")]
    [InlineData("vms")]
    [InlineData("compute_vms_read")]
    public void IsWellFormed_NoDot_ReturnsFalse(string raw)
    {
        PermissionScope.IsWellFormed(raw).ShouldBeFalse();
    }

    /// <summary>Uppercase characters are rejected. The canonical form
    /// is lowercase; mixed-case input must not silently pass the
    /// validator and reach the JWT signer (where case-sensitivity
    /// across services would diverge).</summary>
    /// <param name="raw"></param>
    [Theory(DisplayName = "Given uppercase characters, IsWellFormed returns false")]
    [InlineData("Compute.vms.read")]
    [InlineData("compute.VMS.read")]
    [InlineData("COMPUTE.VMS.READ")]
    public void IsWellFormed_HasUppercase_ReturnsFalse(string raw)
    {
        PermissionScope.IsWellFormed(raw).ShouldBeFalse();
    }

    /// <summary>Punctuation outside the allowed set (<c>.</c> and
    /// <c>_</c>) is rejected — protects against injection of
    /// shell-meta or path-traversal chars via the permission string.</summary>
    /// <param name="raw"></param>
    [Theory(DisplayName = "Given illegal characters, IsWellFormed returns false")]
    [InlineData("compute!vms.read")]
    [InlineData("compute@vms.read")]
    [InlineData("compute#vms.read")]
    [InlineData("compute vms.read")]
    [InlineData("compute-vms.read")]
    [InlineData("compute/vms/read")]
    public void IsWellFormed_IllegalCharacters_ReturnsFalse(string raw)
    {
        PermissionScope.IsWellFormed(raw).ShouldBeFalse();
    }

    /// <summary>Structural dot errors — leading dot, trailing dot, and
    /// consecutive dots — all reject. Each silently passing would let
    /// an attacker smuggle empty segments past the validator.</summary>
    /// <param name="raw"></param>
    [Theory(DisplayName = "Given a structurally malformed dot sequence, IsWellFormed returns false")]
    [InlineData("foo.")]
    [InlineData(".foo")]
    [InlineData("foo..bar")]
    [InlineData("foo...bar")]
    public void IsWellFormed_BadDotSequence_ReturnsFalse(string raw)
    {
        PermissionScope.IsWellFormed(raw).ShouldBeFalse();
    }

    /// <summary>Wildcards other than the exact super-admin literal are
    /// rejected. <c>**</c> has no segment, <c>*.*</c> would otherwise
    /// sneak through as a "valid 3-segment string" and confuse the
    /// authorization handler (which only treats literal <c>*</c> as
    /// super-admin).</summary>
    /// <param name="raw"></param>
    [Theory(DisplayName = "Given super-admin wildcard variants, IsWellFormed returns false")]
    [InlineData("**")]
    [InlineData("*.*")]
    [InlineData("compute.*.read")]
    [InlineData("*.vms.read")]
    public void IsWellFormed_SuperAdminVariants_ReturnsFalse(string raw)
    {
        PermissionScope.IsWellFormed(raw).ShouldBeFalse();
    }

    /// <summary>Only the literal <c>*</c> is recognized as the
    /// super-admin shortcut by <see cref="PermissionScope.IsSuperAdmin" />.
    /// Wildcards in compound forms (<c>*.*</c>, <c>**</c>) are just
    /// invalid permission strings that can never satisfy a real
    /// requirement.</summary>
    [Fact(DisplayName = "Given a '*' permission, IsSuperAdmin returns true")]
    public void IsSuperAdmin_Star_ReturnsTrue()
    {
        var permission = new PermissionScope("*");

        permission.IsSuperAdmin().ShouldBeTrue();
    }

    /// <summary>Any non-<c>*</c> value is NOT the super-admin
    /// shortcut — IsSuperAdmin does a strict ordinal match against the
    /// stored value. Only the literal <c>*</c> short-circuits every
    /// authorization check; variants like <c>*.*</c> are simply
    /// invalid permission strings (rejected by the constructor before
    /// they could be checked here).</summary>
    [Fact(DisplayName = "Given a valid non-'*' permission, IsSuperAdmin returns false")]
    public void IsSuperAdmin_NonStar_ReturnsFalse()
    {
        var permission = new PermissionScope("compute.vms.read");

        permission.IsSuperAdmin().ShouldBeFalse();
    }

    /// <summary>Equality compares the lowercased stored <see cref="PermissionScope.Value" />.
    /// Inputs differing only in surrounding whitespace normalise to
    /// the same canonical string and compare equal — prevents
    /// accidental whitespace drift between token sources (DB column
    /// vs. signer config). Mixed-case inputs cannot reach this
    /// state because <see cref="PermissionScope.IsWellFormed" />
    /// rejects uppercase; case differences must be normalised at the
    /// boundary, not silently accepted by the validator.</summary>
    [Fact(DisplayName = "Two PermissionScopes built from the same input (with surrounding whitespace) compare equal")]
    public void Equality_TrimsWhitespace()
    {
        var padded = new PermissionScope("  compute.vms.read  ");
        var clean = new PermissionScope("compute.vms.read");

        padded.Equals(clean).ShouldBeTrue();
        padded.GetHashCode().ShouldBe(clean.GetHashCode());
        padded.Value.ShouldBe("compute.vms.read");
    }

    /// <summary>The constructor rejects every malformed input —
    /// empty, whitespace, structurally bad, or non-RBAC chars — by
    /// throwing <see cref="IdentityException" /> with the canonical
    /// <c>identity.permission.invalid</c> code. The exception code is
    /// stable across renames so ProblemDetails clients can branch on
    /// it.</summary>
    /// <param name="raw"></param>
    [Theory(DisplayName = "Given an invalid permission string, the constructor throws IdentityException")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("compute")]
    [InlineData("Compute.vms.read")]
    [InlineData("compute!vms.read")]
    [InlineData("foo.")]
    [InlineData("foo..bar")]
    [InlineData("**")]
    [InlineData("*.*")]
    public void Constructor_InvalidString_ThrowsIdentityException(string raw)
    {
        var exception = Should.Throw<IdentityException>(
            () => _ = new PermissionScope(raw));

        exception.Code.ShouldBe(IdentityExceptions.InvalidPermission);
    }

    /// <summary>The constructor rejects <c>null</c> by going through
    /// <see cref="PermissionScope.IsWellFormed" />, which treats
    /// <c>null</c> as whitespace and returns false — so the
    /// constructor raises <see cref="IdentityException" /> with the
    /// canonical <c>identity.permission.invalid</c> code, identical
    /// to any other malformed input. The signature stays non-nullable
    /// (<c>string raw</c>) so the compiler catches it at every call
    /// site; this test pins the runtime behaviour for callers that
    /// bypass the type system with <c>null!</c>.</summary>
    [Fact(DisplayName = "Given a null string, the constructor throws IdentityException")]
    public void Constructor_NullString_Throws()
    {
        var exception = Should.Throw<IdentityException>(
            static () => _ = new PermissionScope(null!));

        exception.Code.ShouldBe(IdentityExceptions.InvalidPermission);
    }
}
