// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Unit tests for the PLX_* environment variable provider.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Plexor.Shared.Configuration;
using Shouldly;

namespace Plexor.Shared.Configuration.Unit;

/// <summary>
///     Tests for <see cref="PlexorEnvironmentVariablesProvider" /> —
///     the PLX_ env-var mapper.
/// </summary>
public sealed class PlexorEnvironmentVariablesProviderTests
{
    /// <summary>
    ///     Test double — overrides the env-var source so we can
    ///     inject a fixed dictionary. Without this every test
    ///     would have to set real process env vars (race-prone
    ///     in parallel xUnit runs).
    /// </summary>
    private sealed class TestableEnvProvider : PlexorEnvironmentVariablesProvider
    {
        private readonly System.Collections.IDictionary entries;

        public TestableEnvProvider(System.Collections.IDictionary entries)
        {
            this.entries = entries;
        }

        protected override System.Collections.IDictionary ReadEnvironmentVariables()
        {
            return entries;
        }
    }

    private static IConfigurationRoot BuildWithEnv(IDictionary<string, string?> memory)
    {
        var dict = new System.Collections.Hashtable();
        foreach (var kvp in memory)
        {
            dict[kvp.Key] = kvp.Value;
        }

        var provider = new TestableEnvProvider(dict);
        return new ConfigurationBuilder()
            .Add(new PlexorEnvironmentVariablesSource(provider))
            .Build();
    }

    /// <summary>
    ///     We test the provider via a real <see cref="IConfigurationRoot" />:
    ///     build a one-off configuration with only our provider +
    ///     a MemoryConfigurationSource so we control exactly which
    ///     "env vars" the provider sees. No process-env pollution.
    /// </summary>
    [Fact]
    public void Loads_PLX_prefixed_entries_into_IConfiguration_tree()
    {
        var memory = new Dictionary<string, string?>
        {
            ["PLX_DATABASE_HOST"] = "localhost",
            ["PLX_DATABASE_PORT"] = "47100",
            ["PLX_CA_CERT_PATH"] = "/var/lib/plexor/ca.crt",
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/root",
            ["PLX_"] = "ignored",
        };

        var config = BuildWithEnv(memory);

        // IConfiguration stores the env-var-derived key as-is
        // (uppercase preserved). Options binding is
        // case-insensitive, so PascalCase property names match
        // these uppercase keys automatically.
        config["DATABASE:HOST"].ShouldBe("localhost");
        config["DATABASE:PORT"].ShouldBe("47100");
        // Multi-word section names (CertAuthority) need to be
        // collapsed by the operator — PLX_CA_CERT_PATH keeps the
        // first-underscore split. IConfiguration is case-
        // insensitive so the storage key CA:CERT_PATH binds to
        // CertAuthorityOptions.CertPath automatically.
        config["CA:CERT_PATH"].ShouldBe("/var/lib/plexor/ca.crt");

        config["PATH"].ShouldBeNull();
        config["HOME"].ShouldBeNull();
    }

    /// <summary>
    ///     Multi-word section is split at the FIRST underscore;
    ///     the rest stays in the key (single underscore join).
    /// </summary>
    [Fact]
    public void Splits_multi_word_sections_at_first_underscore_only()
    {
        var memory = new Dictionary<string, string?>
        {
            ["PLX_DATABASE_POOL_MAX_SIZE"] = "10",
        };

        var config = BuildWithEnv(memory);

        config["DATABASE:POOL_MAX_SIZE"].ShouldBe("10");
    }

    /// <summary>Section-only value (no underscore) maps to a single key.</summary>
    [Fact]
    public void Section_only_value_maps_to_single_segment_key()
    {
        var memory = new Dictionary<string, string?>
        {
            ["PLX_FOO"] = "bar",
        };

        var config = BuildWithEnv(memory);

        config["FOO"].ShouldBe("bar");
    }

    /// <summary>Empty tail (PLX_) yields nothing — not a "Foo:" key.</summary>
    [Fact]
    public void Empty_tail_does_not_produce_key()
    {
        var memory = new Dictionary<string, string?>
        {
            ["PLX_"] = "ignored",
        };

        var config = BuildWithEnv(memory);

        config.GetChildren().Select(c => c.Key).ShouldNotContain("FOO");
    }

    /// <summary>
    ///     ToConfigKey is the public-mapping contract — tests
    ///     pin it directly. Available as internal because tests
    ///     live in the same assembly via InternalsVisibleTo.
    /// </summary>
    public sealed class ToConfigKeyMapping
    {
        [Fact]
        public void Single_underscore_splits_section_and_key()
        {
            PlexorEnvironmentVariablesProvider.ToConfigKey("DATABASE_HOST")
                .ShouldBe("DATABASE:HOST");
        }

        [Fact]
        public void Multi_word_section_splits_at_first_underscore()
        {
            // Documented behaviour: PLX_SECTION_KEY splits at the
            // first underscore, so multi-word section names must
            // be collapsed by the operator (PLX_CA_CERT_PATH, not
            // PLX_CERT_AUTHORITY_CERT_PATH). This is the trade-off
            // for the single-underscore flat convention.
            PlexorEnvironmentVariablesProvider.ToConfigKey("CA_CERT_PATH")
                .ShouldBe("CA:CERT_PATH");
        }

        [Fact]
        public void No_underscore_returns_just_section()
        {
            PlexorEnvironmentVariablesProvider.ToConfigKey("FOO").ShouldBe("FOO");
        }

        [Fact]
        public void Empty_input_returns_null()
        {
            PlexorEnvironmentVariablesProvider.ToConfigKey(string.Empty).ShouldBeNull();
        }

        [Fact]
        public void Empty_section_part_returns_null()
        {
            PlexorEnvironmentVariablesProvider.ToConfigKey("_HOST").ShouldBeNull();
        }

        [Fact]
        public void Empty_key_part_returns_null()
        {
            PlexorEnvironmentVariablesProvider.ToConfigKey("DATABASE_").ShouldBeNull();
        }
    }
}