using Shouldly;

using Xunit;

namespace Plexor.ArchitectureTests;

public sealed class DomainIsolationShould
{
    [Fact(DisplayName = "Given a Domain assembly, when EF Core is referenced, then fail (Domain must be framework-free)")]
    public void DomainProjectsShouldNotDependOnEf()
    {
        var forbidden = new[] { "Microsoft.EntityFrameworkCore" };

        var failing = DomainAssemblies.All
            .Select(asm => (asm.GetName().Name, Refs: asm.GetReferencedAssemblies()))
            .Where(x => x.Refs.Any(refAsm => forbidden.Any(prefix =>
                refAsm.Name?.StartsWith(prefix, StringComparison.Ordinal) == true)))
            .Select(x => x.Name)
            .ToList();

        failing.ShouldBeEmpty(
            $"Domain assemblies must not reference EF Core. Violations: {string.Join(", ", failing)}");
    }

    [Fact(DisplayName = "Given a Domain assembly, when AspNetCore / System.Net.Http is referenced, then fail (Domain must be framework-free)")]
    public void DomainProjectsShouldNotDependOnAspNetCore()
    {
        var forbidden = new[] { "Microsoft.AspNetCore", "System.Net.Http" };

        var failing = DomainAssemblies.All
            .Select(asm => (asm.GetName().Name, Refs: asm.GetReferencedAssemblies()))
            .Where(x => x.Refs.Any(refAsm => forbidden.Any(prefix =>
                refAsm.Name?.StartsWith(prefix, StringComparison.Ordinal) == true)))
            .Select(x => x.Name)
            .ToList();

        failing.ShouldBeEmpty(
            $"Domain assemblies must not reference AspNetCore / System.Net.Http. Violations: {string.Join(", ", failing)}");
    }

    [Fact(DisplayName = "Given a Domain assembly, when own Infrastructure is referenced, then fail (Domain must not reach into Infrastructure)")]
    public void DomainProjectsShouldNotDependOnOwnInfrastructure()
    {
        var forbidden = DomainAssemblies.All
            .Select(static asm => asm.GetName().Name?.Replace(".Domain", ".Infrastructure"))
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        var failing = DomainAssemblies.All
            .SelectMany(asm => asm.GetReferencedAssemblies()
                .Where(refAsm => forbidden.Contains(refAsm.Name))
                .Select(refAsm => $"{asm.GetName().Name} -> {refAsm.Name}"))
            .ToList();

        failing.ShouldBeEmpty(
            $"Domain assemblies must not reference own Infrastructure. Violations: {string.Join(", ", failing)}");
    }
}
