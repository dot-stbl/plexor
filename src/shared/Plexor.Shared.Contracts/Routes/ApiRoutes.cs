// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ApiRoutes — single source of truth for API URL composition.
//
// <see cref="ApiVersion" /> is the version segment embedded in every
// route. Bumping the API version means changing one constant, not
// nine controller [Route] attributes. Resource roots compose via
// <see cref="Resource" /> so controllers never hardcode "api/v1".
//
// v0.1: URL-prefix versioning. The version is the URL path segment.
// v0.2+ may layer attribute-based sub-versioning (e.g. /api/v1
// with [ApiVersion("1.0")] sub-versions) on top of this if
// co-existing generations become a real concern.
// ============================================================================

namespace Plexor.Shared.Contracts.Routes;

/// <summary>
///     URL composition for the Plexor public API. Every controller
///     <c>[Route]</c> attribute starts with <see cref="Base" /> so the
///     version segment lives in one place.
/// </summary>
public static class ApiRoutes
{
    /// <summary>
    ///     The API version segment embedded in every route
    ///     (<c>v1</c>). Bump to <c>"v2"</c> for the next major
    ///     generation; old controllers keep using the old <c>v1</c>
    ///     segment until they migrate.
    /// </summary>
    public const string ApiVersion = "v1";

    /// <summary>
    ///     The base path prefix (<c>api/v1</c>). Every
    ///     controller route composes its full path from this.
    /// </summary>
    public const string Base = "api/" + ApiVersion;

    /// <summary>
    ///     Composes a resource route under the API base:
    ///     <c>api/v1/{name}</c>. Use in controller <c>[Route]</c>
    ///     attributes: <c>[Route(ApiRoutes.Resource("advertisers"))]</c>.
    /// </summary>
    public static string Resource(string name)
    {
        return Base + "/" + name;
    }
}
