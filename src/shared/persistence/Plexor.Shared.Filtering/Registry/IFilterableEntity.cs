namespace Plexor.Shared.Filtering.Registry;

/// <summary>
///     Marker for entity types whose properties should be exposed through the
///     filter DSL on their OpenAPI schema. Wired into
///     <c>FilterableSchemaTransformer</c> via <c>AddFilterableEntity&lt;T&gt;</c>;
///     the transformer attaches <c>x-filterable</c> + <c>x-sortable</c> extensions
///     to the schema so the frontend kubb plugin can generate typed filter builders.
/// </summary>
/// <remarks>
///     <para>
///         This is opt-in: entities not implementing the interface are
///         invisible to the transformer even if a schema with the same name
///         appears in the OpenAPI document. The marker is the explicit signal
///         that "this entity is filterable in the API" — public surface area,
///         not an EF/db attribute.
///     </para>
/// </remarks>
public interface IFilterableEntity;
