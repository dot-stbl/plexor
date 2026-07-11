using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Audit.Infrastructure.Migrations.Audit
{
    /// <inheritdoc />
    public partial class Audit_InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "atlas");

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "atlas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_atlas_audit_entries_tenant_id_occurred_at",
                schema: "atlas",
                table: "audit_entries",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_atlas_audit_entries_tenant_id_resource_type",
                schema: "atlas",
                table: "audit_entries",
                columns: new[] { "tenant_id", "resource_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "atlas");
        }
    }
}
