using Plexor.Modules.Audit.Application.Abstractions;
using Plexor.Modules.Audit.Application.Common;
using Plexor.Modules.Audit.Unit.TestDoubles;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Audit.Unit;

/// <summary>
///     Behavioural tests for <see cref="IAuditStore" />, exercised
///     against the <see cref="InMemoryAuditStore" /> test double. The
///     double mirrors the EF adapter's semantics (append-only, DESC
///     by <c>occurred_at</c>), so handler-level tests pass identically
///     against either.
/// </summary>
public sealed class AuditStoreShould
{
    private static readonly Guid OrgA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrgB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ClusterResourceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static AuditEntry Build(
        Guid orgId = default,
        Guid actorId = default,
        AuditActor actor = AuditActor.User,
        string action = "user.sign_in",
        string? resourceType = null,
        Guid? resourceId = null,
        AuditOutcome outcome = AuditOutcome.Succeeded,
        DateTimeOffset? occurredAt = null)
    {
        return new AuditEntry(
            Id: Guid.NewGuid(),
            OrgId: orgId == default ? OrgA : orgId,
            Actor: actor,
            ActorId: actorId == default ? ActorA : actorId,
            Action: action,
            ResourceType: resourceType,
            ResourceId: resourceId,
            Outcome: outcome,
            ErrorCode: null,
            Metadata: null,
            OccurredAt: occurredAt ?? DateTimeOffset.UtcNow);
    }

    [Fact(DisplayName = "Given RecordAsync then QueryAsync returns the recorded entry")]
    public async Task RecordAsyncThenQueryAsyncReturnsEntryAsync()
    {
        var store = new InMemoryAuditStore();
        var entry = Build(action: "user.sign_in");

        await store.RecordAsync(entry, CancellationToken.None);

        var results = await store.QueryAsync(
            new AuditFilter(OrgId: entry.OrgId),
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(entry.Id);
        results[0].Action.ShouldBe("user.sign_in");
    }

    [Fact(DisplayName = "Given RecordBatchAsync then every entry is queryable")]
    public async Task RecordBatchAsyncThenAllEntriesQueryableAsync()
    {
        var store = new InMemoryAuditStore();
        var entries = new[]
        {
            Build(action: "user.sign_in"),
            Build(action: "user.sign_out"),
            Build(action: "user.password_change"),
        };

        await store.RecordBatchAsync(entries, CancellationToken.None);

        var results = await store.QueryAsync(
            new AuditFilter(OrgId: OrgA),
            CancellationToken.None);

        results.Count.ShouldBe(3);
        foreach (var expected in entries)
        {
            results.ShouldContain(actual => actual.Id == expected.Id);
        }
    }

    [Fact(DisplayName = "Given entries from two orgs, when QueryAsync filters by OrgId, then only that org returns")]
    public async Task QueryAsyncFiltersByOrgIdAsync()
    {
        var store = new InMemoryAuditStore();
        await store.RecordAsync(Build(orgId: OrgA), CancellationToken.None);
        await store.RecordAsync(Build(orgId: OrgB), CancellationToken.None);
        await store.RecordAsync(Build(orgId: OrgA), CancellationToken.None);

        var fromA = await store.QueryAsync(
            new AuditFilter(OrgId: OrgA),
            CancellationToken.None);
        var fromB = await store.QueryAsync(
            new AuditFilter(OrgId: OrgB),
            CancellationToken.None);

        fromA.Count.ShouldBe(2);
        fromB.Count.ShouldBe(1);
        fromA.ShouldAllBe(static entry => entry.OrgId == OrgA);
        fromB.ShouldAllBe(static entry => entry.OrgId == OrgB);
    }

    [Fact(DisplayName = "Given entries with varied fields, when QueryAsync filters, then only matching entries return")]
    public async Task QueryAsyncFiltersByActorActionAndResourceAsync()
    {
        var store = new InMemoryAuditStore();
        var now = DateTimeOffset.UtcNow;
        var range = new TimeRange(now.AddMinutes(-30), now.AddMinutes(30));

        await store.RecordAsync(
            Build(actorId: ActorA, action: "cluster.create", resourceType: "cluster", resourceId: ClusterResourceId, occurredAt: now),
            CancellationToken.None);
        await store.RecordAsync(
            Build(actorId: ActorB, action: "cluster.create", resourceType: "cluster", resourceId: ClusterResourceId, occurredAt: now),
            CancellationToken.None);
        await store.RecordAsync(
            Build(actorId: ActorA, action: "cluster.delete", resourceType: "cluster", resourceId: ClusterResourceId, occurredAt: now),
            CancellationToken.None);
        await store.RecordAsync(
            Build(actorId: ActorA, action: "vms.start", resourceType: "vm", resourceId: Guid.NewGuid(), occurredAt: now),
            CancellationToken.None);

        var byActor = await store.QueryAsync(
            new AuditFilter(OrgId: OrgA, ActorId: ActorA),
            CancellationToken.None);
        byActor.Count.ShouldBe(3);
        byActor.ShouldAllBe(static entry => entry.ActorId == ActorA);

        var byAction = await store.QueryAsync(
            new AuditFilter(OrgId: OrgA, Action: "cluster.create"),
            CancellationToken.None);
        byAction.Count.ShouldBe(2);
        byAction.ShouldAllBe(static entry => entry.Action == "cluster.create");

        var byResource = await store.QueryAsync(
            new AuditFilter(OrgId: OrgA, ResourceType: "vm"),
            CancellationToken.None);
        byResource.Count.ShouldBe(1);
        byResource.ShouldAllBe(static entry => entry.ResourceType == "vm");

        var byTimeRange = await store.QueryAsync(
            new AuditFilter(OrgId: OrgA, TimeRange: range),
            CancellationToken.None);
        byTimeRange.Count.ShouldBe(4);
    }

    [Fact(DisplayName = "Given entries from two actors, when QueryByActorAsync is called, then only that actor's entries return")]
    public async Task QueryByActorAsyncReturnsOnlyActorEntriesAsync()
    {
        var store = new InMemoryAuditStore();
        var now = DateTimeOffset.UtcNow;
        var range = new TimeRange(now.AddHours(-1), now.AddHours(1));

        await store.RecordAsync(
            Build(actorId: ActorA, action: "user.sign_in", occurredAt: now.AddMinutes(-30)),
            CancellationToken.None);
        await store.RecordAsync(
            Build(actorId: ActorA, action: "user.sign_out", occurredAt: now.AddMinutes(-10)),
            CancellationToken.None);
        await store.RecordAsync(
            Build(actorId: ActorB, action: "user.sign_in", occurredAt: now.AddMinutes(-20)),
            CancellationToken.None);

        var actorAEntries = await store.QueryByActorAsync(ActorA, range, CancellationToken.None);
        actorAEntries.Count.ShouldBe(2);
        actorAEntries.ShouldAllBe(static entry => entry.ActorId == ActorA);

        var actorBEntries = await store.QueryByActorAsync(ActorB, range, CancellationToken.None);
        actorBEntries.Count.ShouldBe(1);
        actorBEntries[0].ActorId.ShouldBe(ActorB);

        // DESC by occurred_at — most recent first.
        actorAEntries[0].Action.ShouldBe("user.sign_out");
        actorAEntries[1].Action.ShouldBe("user.sign_in");
    }
}
