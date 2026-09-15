using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Clusters;

public sealed class RotateJoinTokenCommandHandlerShould
{
    [Fact(DisplayName = "Given active token, when RotateJoinToken, then returns new secret + revokes old token")]
    public async Task RotateJoinTokenReturnsNewSecretAndRevokesOldAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        const string oldSecret = "old-rotate-secret";
        await using var db = await TestDb.SqliteAsync();
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
        var oldHash = await TokenHasher.HashAsync(oldSecret, CancellationToken.None);
        await db.JoinTokens.AddAsync(new JoinToken
        {
            Id = IdGenerator.NewTokenId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Label = "initial",
            Status = TokenStatus.Active,
            TokenHash = oldHash,
            IntendedRole = NodeRole.Compute,
            IssuedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        });
        await db.SaveChangesAsync();

        var sut = new RotateJoinTokenCommandHandler(db, TimeProvider.System);
        var result = await sut.HandleAsync(new RotateJoinTokenCommand(clusterId));

        result.ClusterId.ShouldBe(clusterId);
        result.Token.ShouldNotBeNullOrWhiteSpace();
        result.Token.ShouldNotBe(oldSecret);
        result.Endpoint.ShouldBe("https://plexor.host");
        result.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        db.ChangeTracker.Clear();
        var tokens = await db.JoinTokens.AsNoTracking().ToArrayAsync();
        tokens.Length.ShouldBe(2);
        tokens.Single(t => t.TokenHash == oldHash).Status.ShouldBe(TokenStatus.Revoked);
        var newToken = tokens.Single(t => t.TokenHash != oldHash);
        newToken.Status.ShouldBe(TokenStatus.Active);
        var newSecretHash = await TokenHasher.HashAsync(result.Token, CancellationToken.None);
        newToken.TokenHash.ShouldBe(newSecretHash);
    }

    [Fact(DisplayName = "Given no active tokens, when RotateJoinToken, then creates first token (no rows to revoke)")]
    public async Task RotateJoinTokenCreatesFirstTokenWhenNoneActiveAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        await using var db = await TestDb.SqliteAsync();
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
        await db.SaveChangesAsync();

        var sut = new RotateJoinTokenCommandHandler(db, TimeProvider.System);
        var result = await sut.HandleAsync(new RotateJoinTokenCommand(clusterId));

        result.Token.ShouldNotBeNullOrWhiteSpace();

        db.ChangeTracker.Clear();
        var tokens = await db.JoinTokens.AsNoTracking().ToArrayAsync();
        tokens.Length.ShouldBe(1);
        tokens[0].Status.ShouldBe(TokenStatus.Active);
        tokens[0].ClusterId.ShouldBe(clusterId);
        tokens[0].IntendedRole.ShouldBe(NodeRole.Compute);

        var persistedHash = await TokenHasher.HashAsync(result.Token, CancellationToken.None);
        tokens[0].TokenHash.ShouldBe(persistedHash);
    }

    [Fact(DisplayName = "Given rotated token, when NodeJoin uses the old secret, then throws InvalidJoinToken")]
    public async Task RotateJoinTokenRevokesOldTokenAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        const string oldSecret = "stale-after-rotate-secret";
        await using var db = await TestDb.SqliteAsync();
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
        var oldHash = await TokenHasher.HashAsync(oldSecret, CancellationToken.None);
        await db.JoinTokens.AddAsync(new JoinToken
        {
            Id = IdGenerator.NewTokenId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Label = "initial",
            Status = TokenStatus.Active,
            TokenHash = oldHash,
            IntendedRole = NodeRole.Compute,
            IssuedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        });
        await db.SaveChangesAsync();

        var rotater = new RotateJoinTokenCommandHandler(db, TimeProvider.System);
        await rotater.HandleAsync(new RotateJoinTokenCommand(clusterId));

        // ExecuteUpdate bypasses the change tracker; the tracker still
        // has the old token row stamped Active from the seed. The
        // JoinTokenByHashSpec doesn't AsNoTracking, so subsequent
        // Repository reads return the tracked (stale) row. Clear the
        // tracker to force a fresh DB read.
        db.ChangeTracker.Clear();

        var joiner = new NodeJoinCommandHandler(
            db,
            new JoinTokenRepository(db),
            new InMemoryCertificateAuthority(),
            TimeProvider.System);
        var ex = await Should.ThrowAsync<ClustersException>(() =>
            joiner.HandleAsync(new NodeJoinCommand(
                oldSecret,
                "node-1",
                NodeRole.Compute,
                new NodeSpec(4, 16, 100, []))));

        ex.Code.ShouldBe(ClustersExceptions.InvalidJoinToken);
    }

    [Fact(DisplayName = "Given rotated token, when NodeJoin uses the new secret, then succeeds")]
    public async Task RotateJoinTokenLeavesNewTokenRedeemableAsync()
    {
        var clusterId = IdGenerator.NewClusterId();
        const string oldSecret = "to-be-revoked-secret";
        await using var db = await TestDb.SqliteAsync();
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
        var oldHash = await TokenHasher.HashAsync(oldSecret, CancellationToken.None);
        await db.JoinTokens.AddAsync(new JoinToken
        {
            Id = IdGenerator.NewTokenId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Label = "initial",
            Status = TokenStatus.Active,
            TokenHash = oldHash,
            IntendedRole = NodeRole.Compute,
            IssuedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        });
        await db.SaveChangesAsync();

        var rotater = new RotateJoinTokenCommandHandler(db, TimeProvider.System);
        var rotateResult = await rotater.HandleAsync(new RotateJoinTokenCommand(clusterId));

        var joiner = new NodeJoinCommandHandler(
            db,
            new JoinTokenRepository(db),
            new InMemoryCertificateAuthority(),
            TimeProvider.System);
        var joinResult = await joiner.HandleAsync(new NodeJoinCommand(
            rotateResult.Token,
            "node-rotated",
            NodeRole.Compute,
            new NodeSpec(8, 32, 200, [])));

        joinResult.ClusterId.ShouldBe(clusterId);
        joinResult.NodeToken.ShouldNotBeNullOrWhiteSpace();

        db.ChangeTracker.Clear();
        var node = await db.Nodes.AsNoTracking().SingleAsync();
        node.Hostname.ShouldBe("node-rotated");
        node.Status.ShouldBe(NodeStatus.Ready);
    }
}

