// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RefreshCommand — rotate the presented refresh token and issue a
// fresh access JWT.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Refresh-token rotation request. Handler validates the
///     presented token, rotates it inside the same family, and
///     re-issues an access token against the user's resolved
///     permissions.
/// </summary>
/// <param name="RefreshToken">
///     Opaque base64url refresh token returned by
///     <see cref="LoginCommand" /> or a prior refresh.
/// </param>
public sealed record RefreshCommand(string RefreshToken);
