using System.Text.Json;

namespace Plexor.Modules.Audit.Infrastructure.Persistence.Mappers;

/// <summary>
///     JSON round-trip helper for the audit <c>metadata</c> column.
///     Stored as <c>jsonb</c> in PostgreSQL — the C# surface uses
///     <see cref="IReadOnlyDictionary{TKey, TValue}" />, the storage
///     layer uses a JSON string.
/// </summary>
/// <remarks>
///     <para><b>Why a helper instead of inline serialisation.</b>
///     The mapper calls <see cref="Deserialize" /> /
///     <see cref="Serialize" /> at every read / write. Putting the
///     options reference (<c>JsonSerializerOptions.Web</c>) in one
///     place keeps the mapper readable and avoids accidentally
///     mutating the shared frozen options instance.</para>
///     <para><b>Why <c>JsonSerializerOptions.Web</c>.</b> The
///     framework-shipped frozen options handle camelCase + enum
///     strings + reference handling. We don't need any
///     project-specific converters for the metadata dictionary —
///     the values are untyped (<c>object?</c>), so STJ's default
///     handling is sufficient.</para>
///     <para><b>Empty / null policy.</b> A <c>null</c> or empty
///     dictionary serialises to <c>"{}"</c> — never <c>"null"</c>.
///     PostgreSQL's <c>jsonb</c> column requires a valid JSON
///     value; an empty object is the canonical "no metadata"
///     representation.</para>
/// </remarks>
internal static class MetadataJson
{
    /// <summary>
    ///     Serialise a metadata dictionary to a JSON string for
    ///     storage. <c>null</c> and empty inputs both produce
    ///     <c>"{}"</c>.
    /// </summary>
    /// <param name="metadata">The metadata dictionary; may be <c>null</c> or empty.</param>
    /// <returns>A valid JSON object string. Never <c>null</c>.</returns>
    public static string Serialize(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(metadata, JsonSerializerOptions.Web);
    }

    /// <summary>
    ///     Deserialise a JSON string back to a metadata dictionary.
    ///     <c>null</c> / empty input → <c>null</c> (no metadata
    ///     recorded for this row).
    /// </summary>
    /// <param name="json">The raw <c>jsonb</c> string from the database.</param>
    /// <returns>The metadata dictionary, or <c>null</c> when the input is empty.</returns>
    public static IReadOnlyDictionary<string, object?>? Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonSerializerOptions.Web);
    }
}
