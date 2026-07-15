using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class NodeJoinCommandHandlerShould
{
    [Fact(DisplayName = "Given valid active token + matching role + fresh hostname, when Join, then creates node + revokes token")]
    public async Task JoinCreatesNodeAndRevokesToken()
    {
        var clusterId = IdGenerator.NewClusterId();
        var tokenSecret = "test-secret-12345";
        await using var db = await TestDb.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await db.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = Guid.NewGuid(),
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            Endpoint = "https://plexor.host",
            CreatedAt = now,
            UpdatedAt = now,
        });
        var tokenHash = await TokenHasher.HashAsync(tokenSecret, CancellationToken.None);
        await db.JoinTokens.AddAsync(new JoinToken
        {
            Id = IdGenerator.NewTokenId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Label = "initial",
            Status = TokenStatus.Active,
            TokenHash = tokenHash,
            IntendedRole = NodeRole.Compute,
            IssuedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        });
        await db.SaveChangesAsync();

        var sut = new NodeJoinCommandHandler(db, new JoinTokenRepository(db));
        var result = await sut.HandleAsync(new NodeJoinCommand(
            tokenSecret,
            "node-1",
            NodeRole.Compute,
            new NodeSpec(8, 32, 200, ["kvm"])));

        result.NodeId.ShouldNotBe(default(NodeId));
        result.ClusterId.ShouldBe(clusterId);
        result.NodeToken.ShouldNotBeNullOrWhiteSpace();

        var node = await db.Nodes.SingleAsync();
        node.Hostname.ShouldBe("node-1");
        node.Status.ShouldBe(NodeStatus.Ready);
        node.Spec.Vcpu.ShouldBe(8);
    }

    [Fact(DisplayName = "Given consumed (revoked) token, when Join, then throws InvalidJoinToken")]
    public async Task JoinRejectsConsumedToken()
    {
        var clusterId = IdGenerator.NewClusterId();
        var tokenSecret = "consumed-secret";
        await using var db = await TestDb.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await db.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = Guid.NewGuid(),
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            Endpoint = "https://plexor.host",
            CreatedAt = now,
            UpdatedAt = now,
        });
        var tokenHash = await TokenHasher.HashAsync(tokenSecret, CancellationToken.None);
        await db.JoinTokens.AddAsync(new JoinToken
        {
            Id = IdGenerator.NewTokenId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Label = "initial",
            Status = TokenStatus.Revoked,
            TokenHash = tokenHash,
            IntendedRole = NodeRole.Compute,
            IssuedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        });
        await db.SaveChangesAsync();

        var sut = new NodeJoinCommandHandler(db, new JoinTokenRepository(db));
        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new NodeJoinCommand(
                tokenSecret, "node-1", NodeRole.Compute, new NodeSpec(8, 32, 200, []))));
        ex.Code.ShouldBe(ClustersExceptions.InvalidJoinToken);
    }

    [Fact(DisplayName = "Given token for Control role but node requests Compute, when Join, then throws InvalidJoinToken")]
    public async Task JoinRejectsRoleMismatch()
    {
        var clusterId = IdGenerator.NewClusterId();
        var tokenSecret = "control-only-secret";
        await using var db = await TestDb.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await db.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = Guid.NewGuid(),
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            Endpoint = "https://plexor.host",
            CreatedAt = now,
            UpdatedAt = now,
        });
        var tokenHash = await TokenHasher.HashAsync(tokenSecret, CancellationToken.None);
        await db.JoinTokens.AddAsync(new JoinToken
        {
            Id = IdGenerator.NewTokenId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Label = "control-only",
            Status = TokenStatus.Active,
            TokenHash = tokenHash,
            IntendedRole = NodeRole.Control,
            IssuedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        });
        await db.SaveChangesAsync();

        var sut = new NodeJoinCommandHandler(db, new JoinTokenRepository(db));
        var ex = await Should.ThrowAsync<ClustersException>(
            () => sut.HandleAsync(new NodeJoinCommand(
                tokenSecret, "node-1", NodeRole.Compute, new NodeSpec(8, 32, 200, []))));
        ex.Code.ShouldBe(ClustersExceptions.InvalidJoinToken);
    }
}
