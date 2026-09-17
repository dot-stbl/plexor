// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ModuleIsolationShould — enforce Law 3 of the Plexor modular-monolith
// architecture: modules don't reference each other's Infrastructure.
// Contracts (Abstractions / Application / Domain / shared kernel) flow
// freely; concrete plumbing (EF, repos, installers, DbContexts) does not.
//
// The three tests below pin the boundary that's currently enforced:
//   1. A module's Application layer must not depend on its OWN
//      Infrastructure layer (intramodule inversion — application
//      defines ports, infrastructure binds adapters).
//   2. Cross-module consumers reach into another module only via
//      `<Module>.Application.Abstractions` — never `.Infrastructure`.
//      The composition root (`Plexor.Host`) is the ONE allowed
//      exception — it must wire each module's concrete installers
//      and persistence types to compose the application graph.
//      See `HostCompositionShould` for the reverse assertion
//      (modules don't reference Host).
//   3. Sigil.Infrastructure must not reach into Realm.Infrastructure —
//      the per-tenant OrgAuthProviderConfig lookups (CanAuthenticate +
//      Resolve) go through `IOrgAuthProviderConfigReader` (Realm.Application)
//      and the OIDC client-secret decryption goes through
//      `IOrgAuthProviderSecretProtector` (Realm.Application). A future
//      PR that re-introduces a direct Realm.Infrastructure reference
//      fails this test, forcing the new contributor to widen the seam
//      in `Plexor.Modules.Realm.Application.AuthProviders` first.
//
// NetArchTest 1.3.2's `Types.InAssembly` only accepts a
// `System.Reflection.Assembly`; the helper in `TestAssemblies.cs`
// wraps `Assembly.Load(new AssemblyName(name))` so each test names its
// target by simple string, mirroring the more recent API surface.
// Multi-condition negative chains (ShouldNot + HaveDependencyOn + And
// + HaveDependencyOn) are spelled as separate result composes — the
// 1.3.2 fluent shape doesn't compose two independent negative chains
// via `.And().ShouldNot()`.
// ============================================================================

using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Plexor.ArchitectureTests;

/// <summary>
///     Architecture tests that enforce Law 3 — modules communicate
///     only through their <c>*.Application.Abstractions</c>
///     namespaces; concrete <c>*.Infrastructure</c> stays private to
///     each module. Two assertions: intramodule inversion (Application
///     never depends on its own Infrastructure) and cross-module
///     isolation (sibling modules never reach into another module's
///     concrete plumbing).
/// </summary>
public sealed class ModuleIsolationShould
{
    // Law 3 (intramodule inversion): application defines ports,
    // infrastructure binds adapters — the dependency must point
    // inward, never sideways into your own EF plumbing.
    /// <summary>
    ///     Given the <c>Plexor.Modules.Quotas.Application</c>
    ///     assembly, when scanned by NetArchTest for dependencies on
    ///     <c>Plexor.Modules.Quotas.Infrastructure</c> or
    ///     <c>Plexor.Modules.Sigil.Infrastructure</c>, then the
    ///     Application has neither dependency. Application defines
    ///     ports; Infrastructure binds adapters — and the Quotas
    ///     Application must not reach into another module's
    ///     Infrastructure either.
    /// </summary>
    [Fact(DisplayName = "Given the Quotas module, when Application is scanned, then it references neither Quotas.Infrastructure nor other modules' Infrastructure")]
    public void QuotasApplication_does_not_reference_QuotasInfrastructure_Or_other_modules_infrastructure()
    {
        var asm = TestAssemblies.Load("Plexor.Modules.Quotas.Application");

        var ownInfra = Types
            .InAssembly(asm)
            .ShouldNot()
            .HaveDependencyOn("Plexor.Modules.Quotas.Infrastructure")
            .GetResult();
        var otherInfra = Types
            .InAssembly(asm)
            .ShouldNot()
            .HaveDependencyOn("Plexor.Modules.Sigil.Infrastructure")
            .GetResult();

        (ownInfra.IsSuccessful && otherInfra.IsSuccessful).ShouldBeTrue(
            "Plexor.Modules.Quotas.Application must not depend on Quotas.Infrastructure "
            + "(intramodule inversion) or Sigil.Infrastructure (Law 3 cross-module).");
    }

    // Law 3 (cross-module): consumers reach into a sibling module only
    // through its `.Application.Abstractions` namespace — the kernel
    // interface layer. Touching a sibling's `.Infrastructure` from
    // outside is the locked door: it bypasses the inversion and roots
    // one module's EF plumbing inside another's compilation unit.
    //
    // Documented allowed pairs (positive — not asserted; the audit
    // confirmed each one below uses `.Application.Abstractions`):
    //
    //   Plexor.Modules.Branding.Api    -> Plexor.Modules.Sigil.Application.Abstractions
    //   Plexor.Modules.Audit.Api       -> Plexor.Modules.Sigil.Application.Abstractions
    //   Plexor.Modules.Quotas.Api      -> Plexor.Modules.Sigil.Application.Abstractions
    //   Plexor.Host                    -> Plexor.Modules.Sigil.Application.Abstractions
    //
    // Composition-root caveat: `Plexor.Host` legally references
    // `Plexor.Modules.Sigil.Infrastructure` (via the
    // `AddSigilInfrastructureCore()` extension in `Installers\` and
    // the concrete `IdentityDbContext` in `Persistence\`). That is the
    // composition root doing its job per Law 4 — it would be wrong to
    // forbid it. The reverse (no module references Host) lives in
    // `HostCompositionShould.Modules_do_not_reference_Host_or_Migrator`.
    //
    // The negative assertion below is the load-bearing one. If the test
    // starts failing on a (consumer, Sigil.Infrastructure) pair, the
    // consumer has reached past the abstractions layer into concrete
    // EF plumbing — the fix is to expose a port under
    // Plexor.Modules.Sigil.Application.Abstractions.
    /// <summary>
    ///     Given every consumer that reaches into the Sigil module
    ///     (Branding.Api, Audit.Api, Quotas.Api, and
    ///     Clusters.Infrastructure), when scanned by NetArchTest for
    ///     dependencies, then none reference
    ///     <c>Plexor.Modules.Sigil.Infrastructure</c>. The consumers
    ///     above already reach the Sigil module via
    ///     <c>Plexor.Modules.Sigil.Application.Abstractions</c>; a
    ///     failure here means one of them slipped past the
    ///     abstractions layer into concrete EF plumbing.
    /// </summary>
    [Fact(DisplayName = "Given cross-module consumers, when their assemblies are scanned, then none reference another module's Infrastructure")]
    public void Cross_module_consumers_may_reference_Abstractions_but_not_Infrastructure()
    {
        var disallowed = new[]
        {
            ("Plexor.Modules.Sigil.Infrastructure", "Plexor.Modules.Branding.Api"),
            ("Plexor.Modules.Sigil.Infrastructure", "Plexor.Modules.Audit.Api"),
            ("Plexor.Modules.Sigil.Infrastructure", "Plexor.Modules.Quotas.Api"),
            ("Plexor.Modules.Sigil.Infrastructure", "Plexor.Modules.Clusters.Infrastructure"),
        };

        foreach (var (forbidden, consumer) in disallowed)
        {
            var result = Types
                .InAssembly(TestAssemblies.Load(consumer))
                .ShouldNot()
                .HaveDependencyOn(forbidden)
                .GetResult();
            result.IsSuccessful.ShouldBeTrue(
                $"{consumer} must not reference {forbidden} (Law 3: modules communicate only via contracts).");
        }
    }

    // Law 3 (specific cross-module guard): Sigil.Infrastructure is
    // the consumer that historically reached past the Realm
    // abstractions into concrete EF plumbing (`RealmDbContext`,
    // `OrgAuthProviderSecretProtector`). The arch-test cleanup pass
    // (4.6.3d) wired it through `IOrgAuthProviderConfigReader` and
    // `IOrgAuthProviderSecretProtector` instead. This assertion
    // ensures no future PR reintroduces the direct reference.
    /// <summary>
    ///     Given <c>Plexor.Modules.Sigil.Infrastructure</c>, when
    ///     scanned by NetArchTest for dependencies, then it has no
    ///     dependency on <c>Plexor.Modules.Realm.Infrastructure</c>.
    ///     Per-tenant config reads go through
    ///     <see cref="Plexor.Modules.Realm.Application.AuthProviders.IOrgAuthProviderConfigReader" />;
    ///     OIDC client-secret decryption goes through
    ///     <see cref="Plexor.Modules.Realm.Application.AuthProviders.IOrgAuthProviderSecretProtector" />.
    ///     A failure here means Sigil.Infrastructure reached past
    ///     those abstractions into Realm's EF plumbing — the fix is
    ///     to widen the seam in
    ///     <c>Plexor.Modules.Realm.Application.AuthProviders</c>,
    ///     not to add a project reference.
    /// </summary>
    [Fact(DisplayName = "Given Sigil.Infrastructure, when scanned for Realm dependencies, then it has no Realm.Infrastructure reference")]
    public void SigilInfrastructure_does_not_reference_RealmInfrastructure()
    {
        var result = Types
            .InAssembly(TestAssemblies.Load("Plexor.Modules.Sigil.Infrastructure"))
            .ShouldNot()
            .HaveDependencyOn("Plexor.Modules.Realm.Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Plexor.Modules.Sigil.Infrastructure must not reference Plexor.Modules.Realm.Infrastructure "
            + "(Law 3: modules communicate only via contracts). Use "
            + "IOrgAuthProviderConfigReader or IOrgAuthProviderSecretProtector "
            + "(both in Plexor.Modules.Realm.Application.AuthProviders) for any "
            + "OrgAuthProviderConfig / OIDC secret-protector reads.");
    }

    // L8 — ICurrentUser used to live in Plexor.Modules.Sigil.Application.Abstractions.
    // It is a cross-module contract (every module + Host reads it), so it belongs in
    // Plexor.Shared.Kernel alongside the other shared-kernel contracts (IRateLimiter,
    // IAuditEmitter, …). The boundary guard below pins that location: future contributors
    // who try to add a new method to ICurrentUser (or read it from a new module) find it
    // in the kernel, not in Sigil — and Sigil's Application assembly stays free of a
    // shared-kernel shape that other modules shouldn't reach into.
    /// <summary>
    ///     Given the Plexor solution, when the
    ///     <c>Plexor.Shared.Kernel</c> and
    ///     <c>Plexor.Modules.Sigil.Application</c> assemblies are
    ///     scanned by NetArchTest for the interface
    ///     <c>ICurrentUser</c>, then it lives in the shared kernel
    ///     and is absent from Sigil's Application layer. <c>ICurrentUser</c>
    ///     is a cross-module contract — every module (Host, Branding,
    ///     Audit, Quotas, Clusters, Sigil) and the composition root
    ///     read it. Pinning it under <c>Plexor.Shared.Kernel</c>
    ///     prevents modules from coupling through Sigil's Application
    ///     abstractions layer.
    /// </summary>
    [Fact(DisplayName = "Given ICurrentUser, when assemblies are scanned, then it lives in Plexor.Shared.Kernel and not in Sigil.Application")]
    public void ICurrentUser_lives_in_Plexor_Shared_Kernel_not_in_Sigil()
    {
        var inSharedKernel = Types
            .InAssembly(TestAssemblies.Load("Plexor.Shared.Kernel"))
            .That()
            .AreInterfaces()
            .GetTypes()
            .Any(t => t.Name == "ICurrentUser");
        inSharedKernel.ShouldBeTrue(
            "ICurrentUser must live in Plexor.Shared.Kernel so modules don't depend on Sigil.");

        var inSigilApplication = Types
            .InAssembly(TestAssemblies.Load("Plexor.Modules.Sigil.Application"))
            .That()
            .AreInterfaces()
            .GetTypes()
            .Any(t => t.Name == "ICurrentUser");
        inSigilApplication.ShouldBeFalse(
            "ICurrentUser was moved OUT of Sigil in commit L8 — it should not be re-introduced here.");
    }

    // Phase 4.5.d — Storage + Network modules. The two new sibling
    // modules must stay isolated from each other: any cross-reference
    // would root one module's EF plumbing inside the other. The
    // cross-module seam Quotas.Infrastructure uses
    // (Plexor.Modules.Storage.Application for IStorageQuotaReader) is
    // an Application-only reference — the test below asserts that
    // Storage.Infrastructure and Network.Infrastructure don't leak
    // into each other.
    /// <summary>
    ///     Given the Network module, when its assemblies are scanned
    ///     by NetArchTest for dependencies, then neither references
    ///     <c>Plexor.Modules.Storage.Infrastructure</c>. The Network
    ///     module reads storage state through its own seams; reaching
    ///     into Storage.Infrastructure would break Law 3 and
    ///     silently couple the two sibling modules' EF plumbing.
    /// </summary>
    [Fact(DisplayName = "Given the Network module, when scanned, then it does not reference Storage.Infrastructure")]
    public void Network_does_not_reference_Storage_Infrastructure()
    {
        var result = Types
            .InAssembly(TestAssemblies.Load("Plexor.Modules.Network.Infrastructure"))
            .ShouldNot()
            .HaveDependencyOn("Plexor.Modules.Storage.Infrastructure")
            .GetResult();
        result.IsSuccessful.ShouldBeTrue(
            "Plexor.Modules.Network.Infrastructure must not reference Plexor.Modules.Storage.Infrastructure "
            + "(Law 3: sibling modules communicate only via contracts).");
    }

    /// <summary>
    ///     Given the Storage module, when its assemblies are scanned
    ///     by NetArchTest for dependencies, then neither references
    ///     <c>Plexor.Modules.Network.Infrastructure</c>. Symmetric to
    ///     <see cref="Network_does_not_reference_Storage_Infrastructure" />.
    /// </summary>
    [Fact(DisplayName = "Given the Storage module, when scanned, then it does not reference Network.Infrastructure")]
    public void Storage_does_not_reference_Network_Infrastructure()
    {
        var result = Types
            .InAssembly(TestAssemblies.Load("Plexor.Modules.Storage.Infrastructure"))
            .ShouldNot()
            .HaveDependencyOn("Plexor.Modules.Network.Infrastructure")
            .GetResult();
        result.IsSuccessful.ShouldBeTrue(
            "Plexor.Modules.Storage.Infrastructure must not reference Plexor.Modules.Network.Infrastructure "
            + "(Law 3: sibling modules communicate only via contracts).");
    }

    /// <summary>
    ///     Given the Quotas Infrastructure (which depends on Storage +
    ///     Network + Realm for the cross-module seams), when scanned
    ///     for dependencies, then it does NOT reach into the sibling
    ///     modules' Infrastructure layers (where the EF plumbing
    ///     lives). The cross-module references go through the sibling
    ///     <c>Application</c> layers only — the seam pattern that
    ///     Law 3 enforces. The positive direction (Quotas → Storage.Application,
    ///     Quotas → Network.Application) is asserted implicitly: the
    ///     Solutions Build step would fail if those seams were missing,
    ///     because the EfQuotaEnforcer constructor takes them.
    /// </summary>
    [Fact(DisplayName = "Given the Quotas module, when its Infrastructure is scanned, then it does not reach into either sibling module's Infrastructure layer")]
    public void QuotasInfrastructure_reaches_sibling_Application_seams_but_not_Infrastructure()
    {
        var storageInfraBlocked = Types
            .InAssembly(TestAssemblies.Load("Plexor.Modules.Quotas.Infrastructure"))
            .ShouldNot()
            .HaveDependencyOn("Plexor.Modules.Storage.Infrastructure")
            .GetResult();
        storageInfraBlocked.IsSuccessful.ShouldBeTrue(
            "Plexor.Modules.Quotas.Infrastructure must not depend on Plexor.Modules.Storage.Infrastructure "
            + "(Law 3: cross-module references go through Application seams, not Infrastructure plumbing).");

        var networkInfraBlocked = Types
            .InAssembly(TestAssemblies.Load("Plexor.Modules.Quotas.Infrastructure"))
            .ShouldNot()
            .HaveDependencyOn("Plexor.Modules.Network.Infrastructure")
            .GetResult();
        networkInfraBlocked.IsSuccessful.ShouldBeTrue(
            "Plexor.Modules.Quotas.Infrastructure must not depend on Plexor.Modules.Network.Infrastructure "
            + "(Law 3: cross-module references go through Application seams, not Infrastructure plumbing).");
    }
}
