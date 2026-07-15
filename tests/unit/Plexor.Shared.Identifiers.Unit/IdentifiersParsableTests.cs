// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentifiersParsableTests — verifies that ASP.NET Core's model binder
// can map a wire-format ID string to the strongly-typed value object.
//
// .NET 7+ ASP.NET Core looks for <c>static T Parse(string, IFormatProvider?)</c>
// on the target type via the IParsable<T> interface contract. This
// test simulates what the binder does at runtime and asserts the
// happy path + every rejection path is handled without a crash.
// ============================================================================

using Shouldly;
using Xunit;

namespace Plexor.Shared.Identifiers.Unit;

public sealed class IdentifiersParsableTests
{
    [Theory(DisplayName = "Given valid wire-format id, when Parse, then succeeds")]
    [InlineData("cluster_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d0")]
    [InlineData("node_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d0")]
    [InlineData("tok_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d0")]
    [InlineData("wl_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d0")]
    public void Parse_ValidWireFormat_ReturnsId(string wire)
    {
        var prefix = wire[..wire.IndexOf('_')];
        switch (prefix)
        {
            case "cluster":
                var cluster = ClusterId.Parse(wire, null);
                cluster.ToString().ShouldBe(wire);
                break;
            case "node":
                var node = NodeId.Parse(wire, null);
                node.ToString().ShouldBe(wire);
                break;
            case "tok":
                var token = TokenId.Parse(wire, null);
                token.ToString().ShouldBe(wire);
                break;
            case "wl":
                var workload = WorkloadId.Parse(wire, null);
                workload.ToString().ShouldBe(wire);
                break;
            default:
                throw new InvalidOperationException($"Unknown prefix in test: {prefix}");
        }
    }

    [Theory(DisplayName = "Given malformed wire-format id, when Parse, then throws FormatException")]
    [InlineData("cluster_garbage")]        // body not hex
    [InlineData("cluster_0190f4d6c8e7b2a9")]  // body too short
    [InlineData("node_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d0_extra")]  // body too long
    [InlineData("garbage")]                // wrong prefix entirely
    [InlineData("")]                       // empty
    public void Parse_MalformedInput_Throws(string wire)
    {
        Should.Throw<FormatException>(() => ClusterId.Parse(wire, null));
        Should.Throw<FormatException>(() => NodeId.Parse(wire, null));
        Should.Throw<FormatException>(() => TokenId.Parse(wire, null));
        Should.Throw<FormatException>(() => WorkloadId.Parse(wire, null));
    }

    [Theory(DisplayName = "Given null, when TryParse, then returns false without throwing")]
    [InlineData(null)]
    public void TryParse_Null_ReturnsFalse(string? wire)
    {
        ClusterId.TryParse(wire, null, out _).ShouldBeFalse();
        NodeId.TryParse(wire, null, out _).ShouldBeFalse();
        TokenId.TryParse(wire, null, out _).ShouldBeFalse();
        WorkloadId.TryParse(wire, null, out _).ShouldBeFalse();
    }

    [Theory(DisplayName = "Given malformed wire-format id, when TryParse, then returns false without throwing")]
    [InlineData("cluster_garbage")]
    [InlineData("garbage")]
    public void TryParse_MalformedInput_ReturnsFalse(string wire)
    {
        ClusterId.TryParse(wire, null, out _).ShouldBeFalse();
        NodeId.TryParse(wire, null, out _).ShouldBeFalse();
        TokenId.TryParse(wire, null, out _).ShouldBeFalse();
        WorkloadId.TryParse(wire, null, out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "Given valid wire-format id, when TryParse, then returns true + parsed value")]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        const string wire = "cluster_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d0";

        var ok = ClusterId.TryParse(wire, null, out var cluster);

        ok.ShouldBeTrue();
        cluster.ToString().ShouldBe(wire);
    }

    [Fact(DisplayName = "Given JoinTokenSecret.New, when ToString, then returns url-safe base64 with no padding")]
    public void JoinTokenSecret_New_FormatsAsUrlSafeBase64()
    {
        var secret = JoinTokenSecret.New();

        var formatted = secret.ToString();

        // URL-safe base64 has no '+', '/' or '=' chars.
        formatted.ShouldNotContain("+");
        formatted.ShouldNotContain("/");
        formatted.ShouldNotContain("=");
        // 32 bytes → 44 chars in base64 with padding; we strip the
        // trailing '=' twice (44 chars → 42-43 chars depending on byte
        // alignment, but URL-safe alphabet always yields integer length).
        formatted.Length.ShouldBeGreaterThanOrEqualTo(42);
        formatted.Length.ShouldBeLessThanOrEqualTo(44);
    }
}