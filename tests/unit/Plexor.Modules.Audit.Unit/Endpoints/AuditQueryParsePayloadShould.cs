// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditQueryParsePayloadShould — exercise the defensive payload parser
// inside AuditQueryEndpoint. The endpoint must never fail a response
// because one row's payload_json is malformed; the parser surfaces
// the original text under a single _raw key when JSON parsing fails.
// ============================================================================

using Plexor.Modules.Audit.Api.Endpoints;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Audit.Unit.Endpoints;

/// <summary>
///     Pin the <see cref="AuditQueryEndpoint.ParsePayload" /> contract:
///     well-formed JSON dict → parsed dict; non-JSON → single
///     <c>_raw</c> entry holding the original text. The defensive
///     path matters because a single malformed row must not fail
///     the whole list response (Phase 5.2 admin timeline UX).
/// </summary>
public sealed class AuditQueryParsePayloadShould
{
    /// <summary>Given a well-formed JSON object, when ParsePayload
    /// runs, then the dict comes back with the same keys.</summary>
    [Fact(DisplayName = "Given a well-formed JSON object, when ParsePayload runs, then keys are preserved")]
    public void ParsePayload_WithJsonObject_ReturnsDictAsync()
    {
        var parsed = AuditQueryEndpoint.ParsePayload(
            "{\"definition_key\":\"compute.vms.count\",\"scope_kind\":\"Org\"}");

        parsed.Count.ShouldBe(2);
        parsed.ShouldContainKey("definition_key");
        parsed.ShouldContainKey("scope_kind");
    }

    /// <summary>Given an array at the top level (not an object),
    /// when ParsePayload runs, then the parser falls back to the
    /// <c>_raw</c> shape — arrays can't be projected into the
    /// IReadOnlyDictionary-of-string-and-object contract.</summary>
    [Fact(DisplayName = "Given a JSON array at the top level, when ParsePayload runs, then falls back to _raw")]
    public void ParsePayload_WithJsonArray_FallsBackToRawAsync()
    {
        var parsed = AuditQueryEndpoint.ParsePayload("[1, 2, 3]");

        parsed.Count.ShouldBe(1);
        parsed.ShouldContainKey("_raw");
    }

    /// <summary>Given a non-JSON string, when ParsePayload runs,
    /// then the original text surfaces under <c>_raw</c> (no
    /// exception escapes).</summary>
    [Fact(DisplayName = "Given a non-JSON string, when ParsePayload runs, then surfaces as _raw")]
    public void ParsePayload_WithNonJson_ReturnsRawAsync()
    {
        var parsed = AuditQueryEndpoint.ParsePayload("not json at all");

        parsed.ShouldContainKey("_raw");
        parsed["_raw"].ShouldBe("not json at all");
    }
}
