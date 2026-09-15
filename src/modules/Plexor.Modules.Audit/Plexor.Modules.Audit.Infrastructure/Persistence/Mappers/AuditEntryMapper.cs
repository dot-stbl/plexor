using Plexor.Modules.Audit.Application.Abstractions;

namespace Plexor.Modules.Audit.Infrastructure.Persistence.Mappers;

/// <summary>
///     Hand-written static mapper between
///     <see cref="AuditEntryRecord" /> (EF row) and
///     <see cref="AuditEntry" /> (Application-layer port type).
///     Per <c>mapper.md</c>: <c>internal static class</c>, pure
///     functions, no I/O, no DI. Tested directly.
/// </summary>
/// <remarks>
///     <para><b>Why a hand-written mapper, not Mapperly.</b>
///     The Plexor codebase uses Mapperly for entity → DTO mapping
///     (the public surface), but uses hand-written static mappers
///     for EF row ↔ domain record (the internal persistence seam —
///     see <c>mapper.md</c>). This boundary is small (one record
///     type, one direction each way) and the JSON metadata
///     round-trip is easier to read inline than via a custom
///     Mapperly converter.</para>
///     <para><b>Single-direction surface.</b> The mapper is
///     direction-specific: <see cref="ToDomain" /> for reads,
///     <see cref="ToRecord" /> for writes. There's no "deep
///     mapper" — the EF store calls each method as needed.</para>
/// </remarks>
internal static class AuditEntryMapper
{
    /// <summary>
    ///     Map an EF row to the Application-layer record.
    ///     Deserialises the <c>metadata</c> JSON string into a
    ///     dictionary.
    /// </summary>
    /// <param name="record">The EF row read from the database.</param>
    /// <returns>The Application-layer port type.</returns>
    public static AuditEntry ToDomain(AuditEntryRecord record)
    {
        return new AuditEntry(
            record.Id,
            record.OrgId,
            record.Actor,
            record.ActorId,
            record.Action,
            record.ResourceType,
            record.ResourceId,
            record.Outcome,
            record.ErrorCode,
            MetadataJson.Deserialize(record.MetadataJson),
            record.OccurredAt);
    }

    /// <summary>
    ///     Map an Application-layer record to an EF row. Serialises
    ///     the metadata dictionary to a JSON string for the
    ///     <c>jsonb</c> column.
    /// </summary>
    /// <param name="entry">The Application-layer port type.</param>
    /// <returns>The EF row ready for INSERT.</returns>
    public static AuditEntryRecord ToRecord(AuditEntry entry)
    {
        return new AuditEntryRecord
        {
            Id = entry.Id,
            OrgId = entry.OrgId,
            Actor = entry.Actor,
            ActorId = entry.ActorId,
            Action = entry.Action,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            Outcome = entry.Outcome,
            ErrorCode = entry.ErrorCode,
            MetadataJson = MetadataJson.Serialize(entry.Metadata),
            OccurredAt = entry.OccurredAt,
        };
    }
}
