namespace Plexor.Shared.Filtering;

/// <summary>
///     Marks a property as filterable. Used by entities in opt-in filtering mode
///     (see <see cref="OptInFilteringAttribute" />) to explicitly whitelist which
///     fields the DSL exposes.
/// </summary>
/// <remarks>
///     <para>
///         Without a class-level <see cref="OptInFilteringAttribute" />, this
///         attribute is documentation-only — the registry still includes every
///         public property (opt-out contract). When a class is decorated with
///         <see cref="OptInFilteringAttribute" />, the registry includes ONLY
///         properties marked with <see cref="FilterableAttribute" />.
///     </para>
///     <para>
///         The opt-in switch exists to give sensitive entities (e.g. ones with
///         password hashes, internal flags, computed properties) a way to make
///         data enumeration impossible by accident. A new sensitive property
///         added without <c>[Filterable]</c> is invisible to the DSL.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterableAttribute : Attribute;

/// <summary>
///     Switches an entity into strict (opt-in) filtering mode: only properties
///     marked with <see cref="FilterableAttribute" /> are exposed by the registry.
///     Without this attribute, the registry uses the opt-out contract (every public
///     property is filterable unless <c>[NotMapped]</c> or an unfilterable type).
/// </summary>
/// <remarks>
///     <para>
///         Apply this to entities that carry sensitive data — credentials,
///         computed hashes, internal state — where forgetting
///         <c>[NotMapped]</c> on a new property must not silently expose it through
///         the filter DSL.
///     </para>
///     <para>
///         Adding this attribute to an existing entity is a <b>breaking change</b>
///         for any consumer that was filtering on a property without
///         <see cref="FilterableAttribute" /> — those queries will start failing
///         with <see cref="FilterParseException" /> (<c>Unknown field</c>). Plan
///         the migration: add <see cref="FilterableAttribute" /> to every field
///         the consumers currently filter on, then add this class-level attribute.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class OptInFilteringAttribute : Attribute;
