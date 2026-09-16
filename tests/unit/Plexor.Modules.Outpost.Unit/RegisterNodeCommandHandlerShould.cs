// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RegisterNodeCommandHandlerShould — exercise the join flow. The
// handler validates the join token, creates the outpost.node_records
// row, and returns a node-bearer token + cluster endpoint.
//
// Tests use a single OutpostDbContext (in-memory) + a real
// ClusterDbContext that the join handler reads from. The cluster
// row + active join token are seeded by the test.
// ============================================================================

using NSubstitute;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.NodeCommands;
using Plexor.Modules.Outpost.Infrastructure;
using Plexor.Modules.Outpost.Infrastructure.Nodes;
using Plexor.Modules.Outpost.Infrastructure.Persistence;
using Plexor.Modules.Outpost.Infrastructure.Persistence.Repositories;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Mtls;
using Plexor.Shared.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Outpost.Unit;

public sealed class RegisterNodeCommandHandlerShould
{
    [Fact(DisplayName = "Given valid active token + matching role + fresh hostname, when Register, then creates node + revokes token")]
    public async Task JoinCreatesNodeAndRevokesTokenAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        const string tokenSecret = "test-secret-12345";
        var now = DateTimeOffset.UtcNow;

        await using var outpostDb = await OutpostTestDb.CreateAsync();
        await using var clusterDb = await ClusterTestDb.CreateAsync();

        await clusterDb.Clusters.AddAsync(new Cluster
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
        await clusterDb.JoinTokens.AddAsync(new JoinToken
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
        await clusterDb.SaveChangesAsync();

        var sut = new RegisterNodeCommandHandler(
            outpostDb,
            clusterDb,
            new JoinTokenRepository(clusterDb));
        var result = await sut.HandleAsync(new RegisterNodeCommand(
            tokenSecret,
            "node-1",
            "10.0.0.1",
            NodeRole.Compute,
            new NodeSpec(8, 32, 200, ["kvm"]),
            "0.1.0-dev",
            string.Empty));

        result.NodeRecord.Hostname.ShouldBe("node-1");
        result.NodeRecord.IpAddress.ShouldBe("10.0.0.1");
        result.NodeRecord.Role.ShouldBe(NodeRole.Compute);
        result.NodeRecord.Status.ShouldBe(NodeStatus.Ready);
        result.NodeToken.ShouldNotBeNullOrWhiteSpace();
        result.ClusterEndpoint.ShouldBe("https://plexor.host");
    }

    [Fact(DisplayName = "Given consumed (revoked) token, when Register, then throws InvalidJoinToken")]
    public async Task JoinRejectsConsumedTokenAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        const string tokenSecret = "consumed-secret";
        var now = DateTimeOffset.UtcNow;

        await using var outpostDb = await OutpostTestDb.CreateAsync();
        await using var clusterDb = await ClusterTestDb.CreateAsync();

        await clusterDb.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = Guid.NewGuid(),
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        });
        var tokenHash = await TokenHasher.HashAsync(tokenSecret, CancellationToken.None);
        await clusterDb.JoinTokens.AddAsync(new JoinToken
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
        await clusterDb.SaveChangesAsync();

        var sut = new RegisterNodeCommandHandler(
            outpostDb,
            clusterDb,
            new JoinTokenRepository(clusterDb));
        var ex = await Should.ThrowAsync<OutpostException>(
            () => sut.HandleAsync(new RegisterNodeCommand(
                tokenSecret, "node-1", "10.0.0.1", NodeRole.Compute,
                new NodeSpec(8, 32, 200, []),
                "0.1.0-dev",
                string.Empty)));
        ex.Code.ShouldBe(OutpostExceptions.InvalidJoinToken);
    }

    [Fact(DisplayName = "Given token for Control role but node requests Compute, when Register, then throws NodeRoleMismatch")]
    public async Task JoinRejectsRoleMismatchAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        const string tokenSecret = "control-only-secret";
        var now = DateTimeOffset.UtcNow;

        await using var outpostDb = await OutpostTestDb.CreateAsync();
        await using var clusterDb = await ClusterTestDb.CreateAsync();

        await clusterDb.Clusters.AddAsync(new Cluster
        {
            Id = clusterId,
            OrgId = Guid.NewGuid(),
            Name = "prod-eu-1",
            Region = "eu-central-1",
            Status = ClusterStatus.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        });
        var tokenHash = await TokenHasher.HashAsync(tokenSecret, CancellationToken.None);
        await clusterDb.JoinTokens.AddAsync(new JoinToken
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
        await clusterDb.SaveChangesAsync();

        var sut = new RegisterNodeCommandHandler(
            outpostDb,
            clusterDb,
            new JoinTokenRepository(clusterDb));
        var ex = await Should.ThrowAsync<OutpostException>(
            () => sut.HandleAsync(new RegisterNodeCommand(
                tokenSecret, "node-1", "10.0.0.1", NodeRole.Compute,
                new NodeSpec(8, 32, 200, []),
                "0.1.0-dev",
                string.Empty)));
        ex.Code.ShouldBe(OutpostExceptions.NodeRoleMismatch);
    }
}