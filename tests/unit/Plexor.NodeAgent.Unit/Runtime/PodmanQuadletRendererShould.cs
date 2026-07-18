// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PodmanQuadletRendererShould — snapshot tests for the file-static
// renderer. Tests cover: minimal spec, ports, environment
// (alphabetized), restart policies, autoStart=false omits the
// [Install] section, null/empty service-name rejection.
//
// Each test asserts the exact INI string returned. When the
// quadlet format intentionally changes, these tests signal the
// diff. Routes through TryParse so the tests exercise the same
// ImmutableDictionary normalization production uses.
// ============================================================================

using System.Text.Json;
using Plexor.NodeAgent.Providers.Runtime;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Runtime;

public sealed class PodmanQuadletRendererShould
{
    [Fact(DisplayName = "Given bare image, when Render, then emits minimal quadlet with all 4 sections")]
    public void RenderMinimalSpec()
    {
        var config = ParseConfig(/*lang=json,strict*/ """ { "image": "nginx:1.25" } """);
        var output = PodmanQuadletRenderer.Render("web", config);

        output.ShouldBe(
            "[Unit]\n" +
            "Description=Plexor workload web\n" +
            "\n" +
            "[Container]\n" +
            "Image=nginx:1.25\n" +
            "\n" +
            "[Service]\n" +
            "Restart=always\n" +
            "\n" +
            "[Install]\n" +
            "WantedBy=multi-user.target\n");
    }

    [Fact(DisplayName = "Given image + ports, when Render, then emits PublishPort lines")]
    public void RenderSpecWithPorts()
    {
        var config = ParseConfig(/*lang=json,strict*/ """
            { "image": "nginx:1.25", "ports": [80, 443] }
            """);
        var output = PodmanQuadletRenderer.Render("web", config);

        output.ShouldContain("PublishPort=80:80\n");
        output.ShouldContain("PublishPort=443:443\n");
    }

    [Fact(DisplayName = "Given image + environment, when Render, then emits Environment lines (alphabetized)")]
    public void RenderSpecWithEnvironment()
    {
        var config = ParseConfig(/*lang=json,strict*/ """
            { "image": "nginx:1.25",
              "environment": {
                  "LOG_LEVEL": "info",
                  "CACHE_TTL": "300"
              } }
            """);
        var output = PodmanQuadletRenderer.Render("web", config);

        output.ShouldContain("Environment=CACHE_TTL=300\n");
        output.ShouldContain("Environment=LOG_LEVEL=info\n");
    }

    [Fact(DisplayName = "Given restart policy, when Render, then emits Restart= in [Service]")]
    public void RenderSpecWithRestart()
    {
        var config = ParseConfig(/*lang=json,strict*/ """
            { "image": "nginx:1.25", "restart": "on-failure" }
            """);
        var output = PodmanQuadletRenderer.Render("web", config);

        output.ShouldContain("[Service]\nRestart=on-failure\n");
    }

    [Fact(DisplayName = "Given autoStart=false, when Render, then omits [Install] section")]
    public void RenderOmitsInstallWhenAutoStartFalse()
    {
        var config = ParseConfig(/*lang=json,strict*/ """
            { "image": "nginx:1.25", "autoStart": false }
            """);
        var output = PodmanQuadletRenderer.Render("web", config);

        output.ShouldNotContain("[Install]");
        output.ShouldNotContain("WantedBy=multi-user.target");
    }

    [Theory(DisplayName = "Given null or whitespace service name, when Render, then throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectEmptyServiceName(string? serviceName)
    {
        var config = new PodmanQuadletConfig { Image = "nginx:1.25" };

        Should.Throw<ArgumentException>(() =>
            PodmanQuadletRenderer.Render(serviceName!, config));
    }

    private static PodmanQuadletConfig ParseConfig(string json)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        return PodmanQuadletConfig.TryParse(element, out var error)
            ?? throw new InvalidOperationException(error);
    }
}
