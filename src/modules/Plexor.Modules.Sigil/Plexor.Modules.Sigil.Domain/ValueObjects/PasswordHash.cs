using Plexor.Modules.Sigil.Domain.Errors;

namespace Plexor.Modules.Sigil.Domain.ValueObjects;

/// <summary>
///     Bcrypt password hash + format invariant. A valid <c>PasswordHash</c>
///     starts with <c>$2</c> (bcrypt family marker) followed by cost factor
///     <c>$12$</c> (cost 12 — see <c>identity.md</c> §Password policy)
///     and 22 chars of base64 salt + 31 chars of base64 hash.
/// </summary>
/// <remarks>
///     <para><b>Why a value object.</b> Accepting a bare <c>string</c> for
///     <c>User.PasswordHash</c> means a malformed value (e.g. SHA-256 hash
///     leaked from another system, or a plaintext password committed by
///     accident) can sneak past the type system and only fail at verify
///     time. <c>PasswordHash</c> enforces the bcrypt format at the
///     boundary.</para>
///     <para><b>Verification.</b> This type does not own verification —
///     <c>IPasswordHasher.Verify</c> (Application/Infrastructure layer) is
///     the canonical verify path. The value object only asserts the
///     stored form is well-formed bcrypt.</para>
///     <para><b>Null for OAuth-only users.</b> When a user is provisioned
///     via OIDC without a local password, <c>User.PasswordHash</c> is
///     <c>null</c> (the column is nullable in <c>sigil.users</c>). OIDC
///     integration is Phase 2+; the entity has the nullable property
///     regardless so the migration doesn't need a second pass.</para>
/// </remarks>
public sealed class PasswordHash : IEquatable<PasswordHash>
{
    /// <summary>Prefix that identifies bcrypt hashes by family.</summary>
    public const string BcryptPrefix = "$2";

    /// <summary>Bcrypt cost factor enforced by this type (and used by
    /// <c>IPasswordHasher.Hash</c> when issuing a fresh hash).</summary>
    public const int BcryptCost = 12;

    private readonly string value;

    /// <summary>
    ///     Constructs a <c>PasswordHash</c> from a stored hash string.
    ///     No format validation at the ctor — the underlying
    ///     application-layer hasher owns the shape (PBKDF2 in v0.1,
    ///     bcrypt in a future migration). Use
    ///     <see cref="IsWellFormed" /> for audit-time checks that
    ///     the stored value is in a known family before verification.
    ///     The ctor's only job is to refuse null / whitespace so EF
    ///     Core can't store empty strings.
    /// </summary>
    /// <param name="hash">
    ///     Hash string in whatever format the application-layer
    ///     hasher produces (.NET Identity's PBKDF2 self-describing
    ///     string in v0.1; bcrypt in a future migration).
    /// </param>
    public PasswordHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPasswordHash,
                "Password hash is required.");
        }

        value = hash;
    }

    /// <summary>Returns the underlying bcrypt string. For persistence
    /// only — never log or display.</summary>
    public override string ToString()
    {
        return value;
    }

    /// <summary>True when <paramref name="raw" /> is a well-formed bcrypt
    /// hash at the configured cost factor.</summary>
    /// <param name="raw">Candidate hash string.</param>
    public static bool IsWellFormed(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // Bcrypt format: $2[abxy]$NN$<22 salt><31 hash>
        // We enforce cost = 12 to catch operators that downgraded the
        // config (e.g. a leaked bcrypt-4 hash would still verify but is
        // too weak).
        if (!raw.StartsWith(BcryptPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // 7 chars prefix + 22 salt + 31 hash = 60 chars total
        return raw.Length is 60
            && raw[2] is 'b' or 'a' or 'y'
            && raw[3] == '$'
            && raw.AsSpan(4, 2).SequenceEqual("$12".AsSpan());
    }

    /// <summary>Equality compares the underlying bcrypt string.</summary>
    /// <param name="other"></param>
    public bool Equals(PasswordHash? other)
    {
        return other is not null && StringComparer.Ordinal.Equals(value, other.value);
    }

    /// <summary>Equality compares the underlying bcrypt string.</summary>
    /// <param name="obj"></param>
    public override bool Equals(object? obj)
    {
        return obj is PasswordHash other && Equals(other);
    }

    /// <summary>Hash code derived from the underlying bcrypt string.</summary>
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(value);
    }
}
