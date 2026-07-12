using Plexor.Modules.Identity.Domain.Errors;

namespace Plexor.Modules.Identity.Domain.ValueObjects;

/// <summary>
///     Single RBAC permission string. Format:
///     <c>&lt;service&gt;.&lt;resource&gt;.&lt;action&gt;[.&lt;qualifier&gt;]</c>.
///     Special token <c>"*"</c> = super-admin (grants all permissions
///     across all services).
/// </summary>
/// <remarks>
///     <para><b>Why a value object.</b> Permission strings flow through
///     JWT claims, database TEXT[] arrays, controller attributes, and
///     authorization checks. The format invariant ("must be lowercase,
///     no whitespace, must contain at least one dot OR be the literal
///     '*'") belongs at the boundary; a dedicated type catches typos
///     and string drift between layers.</para>
///     <para><b>No wildcards.</b> <c>PermissionScope</c> matches strings by
///     equality — there is no <c>compute.vms.*</c> wildcard in v0.1.
///     Adding a new action (e.g. <c>compute.vms.snapshot</c>) means
///     explicitly adding that string to the role's permissions list;
///     no implicit cascade. See <c>architecture/identity.md</c> §RBAC
///     for rationale.</para>
///     <para><b>Wildcard check.</b> <see cref="IsSuperAdmin" /> returns
///     true when the permission is <c>*</c>. Authorization code uses
///     this to short-circuit all checks.</para>
/// </remarks>
public sealed class PermissionScope : IEquatable<PermissionScope>
{
    /// <summary>The wildcard permission granted to super-admin roles.</summary>
    public const string SuperAdmin = "*";

    private readonly string value;

    /// <summary>The validated, lowercased permission string.</summary>
    public string Value
    {
        get
        {
            return value;
        }
    }

    /// <summary>
    ///     Constructs a permission from a string. Trims + lowercases.
    ///     Throws <see cref="IdentityException" /> on invalid format.
    /// </summary>
    /// <param name="raw">Raw permission string from caller.</param>
    public PermissionScope(string raw)
    {
        if (!IsWellFormed(raw))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                $"'{raw}' is not a valid permission. " +
                "Expected '<service>.<resource>.<action>[.<qualifier>]' or '*'.");
        }

        value = raw.Trim().ToLowerInvariant();
    }

    /// <summary>Returns the lowercased permission string.</summary>
    public override string ToString() { return value; }

    /// <summary>True when this permission is the super-admin wildcard.</summary>
    public bool IsSuperAdmin()
    {
        return StringComparer.Ordinal.Equals(value, SuperAdmin);
    }

    /// <summary>True when <paramref name="raw" /> matches the permission
    /// format (<c>&lt;service&gt;.&lt;resource&gt;.&lt;action&gt;[.&lt;qualifier&gt;]</c>
///     or the literal <c>*</c>).</summary>
    /// <param name="raw">Candidate permission string.</param>
    public static bool IsWellFormed(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim().ToLowerInvariant();
        if (StringComparer.Ordinal.Equals(trimmed, SuperAdmin))
        {
            return true;
        }

        // At least one dot; no whitespace; chars in [a-z0-9._*]
        if (!trimmed.Contains('.'))
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not '.' && ch is not '_' && ch is not '*')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Equality compares the lowercased permission string.</summary>
    public bool Equals(PermissionScope? other)
    {
        return other is not null && StringComparer.Ordinal.Equals(value, other.value);
    }

    /// <summary>Equality compares the lowercased permission string.</summary>
    public override bool Equals(object? obj)
    {
        return obj is PermissionScope other && Equals(other);
    }

    /// <summary>Hash code derived from the lowercased permission string.</summary>
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(value);
    }
}