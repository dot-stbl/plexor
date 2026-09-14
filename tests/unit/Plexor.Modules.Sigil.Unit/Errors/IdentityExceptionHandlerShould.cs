// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentityExceptionHandlerShould — exercise the RFC 9457 ProblemDetails
// mapping for IdentityException. Each test drives the handler against a
// synthetic DefaultHttpContext and asserts the resulting status + body
// shape. No DB, no auth scheme — pure handler behaviour.
// ============================================================================

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.Errors;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Errors;

/// <summary>
///     Behavioural tests for <see cref="IdentityExceptionHandler" />.
///     Verifies that <see cref="IdentityException" /> (and its codes) maps
///     to a ProblemDetails body with the canonical status, the
///     <c>/errors/&lt;code&gt;</c> type URI, the discriminator in the
///     title, and the exception message in the detail. Inner-exception
///     metadata surfaces only as a type name in <c>Extensions</c> — never
///     in the client-visible detail (per exceptions.md §4 — no PII).
/// </summary>
public sealed class IdentityExceptionHandlerShould
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static async Task<ProblemDetails> HandleAsync(IdentityException exception)
    {
        var handler = new IdentityExceptionHandler(NullLogger<IdentityExceptionHandler>.Instance);
        var context = new DefaultHttpContext
        {
            Request = { Path = "/auth/login" },
        };
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBeGreaterThanOrEqualTo(400);

        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            JsonOptions,
            CancellationToken.None);

        problem.ShouldNotBeNull();
        return problem!;
    }

    /// <summary>Generic <see cref="IdentityException" /> with the
    /// credentials-invalid code maps to status 401, type
    /// <c>/errors/identity.credentials.invalid</c>, and the message
    /// surfaces verbatim in <c>detail</c>.</summary>
    [Fact(DisplayName = "Given generic IdentityException with invalid credentials code, when handled, then status is 401 and ProblemDetails carries the discriminator")]
    public async Task IdentityException_MapsToProblemDetailsWithCorrectStatus()
    {
        var problem = await HandleAsync(
            new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "The supplied credentials are invalid."));

        problem.Status.ShouldBe(StatusCodes.Status401Unauthorized);
        problem.Type.ShouldBe($"/errors/{IdentityExceptions.InvalidCredentials}");
        problem.Title.ShouldBe(IdentityExceptions.InvalidCredentials);
        problem.Detail.ShouldBe("The supplied credentials are invalid.");
        problem.Instance.ShouldBe("/auth/login");
    }

    /// <summary><see cref="IdentityExceptions.AccountLocked" /> maps to
    /// 423 Locked (RFC 4918 §11.3 — a state the resource is in, not a
    /// permission failure).</summary>
    [Fact(DisplayName = "Given AccountLocked code, when handled, then status is 423 Locked")]
    public async Task AccountLockedException_Returns423()
    {
        var problem = await HandleAsync(
            new IdentityException(
                IdentityExceptions.AccountLocked,
                "Account is locked due to repeated failed login attempts."));

        problem.Status.ShouldBe(StatusCodes.Status423Locked);
        problem.Type.ShouldBe($"/errors/{IdentityExceptions.AccountLocked}");
    }

    /// <summary><see cref="IdentityExceptions.AccountSuspended" /> maps
    /// to 403 Forbidden — suspension is an administrative state, not a
    /// bad request or a transient lock.</summary>
    [Fact(DisplayName = "Given AccountSuspended code, when handled, then status is 403 Forbidden")]
    public async Task AccountSuspendedException_Returns403()
    {
        var problem = await HandleAsync(
            new IdentityException(
                IdentityExceptions.AccountSuspended,
                "Account has been suspended by an administrator."));

        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Type.ShouldBe($"/errors/{IdentityExceptions.AccountSuspended}");
    }

    /// <summary><see cref="IdentityExceptions.RefreshTokenReplayed" />
    /// maps to 401 Unauthorized — from the client's perspective a
    /// replayed refresh token is indistinguishable from invalid
    /// credentials (the family has been revoked).</summary>
    [Fact(DisplayName = "Given refresh-token replay code, when handled, then status is 401 Unauthorized")]
    public async Task TokenReplayedException_Returns401()
    {
        var problem = await HandleAsync(
            new IdentityException(
                IdentityExceptions.RefreshTokenReplayed,
                "Refresh token has already been used; the token family has been revoked."));

        problem.Status.ShouldBe(StatusCodes.Status401Unauthorized);
        problem.Type.ShouldBe($"/errors/{IdentityExceptions.RefreshTokenReplayed}");
    }

    /// <summary>When <see cref="IdentityException" /> carries an inner
    /// exception, the handler must surface the inner type as
    /// machine-readable metadata (<c>Extensions["innerType"]</c>) but
    /// never leak the inner message or a stack trace into
    /// <c>detail</c> (per exceptions.md §4 — no PII / secrets in error
    /// responses). The outer message goes into <c>detail</c>; the inner
    /// message must not appear anywhere in the body.</summary>
    [Fact(DisplayName = "Given IdentityException with inner, when handled, then inner type appears in extensions but detail omits inner message")]
    public async Task InnerExceptionIncludedInMetadataButNotInDetail()
    {
        var inner = new InvalidOperationException(
            "database connection refused at host 10.0.0.5 — credentials db1/prod-secret");

        var problem = await HandleAsync(
            new IdentityException(
                IdentityExceptions.InvalidApiKey,
                "The presented API key could not be verified.",
                inner));

        problem.Status.ShouldBe(StatusCodes.Status401Unauthorized);

        problem.Extensions.ShouldContainKey("innerType");
        ((JsonElement)problem.Extensions["innerType"]!).GetString()
            .ShouldBe(typeof(InvalidOperationException).FullName);

        var detail = problem.Detail;
        detail.ShouldBe("The presented API key could not be verified.");
        detail!.ShouldNotContain("database connection refused");
        detail!.ShouldNotContain("10.0.0.5");
        detail!.ShouldNotContain("db1/prod-secret");
    }
}
