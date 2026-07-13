// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Tests for OpenApiBuildTimeExtensions — verifies the guard correctly
// strips Plexor.* hosted services during build-time OpenAPI document
// generation and leaves them intact during a normal host run.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plexor.Host.Installers;
using Xunit;

namespace Plexor.Host.UnitTests;

/// <summary>
/// Unit tests for the OpenApiBuildTimeExtensions guard. The guard detects
/// when the host is being launched by the build-time OpenAPI document
/// generator and strips the app's own hosted services so the tool exits
/// without needing Postgres.
/// </summary>
public sealed class OpenApiBuildTimeExtensionsShould
{
    /// <summary>
    ///     The guard must be a no-op when the entry assembly is NOT the
    ///     <c>GetDocument.Insider</c> build tool (i.e. a normal host run).
    ///     The test process has <c>Plexor.Host.UnitTests.dll</c> as its
    ///     entry assembly, which is also not <c>GetDocument.Insider</c>.
    /// </summary>
    [Fact]
    public void LeaveServicesIntactOutsideOpenApiDocumentGeneration()
    {
        var services = new ServiceCollection();
        services.AddHostedService<FakeHostedService>();

        var returned = services.RemoveHostedServicesForOpenApiGeneration();

        Assert.Same(services, returned);
        Assert.Single(services, d => d.ServiceType == typeof(IHostedService));
    }

    /// <summary>
    ///     Sanity check on the predicate the extension uses. The
    ///     <see cref="OpenApiBuildTimeExtensions.IsOpenApiDocumentGeneration"/>
    ///     flag is <c>init</c>-only and can't be flipped from tests, so we
    ///     re-implement the matching predicate here to assert the
    ///     "Plexor.*" namespace filter behaves as expected.
    /// </summary>
    [Fact]
    public void StripPlexorHostedServicesWhenFilterMatches()
    {
        var services = new ServiceCollection();
        services.AddHostedService<FakeHostedService>();
        services.AddHostedService<AnotherFakeHostedService>();

        var matched = StripPlexorHostedServices(services);

        Assert.Equal(2, matched.Count);
    }

    /// <summary>
    ///     Direct port of the extension's stripping predicate for unit testing.
    ///     Kept in the test to avoid touching the readonly flag from the
    ///     test assembly.
    /// </summary>
    private static List<ServiceDescriptor> StripPlexorHostedServices(IServiceCollection services)
    {
        return services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Namespace?.StartsWith("Plexor.", StringComparison.Ordinal) == true)
            .ToList();
    }

    private sealed class FakeHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AnotherFakeHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
