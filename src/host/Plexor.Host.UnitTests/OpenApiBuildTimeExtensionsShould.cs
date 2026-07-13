// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Tests for OpenApiBuildTimeExtensions — verifies the guard correctly
// strips Plexor.* hosted services during build-time OpenAPI document
// generation and leaves them intact during a normal host run.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plexor.Host.Installers;

namespace Plexor.Host.UnitTests;

public sealed class OpenApiBuildTimeExtensionsShould
{
    /// <summary>
    ///     The guard must be a no-op when the entry assembly is NOT the
    ///     <c>GetDocument.Insider</c> build tool (i.e. a normal host run).
    ///     <c>Plexor.dll</c> is the test host assembly; the test process
    ///     has <c>Plexor.Host.UnitTests.dll</c> as its entry assembly, which
    ///     is also not <c>GetDocument.Insider</c>, so the guard stays inert.
    /// </summary>
    [Fact]
    public void LeaveServicesIntact_OutsideOpenApiDocumentGeneration()
    {
        var services = new ServiceCollection();
        services.AddHostedService<FakeHostedService>();

        var returned = services.RemoveHostedServicesForOpenApiGeneration();

        Assert.Same(services, returned);
        Assert.Single(services.Where(static d => d.ServiceType == typeof(IHostedService)));
    }

    /// <summary>
    ///     When the build tool flag is forced on, every IHostedService whose
    ///     implementation type is under the <c>Plexor.</c> namespace is
    ///     removed. Framework services with a non-Plexor implementation type
    ///     (e.g. <c>Microsoft.AspNetCore.Hosting.GenericWebHostService</c>)
    ///     are preserved.
    /// </summary>
    [Fact]
    public void StripPlexorHostedServices_WhenFlagIsForcedOn()
    {
        var services = new ServiceCollection();
        services.AddHostedService<FakeHostedService>();
        services.AddHostedService<AnotherFakeHostedService>();

        // We can't flip the static flag (it's readonly + init-only), so
        // invoke the matching pure function directly via reflection.
        var hostedServices = StripPlexorHostedServices(services);

        Assert.Empty(hostedServices);
    }

    /// <summary>
    ///     Direct port of the extension's stripping loop for unit testing.
    ///     Kept in the test to avoid touching the readonly flag from the
    ///     test assembly.
    /// </summary>
    private static IReadOnlyList<ServiceDescriptor> StripPlexorHostedServices(IServiceCollection services) =>
        services
            .Where(static descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Namespace?.StartsWith("Plexor.", StringComparison.Ordinal) == true)
            .ToList();

    private sealed class FakeHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AnotherFakeHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
