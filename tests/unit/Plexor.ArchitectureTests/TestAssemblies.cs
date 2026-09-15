// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TestAssemblies — small helper for the architecture test project.
// NetArchTest 1.3.2 ships the `Types.InAssembly` overload that takes a
// `System.Reflection.Assembly`, but every existing example uses a
// string overload from the newer NetArchTest 3.x line. Plexor pins
// 1.3.2 (see Directory.Packages.props), so this loader wraps
// `Assembly.Load(new AssemblyName(name))` once per name and caches
// the result for the duration of the test process.
//
// The project-reference list in the test csproj makes every module's
// Application + Infrastructure assembly land in the bin/ directory;
// `Assembly.Load` discovers them via the standard probing path.
// ============================================================================

using System.Reflection;

namespace Plexor.ArchitectureTests;

internal static class TestAssemblies
{
    private static readonly Dictionary<string, Assembly?> Cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Load <paramref name="assemblyName" /> via the runtime's
    ///     standard probe path. Returns <c>null</c> when the assembly
    ///     is not in the test project's runtime graph — the caller's
    ///     test should then fail with a "missing reference" message,
    ///     never silently no-op.
    /// </summary>
    /// <param name="assemblyName"></param>
    public static Assembly? TryLoad(string assemblyName)
    {
        if (Cache.TryGetValue(assemblyName, out var cached))
        {
            return cached;
        }

        Assembly? loaded = null;
        try
        {
            loaded = Assembly.Load(new AssemblyName(assemblyName));
        }
        catch (FileNotFoundException)
        {
            // Probe the already-loaded AppDomain — at least one
            // independent test runner (TUnit, NUnit, xUnit) loads
            // assemblies lazily and `Assembly.Load` may miss them
            // until they're referenced. The test's csproj guarantees
            // every assembly we look up is on the probing path, so
            // this branch is a safety net, not the happy path.
            foreach (var existing in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(existing.GetName().Name, assemblyName, StringComparison.Ordinal))
                {
                    loaded = existing;
                    break;
                }
            }
        }

        Cache[assemblyName] = loaded;
        return loaded;
    }

    /// <summary>
    ///     Like <see cref="TryLoad" />, but throws when the assembly
    ///     is missing. Use this from inside an assertion so the
    ///     failure message names the missing reference.
    /// </summary>
    /// <param name="assemblyName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static Assembly Load(string assemblyName)
    {
        return TryLoad(assemblyName)
            ?? throw new InvalidOperationException(
                $"Assembly '{assemblyName}' is not on the test project's runtime graph. "
                + "Add a <ProjectReference /> in Plexor.ArchitectureTests.csproj.");
    }
}
