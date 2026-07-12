using System.Text.RegularExpressions;
using Plexor.Modules.Identity.Domain.Errors;

namespace Plexor.Modules.Identity.Domain.ValueObjects;

/// <summary>
///     RFC 5322-simplified email address. Lowercased on construction so
///     lookups by email are case-insensitive regardless of how the caller
///     typed it.
/// </summary>
/// <remarks>
///     <para><b>Validation.</b> Single regex check on construction —
///     <c>local@domain.tld</c> with no whitespace, no quoted strings, no
///     IP-literal domains. Matches what 99 % of validation does; if
///     a more aggressive check is needed later, replace the regex with
///     a real RFC 5322 parser (don't).</para>
///     <para><b>Why value object.</b> Email is meaningless as a bare
///     string — every consumer needs the validation + normalization. A
///     dedicated type catches <c>user.Email.ToString()</c> mixing with
///     user-controlled input at compile time.</para>
/// </remarks>
public sealed class Email : IEquatable<Email>
{
    // Regex match timeout — prevents ReDoS attacks via pathological
    // input. 100 ms is well above the cost of a legitimate address
    // and orders of magnitude below any user-perceived latency.
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly Regex Pattern = new(
        @"^[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        MatchTimeout);

    private readonly string value;

    /// <summary>The validated, normalized email string.</summary>
    public string Value
    {
        get
        {
            return value;
        }
    }

    /// <summary>
    ///     Constructs an email from a raw string. Throws
    ///     <see cref="IdentityException" /> on invalid input.
    /// </summary>
    /// <param name="raw">Raw email input — trimmed + lowercased before validation.</param>
    public Email(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidEmail,
                "Email cannot be null or whitespace.");
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (!Pattern.IsMatch(normalized))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidEmail,
                $"'{raw}' is not a valid email address.");
        }

        value = normalized;
    }

    /// <summary>Returns the lowercase normalized form.</summary>
    public override string ToString()
    {
        return value;
    }

    /// <summary>Equality compares the underlying normalized string.</summary>
    public bool Equals(Email? other)
    {
        return other is not null && StringComparer.Ordinal.Equals(value, other.value);
    }

    /// <summary>Equality compares the underlying normalized string.</summary>
    public override bool Equals(object? obj)
    {
        return obj is Email other && Equals(other);
    }

    /// <summary>Hash code derived from the underlying normalized string.</summary>
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(value);
    }
}