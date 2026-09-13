using System.Reflection;

namespace Plexor.ArchitectureTests;

/// <summary>
///     Centralized map of all Domain assemblies. ArchitectureTests reference
///     these by name so adding a new module is one line.
/// </summary>
public static class DomainAssemblies
{
    public static Assembly Sigil =>
        typeof(Plexor.Modules.Sigil.Domain.Entities.User).Assembly;

    public static Assembly Clusters =>
        typeof(Plexor.Modules.Clusters.Domain.Cluster).Assembly;

    public static Assembly Realm =>
        typeof(Plexor.Modules.Realm.Domain.Entities.Organization).Assembly;

    public static IReadOnlyList<Assembly> All => [Sigil, Clusters, Realm];
}
