// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SealedDefaultShould — enforce naming-and-types.md §2 / Law 2
// (architecture.md): every concrete class is `sealed` by default.
// Inheritance is opt-in, not opt-out: a class proves it deserves to
// be a base by having a documented subclass today or an ADR.
//
// Exemptions encoded in this test:
//   - `Program`           — entry point can be referenced by name by
//                            tooling (top-level statements generate it).
//   - Names ending in
//     `Exception`         — base exceptions are sometimes open by
//                            convention; locking them sealed-by-default
//                            trips well-meaning catches elsewhere.
//   - `X509Authority`     — pre-existing base in Plexor.Shared.Mtls;
//                            future cleanup will seal it (tracked).
//   - Namespaces containing
//     `Migrations`        — EF Core tool-generated migration files
//                            (see `ef-migrations.md`). They live in
//                            `*.Migrations` and `*.Migrations.<Context>`
//                            namespaces; the source generator emits
//                            `[GeneratedCode]` at compile time but the
//                            file itself is `public partial class`
//                            per EF conventions.
//   - Namespaces containing
//     `Mappers`           — Mapperly source-generated partial classes
//                            (see `mapping.md`). The mapper is a
//                            `public partial class` rather than `sealed
//                            class` because the source generator
//                            supplies the body.
//
// The test collects every offender before reporting so the contributor
// sees the whole list, not one failure at a time.
// ============================================================================

using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Plexor.ArchitectureTests;

/// <summary>
///     Architecture tests that enforce naming-and-types.md §2 —
///     every concrete class is <c>sealed</c> by default. Inheritance
///     is opt-in; a class proves it deserves to be a base by having a
///     documented subclass today or an ADR.
/// </summary>
public sealed class SealedDefaultShould
{
    /// <summary>
    ///     Given every module's <c>*.Application</c> and
    ///     <c>*.Infrastructure</c> assembly, when scanned by
    ///     NetArchTest for concrete classes, then every concrete
    ///     class is <c>sealed</c> (modulo the documented exemptions:
    ///     <c>Program</c>, types ending in <c>Exception</c>,
    ///     <c>X509Authority</c>, types in EF-Migration namespaces,
    ///     and types in Mapperly-Mapper namespaces).
    /// </summary>
    [Fact(DisplayName = "Given all module Application + Infrastructure assemblies, when scanned for unsealed concrete classes, then every offender is sealed")]
    public void All_concrete_classes_in_modules_are_sealed()
    {
        var assemblies = new[]
        {
            "Plexor.Modules.Realm.Application",
            "Plexor.Modules.Sigil.Application",
            "Plexor.Modules.Clusters.Application",
            "Plexor.Modules.Quotas.Application",
            "Plexor.Modules.Branding.Application",
            "Plexor.Modules.Audit.Application",
            "Plexor.Modules.Sigil.Infrastructure",
            "Plexor.Modules.Realm.Infrastructure",
            "Plexor.Modules.Clusters.Infrastructure",
            "Plexor.Modules.Quotas.Infrastructure",
            "Plexor.Modules.Branding.Infrastructure",
            "Plexor.Modules.Audit.Infrastructure",
        };

        var violations = new List<string>();
        foreach (var asm in assemblies)
        {
            var concreteClasses = Types
                .InAssembly(TestAssemblies.Load(asm))
                .That()
                .AreClasses()
                .And()
                .AreNotAbstract()
                .And()
                .DoNotHaveNameMatching("Program")
                .And()
                .DoNotHaveNameEndingWith("Exception")
                .And()
                .DoNotHaveNameMatching("X509Authority")
                .And()
                .DoNotResideInNamespaceContaining("Migrations")
                .And()
                .DoNotResideInNamespaceContaining("Mappers")
                .GetTypes();

            foreach (var type in concreteClasses)
            {
                if (type.IsSealed)
                {
                    continue;
                }
                violations.Add($"{asm}: {type.FullName}");
            }
        }

        violations.ShouldBeEmpty(
            "Concrete classes must be sealed by default. Offenders:\n" + string.Join("\n", violations));
    }
}
