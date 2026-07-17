// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DockerComposeRendererShould — snapshot tests for the file-static
// renderer. Tests cover: minimal spec (image only), spec with
// ports, spec with environment, spec with volumes, spec with
// every field populated, null/empty service-name rejection.
//
// Each test asserts the exact YAML string returned by the
// renderer. Snapshot-style (no separate fixture file — the
// expected output is small enough to inline). When the YAML
// format intentionally changes, these tests signal the diff.
// ============================================================================

using System.Collections.Immutable;
using System.Text.Json;
using Plexor.NodeAgent.Providers.Runtime;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Runtime;

public sealed class DockerComposeRendererShould
{
    [Fact(DisplayName = "Given bare image, when Render, then emits minimal compose.yaml")]
    public void RenderMinimalSpec()
    {
        var config = new DockerComposeConfig { Image = "nginx:1.25" };
        var output = DockerComposeRenderer.Render("web", config);

        output.ShouldBe(
            "services:" + "\n" +
            "  web:" + "\n" +
            "    image: nginx:1.25" + "\n");
    }

    [Fact(DisplayName = "Given image + ports, when Render, then emits ports list")]
    public void RenderSpecWithPorts()
    {
        var config = new DockerComposeConfig
        {
            Image = "nginx:1.25",
            Ports = [80, 443],
        };
        var output = DockerComposeRenderer.Render("web", config);

        output.ShouldBe(
            "services:" + "\n" +
            "  web:" + "\n" +
            "    image: nginx:1.25" + "\n" +
            "    ports:" + "\n" +
            "      - \"80:80\"" + "\n" +
            "      - \"443:443\"" + "\n");
    }

    [Fact(DisplayName = "Given image + environment, when Render, then emits environment block")]
    public void RenderSpecWithEnvironment()
    {
        // Route through TryParse so the test exercises the same
        // ImmutableDictionary normalization the production code
        // uses (alphabetic iteration order).
        var json = JsonSerializer.Deserialize<JsonElement>("""
            { "image": "nginx:1.25",
              "environment": {
                  "LOG_LEVEL": "info",
                  "CACHE_TTL": "300"
              } }
            """);
        var config = DockerComposeConfig.TryParse(json, out var error)
            ?? throw new InvalidOperationException(error);
        var output = DockerComposeRenderer.Render("web", config);

        output.ShouldBe(
            "services:" + "\n" +
            "  web:" + "\n" +
            "    image: nginx:1.25" + "\n" +
            "    environment:" + "\n" +
            "      CACHE_TTL: 300" + "\n" +
            "      LOG_LEVEL: info" + "\n");
    }

    [Fact(DisplayName = "Given image + volumes, when Render, then emits volumes list")]
    public void RenderSpecWithVolumes()
    {
        var config = new DockerComposeConfig
        {
            Image = "postgres:15",
            Volumes = ["/var/lib/postgres:/var/lib/postgresql/data"],
        };
        var output = DockerComposeRenderer.Render("db", config);

        output.ShouldBe(
            "services:" + "\n" +
            "  db:" + "\n" +
            "    image: postgres:15" + "\n" +
            "    volumes:" + "\n" +
            "      - /var/lib/postgres:/var/lib/postgresql/data" + "\n");
    }

    [Fact(DisplayName = "Given full spec, when Render, then emits image + ports + environment + volumes")]
    public void RenderFullSpec()
    {
        var config = new DockerComposeConfig
        {
            Image = "ghcr.io/stbl/postgres:15",
            Ports = [5432],
            Environment = new Dictionary<string, string>
            {
                ["POSTGRES_PASSWORD"] = "secret",
            }.ToImmutableDictionary(),
            Volumes = ["/srv/data:/var/lib/postgresql/data"],
        };
        var output = DockerComposeRenderer.Render("db", config);

        output.ShouldBe(
            "services:" + "\n" +
            "  db:" + "\n" +
            "    image: ghcr.io/stbl/postgres:15" + "\n" +
            "    ports:" + "\n" +
            "      - \"5432:5432\"" + "\n" +
            "    environment:" + "\n" +
            "      POSTGRES_PASSWORD: secret" + "\n" +
            "    volumes:" + "\n" +
            "      - /srv/data:/var/lib/postgresql/data" + "\n");
    }


    [Theory(DisplayName = "Given null or whitespace service name, when Render, then throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectEmptyServiceName(string? serviceName)
    {
        var config = new DockerComposeConfig { Image = "nginx:1.25" };

        Should.Throw<ArgumentException>(() =>
            DockerComposeRenderer.Render(serviceName!, config));
    }
}
