namespace Plexor.Modules.Sigil.Application.Abstractions;

/// <summary>
///     Loads the role names assigned to a user. Used by auth handlers to
///     issue JWTs that include role claims. Per-UserId; one DB roundtrip
///     per call.
/// </summary>
/// <remarks>
///     <para><b>Why names, not bindings.</b> The JWT issuer only needs the
///     role names (one <c>role</c> claim per name). Walking to the
///     bindings and joining to the roles table inline in every auth
///     handler kept that loop byte-identical across Login + Refresh —
///     a textbook port extraction.</para>
///     <para><b>Distinct, unordered.</b> A user may have multiple
///     bindings for the same role at different scopes (e.g. org-wide
///     plus team-scoped); the loader deduplicates by name so the
///     JWT contains one claim per role, not per binding.</para>
/// </remarks>
public interface IRoleNameLoader
{
    /// <summary>
    ///     Returns the distinct role names the user has bindings for,
    ///     in unspecified order. Empty array if user has no bindings.
    /// </summary>
    /// <param name="userId">Caller identity, normally parsed from the
    /// login command's credentials or the refresh token's owner
    /// row.</param>
    /// <param name="cancellationToken">Forwarded to the DB read.</param>
    public Task<IReadOnlyList<string>> LoadAsync(Guid userId, CancellationToken cancellationToken = default);
}
