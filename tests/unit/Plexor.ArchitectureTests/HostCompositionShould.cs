// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostCompositionShould — enforce the reverse of Law 4 from the Plexor
// modular-monolith architecture: modules NEVER reference the entry
// point. Composition flows one way — the host wires the modules via
// installer extension methods, modules stay composition-agnostic so
// they can be hosted by Plexor.Host, Plexor.Migrator, an integration
// test harness, or any future workshop tooling.
//
// Two referends are forbidden here:
//   - `Plexor.Host`     — the production HTTP/gRPC control plane
//   - `Plexor.Migrator` — the schema-migration CLI
//
// A module referencing either becomes un-hostable outside that entry
// point; the Inversion of Control layering breaks.
//
// Module-as-folder note: a Plexor module is a folder that contains
// three assemblies (Domain / Application / Infrastructure). The
// assertion below enumerates each assembly explicitly — NetArchTest
// 1.3.2's `Types.InAssembly` requires a real assembly name; the
// module-folder name (`Plexor.Modules.Realm`) does not resolve to
// anything loadable.
// ============================================================================

using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Plexor.ArchitectureTests;

/// <summary>
///     Architecture tests that enforce Law 4 (composition root only):
///     no module may reference the host or migrator entry points.
///     Composition flows one way — the host wires modules via their
///     installer extensions; modules stay composition-agnostic.
/// </summary>
public sealed class HostCompositionShould
{
    /// <summary>
    ///     Given the Domain / Application / Infrastructure assembly of
    ///     every module, when scanned by NetArchTest for dependencies
    ///     on the host or migrator entry points, then none of them
    ///     reference <c>Plexor.Host</c> or <c>Plexor.Migrator</c>. A
    ///     module referencing either becomes un-hostable outside that
    ///     entry point.
    /// </summary>
    [Fact(DisplayName = "Given all module assemblies, when scanned for composition-root refs, then none reference Plexor.Host or Plexor.Migrator")]
    public void Modules_do_not_reference_Host_or_Migrator()
    {
        var moduleLayers = new[]
        {
            ("Plexor.Modules.Realm",      new[] { "Plexor.Modules.Realm.Domain",      "Plexor.Modules.Realm.Application",      "Plexor.Modules.Realm.Infrastructure" }),
            ("Plexor.Modules.Sigil",      ["Plexor.Modules.Sigil.Domain",      "Plexor.Modules.Sigil.Application",      "Plexor.Modules.Sigil.Infrastructure"]),
            ("Plexor.Modules.Clusters",   ["Plexor.Modules.Clusters.Domain",   "Plexor.Modules.Clusters.Application",   "Plexor.Modules.Clusters.Infrastructure"]),
            ("Plexor.Modules.Quotas",     ["Plexor.Modules.Quotas.Domain",     "Plexor.Modules.Quotas.Application",     "Plexor.Modules.Quotas.Infrastructure"]),
            ("Plexor.Modules.Branding",   ["Plexor.Modules.Branding.Domain",   "Plexor.Modules.Branding.Application",   "Plexor.Modules.Branding.Infrastructure"]),
            ("Plexor.Modules.Audit",      ["Plexor.Modules.Audit.Domain",      "Plexor.Modules.Audit.Application",      "Plexor.Modules.Audit.Infrastructure"]),
        };

        foreach (var (module, layers) in moduleLayers)
        {
            foreach (var asm in layers)
            {
                var noHost = Types
                    .InAssembly(TestAssemblies.Load(asm))
                    .ShouldNot()
                    .HaveDependencyOn("Plexor.Host")
                    .GetResult();
                var noMigrator = Types
                    .InAssembly(TestAssemblies.Load(asm))
                    .ShouldNot()
                    .HaveDependencyOn("Plexor.Migrator")
                    .GetResult();

                (noHost.IsSuccessful && noMigrator.IsSuccessful).ShouldBeTrue(
                    $"{asm} ({module}) must not reference Plexor.Host or Plexor.Migrator (Law 4: composes only at entry points).");
            }
        }
    }
}
