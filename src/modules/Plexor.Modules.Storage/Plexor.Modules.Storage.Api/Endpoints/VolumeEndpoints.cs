// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VolumeEndpoints — minimal API surface for /api/v1/storage/volumes.
// One static handler per HTTP method (List / Get / Create / Delete);
// each in its own file. The file-static GroupRoute + RouteNames
// centralise the URL composition + the OpenAPI route names so every
// handler references the same constants.
//
// Wire shape + permission gating mirrors Plexor.Modules.Branding.Api
// (BrandingController) and Plexor.Modules.Audit.Api
// (AuditQueryEndpoint):
//   - [Authorize] on the group (added in StorageEndpoints registration).
//   - RequireAuthorization(AuthorizationPolicyNames.For(...)) on each
//     endpoint, gating on StoragePermissions.Read / .Write.
//   - FluentValidation validator via [FromServices] on Create (runs
//     before any service call; failures surface as 400
//     ValidationProblemDetails per RFC 9457).
//
// Tenant scoping is enforced by passing currentUser.TenantId to
// the IVolumeService — a caller from Org X cannot read or mutate
// Org Y's volumes (cross-tenant lookups return 404, not 403).
// ============================================================================

using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Constants shared across the volume endpoint family. Lives in
///     its own file so the four endpoint files can reference the
///     same URL composition + route names without each one
///     re-declaring them.
/// </summary>
public static class VolumeEndpoints
{
    /// <summary>Base URL for the volumes resource group.
    /// Composed from <see cref="ApiRoutes.Base" /> so the
    /// <c>/api/v1</c> prefix lives in one place.</summary>
    public const string GroupRoute = ApiRoutes.Base + "/storage/volumes";

    /// <summary>Stable route names referenced by OpenAPI +
    /// each handler's <c>WithName(...)</c> call.</summary>
    public static class RouteNames
    {
        /// <summary><c>GET /api/v1/storage/volumes</c>.</summary>
        public const string List = "storage-volumes-list";

        /// <summary><c>GET /api/v1/storage/volumes/{volumeId}</c>.</summary>
        public const string Get = "storage-volumes-get";

        /// <summary><c>POST /api/v1/storage/volumes</c>.</summary>
        public const string Create = "storage-volumes-create";

        /// <summary><c>PUT /api/v1/storage/volumes/{volumeId}</c>.</summary>
        public const string Update = "storage-volumes-update";

        /// <summary><c>DELETE /api/v1/storage/volumes/{volumeId}</c>.</summary>
        public const string Delete = "storage-volumes-delete";
    }
}
