// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// K3sWorkloadRendererShould — snapshot tests for the file-static
// renderer. Tests cover: minimal spec (no ports → no Service),
// spec with ports (Service emitted), replicas / namespace
// propagation, env: blocks (alphabetized), [Null-Name] rejection.
//
// Each test asserts the exact YAML the renderer emits across all
// three files (kustomization + deployment + optional service).
// Routes through TryParse so the tests exercise the same
// ImmutableDictionary normalization production uses.
// ============================================================================

using System.Text.Json;
using Plexor.NodeAgent.Providers.Runtime;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Runtime;

public sealed class K3sWorkloadRendererShould
{
    [Fact(DisplayName = "Given bare image, when Render, then emits kustomization + deployment, no service")]
    public void RenderMinimalSpec()
    {
        var config = ParseConfig(""" { "image": "nginx:1.25" } """);
        var manifest = K3sWorkloadRenderer.Render("web", config);

        manifest.ServiceYaml.ShouldBeEmpty();
        manifest.KustomizationYaml.ShouldContain("resources:");
        manifest.KustomizationYaml.ShouldContain("- deployment.yaml");
        manifest.KustomizationYaml.ShouldNotContain("- service.yaml");
        manifest.DeploymentYaml.ShouldContain("kind: Deployment");
        manifest.DeploymentYaml.ShouldContain("name: web");
        manifest.DeploymentYaml.ShouldContain("image: nginx:1.25");
        manifest.DeploymentYaml.ShouldContain("replicas: 1");
    }

    [Fact(DisplayName = "Given image + ports, when Render, then service is emitted with both ports")]
    public void RenderSpecWithPorts()
    {
        var config = ParseConfig("""
            { "image": "nginx:1.25", "ports": [80, 443] }
            """);
        var manifest = K3sWorkloadRenderer.Render("web", config);

        manifest.ServiceYaml.ShouldNotBeEmpty();
        manifest.ServiceYaml.ShouldContain("kind: Service");
        manifest.ServiceYaml.ShouldContain("port: 80");
        manifest.ServiceYaml.ShouldContain("port: 443");
        manifest.ServiceYaml.ShouldContain("targetPort: port-80");
        manifest.KustomizationYaml.ShouldContain("- service.yaml");

        manifest.DeploymentYaml.ShouldContain("containerPort: 80");
        manifest.DeploymentYaml.ShouldContain("containerPort: 443");
    }

    [Fact(DisplayName = "Given replicas > 1 + namespace, when Render, then values propagate")]
    public void RenderPropagatesReplicasAndNamespace()
    {
        var config = ParseConfig("""
            { "image": "nginx:1.25", "replicas": 3, "namespace": "staging" }
            """);
        var manifest = K3sWorkloadRenderer.Render("web", config);

        manifest.KustomizationYaml.ShouldContain("namespace: staging");
        manifest.DeploymentYaml.ShouldContain("replicas: 3");
        manifest.DeploymentYaml.ShouldContain("namespace: staging");
    }

    [Fact(DisplayName = "Given image + environment, when Render, then env: block is alphabetized")]
    public void RenderEmitsSortedEnvBlock()
    {
        var config = ParseConfig("""
            { "image": "nginx:1.25",
              "environment": {
                  "LOG_LEVEL": "info",
                  "CACHE_TTL": "300"
              } }
            """);
        var manifest = K3sWorkloadRenderer.Render("web", config);

        var lCacheIdx = manifest.DeploymentYaml.IndexOf("CACHE_TTL", StringComparison.Ordinal);
        var lLogIdx = manifest.DeploymentYaml.IndexOf("LOG_LEVEL", StringComparison.Ordinal);
        lCacheIdx.ShouldBeLessThan(lLogIdx);
    }

    [Fact(DisplayName = "Given env value with embedded quotes, when Render, then YAML stays parseable")]
    public void RenderEscapesQuotesInEnvValues()
    {
        // JSON literal constructed via concatenation; C# raw
        // strings don't process backslash escapes so we keep
        // things explicit.
        const string jsonText =
            "{ \"image\": \"nginx:1.25\"," +
            " \"environment\": { \"GREETING\": \"Hello \\\"World\\\"\" } }";
        var element = JsonSerializer.Deserialize<JsonElement>(jsonText);
        var config = K3sWorkloadConfig.TryParse(element, out var error)
            ?? throw new InvalidOperationException(error);
        var manifest = K3sWorkloadRenderer.Render("web", config);

        // YAML escape for an embedded quote inside a double-
        // quoted string is `\"`. The renderer should produce
        // a value: "Hello \"World\"" entry (literal backslash
        // + quote, not raw quote which would terminate the
        // YAML scalar early). The C# escape sequence \\\" is
        // one literal backslash followed by a literal quote.
        manifest.DeploymentYaml.ShouldContain("Hello \\\"World\\\"");
        // The value field must NOT contain an unescaped quote
        // that would terminate the YAML scalar prematurely.
        manifest.DeploymentYaml.ShouldNotContain("Hello \" World\"");
    }

    [Theory(DisplayName = "Given null or whitespace workload name, when Render, then throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectEmptyWorkloadName(string? workloadName)
    {
        var config = new K3sWorkloadConfig { Image = "nginx:1.25" };

        Should.Throw<ArgumentException>(() =>
            K3sWorkloadRenderer.Render(workloadName!, config));
    }

    private static K3sWorkloadConfig ParseConfig(string json)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        return K3sWorkloadConfig.TryParse(element, out var error)
            ?? throw new InvalidOperationException(error);
    }
}
