// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StorageEndpoints — single registration entry for every Storage
// minimal-API endpoint. Mirrors the Plexor.Modules.Audit.Api pattern
// (AuditQueryEndpoint.MapAuditQuery is the closest sibling) — one
// public static class with extension methods that Program.cs chains.
//
// Usage from Plexor.Host/Program.cs:
//
//     app.MapStorageEndpoints();
//
// The volume + bucket endpoints are independent; the caller could
// chain the individual MapXxxEndpoint methods for partial registration
// in tests but the production wiring always uses MapStorageEndpoints.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Plexor.Modules.Storage.Api.Endpoints;

/// <summary>
///     Top-level registration extension for every Storage endpoint.
///     Maps the four volumes endpoints (list / create / get / delete)
///     and the four buckets endpoints (list / create / get / delete)
///     under <c>/api/v1/storage/volumes</c> and
///     <c>/api/v1/storage/buckets</c> respectively.
/// </summary>
public static class StorageEndpoints
{
    /// <summary>
    ///     Register every Storage endpoint. Returns the same
    ///     <paramref name="builder" /> for chaining.
    /// </summary>
    public static IEndpointRouteBuilder MapStorageEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapListVolumesEndpoint();
        builder.MapCreateVolumeEndpoint();
        builder.MapGetVolumeEndpoint();
        builder.MapDeleteVolumeEndpoint();

        builder.MapListBucketsEndpoint();
        builder.MapCreateBucketEndpoint();
        builder.MapGetBucketEndpoint();
        builder.MapDeleteBucketEndpoint();

        return builder;
    }
}
