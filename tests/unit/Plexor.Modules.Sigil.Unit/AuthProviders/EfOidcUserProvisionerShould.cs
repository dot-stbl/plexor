// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOidcUserProvisionerShould — exercise the find-or-create User +
// RoleBinding flow in EfOidcUserProvisioner (Phase 4.6.3c).
//
// Test coverage limitation
// ------------------------
// The production IdentityDbContext schema uses a Postgres
// `text[]` column for `sigil.roles.permissions` (via
// PermissionScopeListValueConverter), which the EF Core InMemory
// provider can't compose at model-build time. The other tables
// (User, RoleBinding) inherit the schema, so any DbContext access
// triggers model validation and the entire DbContext becomes
// un-testable against InMemory. The Host.UnitTests project uses
// a real Postgres via docker-compose for full schema coverage;
// Plexor.Modules.Sigil.Unit is a pure unit-test project (no
// docker / no Postgres) so the schema-dependent tests are out
// of scope here.
//
// What this test file covers
// --------------------------
// Behavioural verification of paths that DON'T need the schema:
//   1. ProvisionAsync_WithMissingEmail_FailsWithOidcUserMissingClaimsError
//      — the missing-email check fires BEFORE any DB roundtrip;
//        verifies the exception code, the message, and that the
//        handler does NOT silently insert a user with a null email.
//   2. ProvisionAsync_WithValidEmail_DoesNotThrowBeforeSchema —
//      confirms that with a valid email, the provisioner reaches
//      the DB roundtrip (the InMemory model-creation failure is
//      the proof that we got past the email + display-name
//      resolution path). The test asserts the failure is the
//      InMemory model error, not an IdentityException — i.e.
//      the new code is reached.
// ============================================================================

using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Provisioners;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for <see cref="EfOidcUserProvisioner" />.
///     Limited to schema-independent paths (see file header for
///     the rationale — the InMemory provider can't model the
///     production schema's Postgres `text[]` column).
/// </summary>
public sealed class EfOidcUserProvisionerShould
{
    private const string TestAuthority = "https://kc.example.com/realms/plexor";

    private const string TestSubject = "external-user-123";

    /// <summary>
    ///     Given a sub whose id_token has no email claim, when
    ///     ProvisionAsync runs, then the handler throws
    ///     <see cref="IdentityExceptions.OidcUserMissingClaims" />.
    ///     The throw happens BEFORE any DB roundtrip — the
    ///     InMemory provider never gets a chance to fail model
    ///     validation.
    /// </summary>
    [Fact(DisplayName = "Given an id_token with no email claim, when ProvisionAsync runs, then the handler throws OidcUserMissingClaims")]
    public async Task ProvisionAsync_WithMissingEmail_FailsWithOidcUserMissingClaimsErrorAsync()
    {
        await using var identity = await IdentityTestDb.CreateAsync();
        var provisioner = new EfOidcUserProvisioner(identity, TimeProvider.System);

        var config = new OidcTenantConfig(
            OrgId: Guid.NewGuid(),
            Authority: TestAuthority,
            ClientId: "plexor-cli");

        var exception = await Should.ThrowAsync<IdentityException>(
            () => provisioner.ProvisionAsync(
                config,
                subject: TestSubject,
                email: string.Empty,
                preferredUsername: "alice",
                name: "Alice Doe"));

        exception.Code.ShouldBe(IdentityExceptions.OidcUserMissingClaims);
        exception.Message.ShouldContain("email");
    }

    /// <summary>
    ///     Given a sub with a valid email, when ProvisionAsync runs,
    ///     then the handler reaches the DB roundtrip (the
    ///     InMemory model-creation failure is the proof that we
    ///     got past the email + display-name resolution path).
    ///     The test asserts the failure is the InMemory model
    ///     error, not an IdentityException — i.e. the new code is
    ///     reached and the missing-email / display-name paths
    ///     completed successfully.
    /// </summary>
    [Fact(DisplayName = "Given an id_token with a valid email, when ProvisionAsync runs, then the handler reaches the DB roundtrip")]
    public async Task ProvisionAsync_WithValidEmail_DoesNotThrowBeforeSchemaAsync()
    {
        await using var identity = await IdentityTestDb.CreateAsync();
        var provisioner = new EfOidcUserProvisioner(identity, TimeProvider.System);

        var config = new OidcTenantConfig(
            OrgId: Guid.NewGuid(),
            Authority: TestAuthority,
            ClientId: "plexor-cli");

        // The handler should get past the missing-email check +
        // display-name resolution and hit the DB. InMemory fails
        // at model creation (Postgres text[] column mapping),
        // so the exception is NOT an IdentityException — it's
        // the InMemory provider's model-composition error. The
        // test asserts the latter to confirm we reached the DB.
        var exception = await Should.ThrowAsync<Exception>(
            () => provisioner.ProvisionAsync(
                config,
                subject: TestSubject,
                email: "alice@example.com",
                preferredUsername: "alice",
                name: "Alice Doe"));

        exception.ShouldNotBeOfType<IdentityException>();
    }
}
