// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BucketEndpoints — central route constants for the buckets resource
// group. The four endpoint files (ListBucketsEndpoint / GetBucketEndpoint
// / CreateBucketEndpoint / DeleteBucketEndpoint) all reference the
// constants here so the URL composition + route names live in one
// place.
// ============================================================================

using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Constants shared across the bucket endpoint family.
/// </summary>
public static class BucketEndpoints
{
    /// <summary>Base URL for the buckets resource group.</summary>
    public const string GroupRoute = ApiRoutes.Base + "/storage/buckets";

    /// <summary>Stable route names referenced by OpenAPI +
    /// each handler's <c>WithName(...)</c> call.</summary>
    public static class RouteNames
    {
        /// <summary><c>GET /api/v1/storage/buckets</c>.</summary>
        public const string List = "storage-buckets-list";

        /// <summary><c>GET /api/v1/storage/buckets/{bucketId}</c>.</summary>
        public const string Get = "storage-buckets-get";

        /// <summary><c>POST /api/v1/storage/buckets</c>.</summary>
        public const string Create = "storage-buckets-create";

        /// <summary><c>DELETE /api/v1/storage/buckets/{bucketId}</c>.</summary>
        public const string Delete = "storage-buckets-delete";
    }
}
