// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TenantsDbContext — EF Core context for the Tenants module. Persists
// tenant rows in the 'realm' PostgreSQL schema (schema-per-module
// convention per .agents/STATE.md). The Identity module FKs into
// sigil.tenants.id-equivalent — see Tenants_InitialSchema for the FK
// target.
//
// v0.1 keeps this minimal: only Tenant. Project, Quota, Membership
// entities land when Plexor.Modules.Tenants grows beyond the bare row
// Identity needs as a parent.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Tenants.Domain;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Tenants.Infrastructure.Persistence;

#pragma warning disable CS1591 // XML doc — see DbContext.Set<T>() for usage

public sealed class TenantsDbContext : PlexorDbContext
{
    public TenantsDbContext(DbContextOptions<TenantsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Tenants);
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Tenants);

        builder.HasKey(static tenant => tenant.Id);

        builder.Property(static tenant => tenant.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static tenant => tenant.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static tenant => tenant.Slug)
            .HasColumnName("slug")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static tenant => tenant.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static tenant => tenant.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Slug is globally unique (used in login URL + cross-tenant
        // uniqueness is required for routing).
        builder.HasIndex(static tenant => tenant.Slug)
            .HasDatabaseName("ix_realm_tenants_slug")
            .IsUnique();
    }
}