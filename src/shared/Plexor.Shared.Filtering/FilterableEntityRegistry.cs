using System.Collections.Concurrent;

namespace Plexor.Shared.Filtering;

/// <summary>
///     Registry of <see cref="IFilterableEntity" /> types, indexed by their
///     CLR full name. Entities are added via <c>AddFilterableEntity&lt;T&gt;</c>
///     at startup; the <see cref="FilterableSchemaTransformer" /> reads them
///     from DI on every schema-emission pass.
/// </summary>
/// <remarks>
///     <para>
///         The registry caches one <see cref="UntypedFilterableField" /> list
///         per type, computed via reflection on first access. Each
///         <c>x-filterable</c> schema attaches its own copy — the cache is
///         per-type, not per-schema, so the first emission pays the
///         reflection cost and subsequent ones reuse the list.
///     </para>
///     <para>
///         Singleton lifetime — same instance reused across every schema
///         emission (build-time Microsoft.Extensions.ApiDescription.Server
///         target + every <c>/openapi/v1.json</c> request).
///     </para>
/// </remarks>
public sealed class FilterableEntityRegistry()
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<UntypedFilterableField>> byTypeName = new(StringComparer.Ordinal);

    /// <summary>
    ///     Registers a CLR type as filterable. Idempotent — repeat calls with
    ///     the same <typeparamref name="T" /> are no-ops.
    /// </summary>
    /// <typeparam name="T">Entity implementing <see cref="IFilterableEntity" />.</typeparam>
    public void Register<T>() where T : IFilterableEntity
    {
        var typeName = typeof(T).FullName
            ?? throw new InvalidOperationException(
                $"Type {typeof(T).Name} has no full name — anonymous types cannot be filtered.");
        Register(typeName, typeof(T));
    }

    /// <summary>
    ///     Registers a CLR type by reference + caches the untyped field set
    ///     for later lookup. Internal so the DI extension can stay one-method.
    /// </summary>
    /// <param name="typeName">CLR full name (used as lookup key).</param>
    /// <param name="type">CLR type to reflect for fields.</param>
    private void Register(string typeName, Type type)
    {
        if (byTypeName.ContainsKey(typeName))
        {
            return;
        }

        // Cache via FilterableFieldRegistry.BuildUntyped which already
        // owns the per-type reflection cache — so wiring this up costs
        // zero extra reflection per type.
        byTypeName[typeName] = FilterableFieldRegistry.BuildUntyped(type);
    }

    /// <summary>
    ///     Returns the cached field set for a CLR full name, or <c>null</c>
    ///     if the entity was never registered.
    /// </summary>
    /// <param name="typeName">CLR full name (must match <see cref="Type.FullName" />).</param>
    public IReadOnlyList<UntypedFilterableField>? TryGet(string typeName)
    {
        return byTypeName.TryGetValue(typeName, out var fields) ? fields : null;
    }

    /// <summary>Number of entities currently registered (diagnostics).</summary>
    public int Count => byTypeName.Count;
}
