using System.Reflection;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Plexor.ArchitectureTests;

/// <summary>
///     Domain projects must have zero framework/IO dependencies per
///     <c>architecture.md</c> Law 2. NetArchTest-driven assertions that
///     catch accidental AspNetCore/EF/HttpClient references before code
///     review. Fails the build on violation.
/// </summary>
public sealed class DomainIsolationShould
{
    private static readonly string[] DomainProjectNames =
    [
        "Plexor.Modules.Sigil.Domain",
        "Plexor.Modules.Clusters.Domain",
        "Plexor.Modules.Realm.Domain",
    ];

    private static readonly Assembly[] DomainAssemblies = DomainProjectNames
        .Select(static name => Assembly.Load(name))
        .ToArray();

    [Fact(DisplayName = "Domain projects should not depend on Plexor.Shared.Filtering.Web")]
    public void DomainProjectsShouldNotDependOnFilteringWeb()
    {
        var result = Types.InAssemblies(DomainAssemblies)
            .ShouldNot()
            .HaveDependencyOn("Plexor.Shared.Filtering.Web")
            .GetResult();

        var failingTypes = string.Join(", ", result.FailingTypeNames ?? []);

        result.IsSuccessful.ShouldBeTrue(
            "Domain projects must not reference Plexor.Shared.Filtering.Web " +
            "(it pulls in Microsoft.AspNetCore.App via FrameworkReference). " +
            "Domain projects depend on Plexor.Shared.Filtering.Core instead." +
            "\nOffending types: " + failingTypes);
    }

    [Fact(DisplayName = "Domain projects should not depend on Plexor.Host")]
    public void DomainProjectsShouldNotDependOnHost()
    {
        var result = Types.InAssemblies(DomainAssemblies)
            .ShouldNot()
            .HaveDependencyOn("Plexor.Host")
            .GetResult();

        var failingTypes = string.Join(", ", result.FailingTypeNames ?? []);

        result.IsSuccessful.ShouldBeTrue(
            "Domain projects must not reference Plexor.Host " +
            "(composition root, not domain)." +
            "\nOffending types: " + failingTypes);
    }

    [Fact(DisplayName = "Domain projects should not contain types from Microsoft.AspNetCore.* namespaces")]
    public void DomainProjectsShouldNotUseAspNetCoreNamespaces()
    {
        // ResideInNamespace matches by prefix; "Microsoft.AspNetCore" catches
        // Microsoft.AspNetCore.Mvc, Microsoft.AspNetCore.OpenApi, etc.
        var result = Types.InAssemblies(DomainAssemblies)
            .ShouldNot()
            .ResideInNamespace("Microsoft.AspNetCore")
            .GetResult();

        var failingTypes = string.Join(", ", result.FailingTypeNames ?? []);

        result.IsSuccessful.ShouldBeTrue(
            "Domain projects must not contain types in Microsoft.AspNetCore.* namespaces " +
            "(Law 2 from architecture.md: Domain has zero framework/IO dependencies)." +
            "\nOffending types: " + failingTypes);
    }
}
