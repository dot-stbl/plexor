// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CustomCssEndpoint — minimal API handler for GET /custom.css. The
// route serves the operator's custom.css from disk with sensible
// fall-through:
//   - File exists      → 200 with file contents (CSS content-type)
//   - File missing     → 404 (not 500 — the operator may not have
//                          created custom.css yet; this is the v1
//                          default state).
//
// Why an endpoint, not static-file middleware: the file path is
// OS-conventional (default) + operator-overridable. Static-file
// middleware assumes a known relative path under wwwroot; serving
// arbitrary operator-supplied paths needs a tiny controller-shaped
// surface that can read IOptions + handle 404 distinctly.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plexor.Modules.Branding.Api;

namespace Plexor.Modules.Branding.Api.Endpoints;

/// <summary>
///     Minimal API endpoint that serves the operator's custom.css
///     file. Registered via <c>MapCustomCssEndpoint</c> from
///     <c>Program.cs</c> at the same level as the controller
///     surface.
/// </summary>
public static class CustomCssEndpoint
{
    /// <summary>
    ///     The relative URL the operator's frontend hits to fetch the
    ///     custom.css. Mirrored in <c>index.html</c> as the link's
    ///     <c>href</c>; <c>main.tsx</c> probes <c>HEAD /custom.css</c>
    ///     before enabling the link so the document never carries a
    ///     broken link tag when the file is missing.
    /// </summary>
    public const string Path = "/custom.css";

    /// <summary>
    ///     Map the endpoint. Call once from <c>Program.cs</c> at
    ///     composition time. The endpoint is anonymous — the CSS
    ///     is publicly served (the FE boot script runs before any
    ///     auth token is in flight).
    /// </summary>
    public static IEndpointRouteBuilder MapCustomCssEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(Path, HandleAsync);
        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IOptions<BrandingOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Plexor.Modules.Branding.CustomCssEndpoint");
        var configuredPath = options.Value.CustomCssPath;

        // Environment-variable expansion for the Windows %ProgramData% path.
        var resolvedPath = Environment.ExpandEnvironmentVariables(configuredPath);

        if (!File.Exists(resolvedPath))
        {
            logger.LogDebug(
                "Custom CSS file not found at {Path}; returning 404.",
                resolvedPath);
            return Results.NotFound();
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(resolvedPath, cancellationToken);
            // Hardcoded content-type — custom.css is always CSS, and
            // FileExtensionContentTypeProvider would need a configured
            // provider for the operator's chosen extension.
            return Results.File(bytes, contentType: "text/css");
        }
        catch (IOException ex)
        {
            logger.LogWarning(
                ex,
                "Custom CSS file at {Path} was unreadable; returning 404.",
                resolvedPath);
            return Results.NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(
                ex,
                "Custom CSS file at {Path} was inaccessible; returning 404.",
                resolvedPath);
            return Results.NotFound();
        }
    }
}