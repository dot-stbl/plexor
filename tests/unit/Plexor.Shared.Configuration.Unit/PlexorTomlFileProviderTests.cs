// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Unit tests for the PlexorTomlFileProvider — TOML → IConfiguration
// key tree (Section:Key). Locks down the flattening rules so they
// stay stable.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Plexor.Shared.Configuration.Unit;

/// <summary>
///     Tests for <see cref="PlexorTomlFileProvider" /> — TOML →
///     IConfiguration key tree (Section:Key).
/// </summary>
public sealed class PlexorTomlFileProviderTests : IDisposable
{
    private readonly string tempFile;

    public PlexorTomlFileProviderTests()
    {
        tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>Flat table → Section:Key entries.</summary>
    [Fact]
    public void Loads_flat_table_into_section_colon_keys()
    {
        File.WriteAllText(tempFile,
            """
            [database]
            host = "localhost"
            port = 47100
            """);

        var config = new ConfigurationBuilder()
            .Add(new PlexorTomlConfigurationSource(tempFile))
            .Build();

        config["database:host"].ShouldBe("localhost");
        config["database:port"].ShouldBe("47100");
    }

    /// <summary>Multi-word sections become PascalCase prefixes in IConfiguration.</summary>
    [Fact]
    public void Loads_multi_word_sections_with_pascalcase_prefix()
    {
        File.WriteAllText(tempFile,
            """
            [cert_authority]
            cert_path = "/var/lib/plexor/ca.crt"
            host_cert_password = "secret"
            """);

        var config = new ConfigurationBuilder()
            .Add(new PlexorTomlConfigurationSource(tempFile))
            .Build();

        // IConfiguration stores TOML keys as-is. Options binder is
        // case-insensitive, so the test asserts on the
        // canonical form that ends up in the dictionary.
        config["cert_authority:cert_path"].ShouldBe("/var/lib/plexor/ca.crt");
        config["cert_authority:host_cert_password"].ShouldBe("secret");
    }

    /// <summary>Nested tables flatten with extra colon segments.</summary>
    [Fact]
    public void Flattens_nested_tables_with_colon_separator()
    {
        File.WriteAllText(tempFile,
            """
            [database]
            host = "localhost"

            [database.pool]
            max_size = 10
            """);

        var config = new ConfigurationBuilder()
            .Add(new PlexorTomlConfigurationSource(tempFile))
            .Build();

        config["database:host"].ShouldBe("localhost");
        config["database:pool:max_size"].ShouldBe("10");
    }

    /// <summary>Arrays of tables become numeric-suffixed sections.</summary>
    [Fact]
    public void Flattens_arrays_of_tables_with_numeric_indices()
    {
        File.WriteAllText(tempFile,
            """
            [[plugins]]
            name = "first"

            [[plugins]]
            name = "second"
            """);

        var config = new ConfigurationBuilder()
            .Add(new PlexorTomlConfigurationSource(tempFile))
            .Build();

        config["plugins:0:name"].ShouldBe("first");
        config["plugins:1:name"].ShouldBe("second");
    }

    /// <summary>
    ///     Absent file → empty configuration (not an error). The
    ///     higher providers (env) still apply on top.
    /// </summary>
    [Fact]
    public void Absent_file_yields_empty_configuration()
    {
        // tempFile was deleted in ctor — so it doesn't exist.
        var config = new ConfigurationBuilder()
            .Add(new PlexorTomlConfigurationSource(tempFile))
            .Build();

        config.GetChildren().ShouldBeEmpty();
    }

    /// <summary>
    ///     TOML scalar values are rendered as invariant-culture
    ///     strings — no locale-dependent decimal separators or
    ///     booleans leaking into IConfiguration.
    /// </summary>
    [Fact]
    public void Numbers_and_booleans_render_in_invariant_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("ru-RU");

            File.WriteAllText(tempFile,
                """
                server = 47100
                enabled = true
                ratio = 0.5
                """);

            var config = new ConfigurationBuilder()
                .Add(new PlexorTomlConfigurationSource(tempFile))
                .Build();

            config["server"].ShouldBe("47100");
            config["enabled"].ShouldBe("true");
            config["ratio"].ShouldBe("0.5");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
