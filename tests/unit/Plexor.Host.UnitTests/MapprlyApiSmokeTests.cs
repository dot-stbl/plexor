// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MapprlyApiSmokeTests — verifies the Mapperly source-generated
// UserSummary projection works against a real Postgres. We pull
// users through EF Core LINQ, materialize them, then run Mapperly
// on the materialized entity. We avoid WebApplicationFactory<Program>
// here because the host's startup-time service provider validation
// rejects a few pre-existing captive dependencies (singleton services
// capturing scoped repositories) that are an engineering-zone backlog.
// The Mapperly translation pipeline is also covered by the existing
// pure-POCO unit tests in Plexor.Modules.Sigil.Unit via dotnet build
// of the SigilMapper.Mapper.g.cs file; this test exercises the
// real-database retrieval path the mappers will be used on in
// production.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Mappers;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Host.UnitTests;

public sealed class MapprlyApiSmokeTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture fixture;

    public MapprlyApiSmokeTests(PostgresFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact(DisplayName = "Given Mapperly-generated UserSummary projection, when EF Core materializes then mapper projects, then DTO field shape matches entity")]
    public async Task MapperlyUserSummaryProjectsEndToEndFromPostgres()
    {
        // Arrange: seed one user via the DbContext so we have a row
        // to fetch + project through Mapperly.
        var mapper = new SigilMapper();
        var email = $"smoke-{Guid.NewGuid():N}@plexor.local";
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using (var db = await fixture.NewDbContextAsync())
        {
            await db.Users.AddAsync(new User
            {
                Id = userId,
                OrgId = orgId,
                Email = new Email(email),
                DisplayName = "Mapper Smoke",
                Status = "active",
                FailedLoginCount = 0,
                LockedUntil = null,
                LastLoginAt = null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                PasswordHash = null,
            });
            await db.SaveChangesAsync();
        }

        // Act: pull the user via EF Core (where the value-object
        // conversion runs automatically) and run Mapperly on the
        // materialized entity.
        await using (var db = await fixture.NewDbContextAsync())
        {
            var user = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .FirstAsync();

            var summary = mapper.ToUserSummary(user);

            // Assert — the Mapperly-generated body must have projected
            // every field 1:1; if any mapping is missing the build
            // would have errored at design-time (Target strategy).
            summary.Id.ShouldBe(userId);
            summary.Email.ShouldBe(email);
            summary.DisplayName.ShouldBe("Mapper Smoke");
            summary.Status.ShouldBe("active");
            summary.OrgId.ShouldBe(orgId);
            summary.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
            summary.UpdatedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
            summary.LastLoginAt.ShouldBeNull();
        }
    }
}
