using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Quotas.Infrastructure.Migrations.QuotasDbContext;

/// <inheritdoc />
public partial class InitQuotas : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "quotas");

        migrationBuilder.CreateTable(
            name: "quota_definitions",
            columns: static table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                default_value = table.Column<decimal>(type: "numeric(38,18)", nullable: true),
                builtin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                effective_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            schema: "quotas",
            constraints: static table => table.PrimaryKey("PK_quota_definitions", static x => x.id));

        migrationBuilder.CreateTable(
            name: "rate_limit_events",
            columns: static table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                principal_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                org_id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            schema: "quotas",
            constraints: static table => table.PrimaryKey("PK_rate_limit_events", static x => x.id));

        migrationBuilder.CreateTable(
            name: "quota_assignments",
            columns: static table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                scope_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                org_id = table.Column<Guid>(type: "uuid", nullable: false),
                value = table.Column<decimal>(type: "numeric(38,18)", nullable: false),
                period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            schema: "quotas",
            constraints: static table =>
            {
                table.PrimaryKey("PK_quota_assignments", static x => x.id);
                table.ForeignKey(
                    name: "FK_quota_assignments_quota_definitions_definition_id",
                    column: static x => x.definition_id,
                    principalTable: "quota_definitions",
                    principalColumn: "id",
                    principalSchema: "quotas",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "quota_usage",
            columns: static table => new
            {
                scope_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                org_id = table.Column<Guid>(type: "uuid", nullable: false),
                current_value = table.Column<decimal>(type: "numeric(38,18)", nullable: false, defaultValue: 0m),
                period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_reconciled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            schema: "quotas",
            constraints: static table =>
            {
                table.PrimaryKey("PK_quota_usage", static x => new { x.scope_kind, x.scope_id, x.definition_id });
                table.ForeignKey(
                    name: "FK_quota_usage_quota_definitions_definition_id",
                    column: static x => x.definition_id,
                    principalTable: "quota_definitions",
                    principalColumn: "id",
                    principalSchema: "quotas",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_quotas_quota_assignments_definition_scope_period",
            table: "quota_assignments",
            columns: ["definition_id", "scope_kind", "scope_id", "period"],
            schema: "quotas",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_quotas_quota_assignments_org_id",
            table: "quota_assignments",
            column: "org_id",
            schema: "quotas");

        migrationBuilder.CreateIndex(
            name: "ix_quotas_quota_definitions_key",
            table: "quota_definitions",
            column: "key",
            schema: "quotas",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_quota_usage_definition_id",
            table: "quota_usage",
            column: "definition_id",
            schema: "quotas");

        migrationBuilder.CreateIndex(
            name: "ix_quotas_quota_usage_org_id",
            table: "quota_usage",
            column: "org_id",
            schema: "quotas");

        migrationBuilder.CreateIndex(
            name: "ix_quotas_rate_limit_events_occurred_at",
            table: "rate_limit_events",
            column: "occurred_at",
            schema: "quotas");

        migrationBuilder.CreateIndex(
            name: "ix_quotas_rate_limit_events_org_id_occurred_at",
            table: "rate_limit_events",
            columns: ["org_id", "occurred_at"],
            schema: "quotas");

        migrationBuilder.CreateIndex(
            name: "ix_quotas_rate_limit_events_principal_id_occurred_at",
            table: "rate_limit_events",
            columns: ["principal_id", "occurred_at"],
            schema: "quotas");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "quota_assignments",
            schema: "quotas");

        migrationBuilder.DropTable(
            name: "quota_usage",
            schema: "quotas");

        migrationBuilder.DropTable(
            name: "rate_limit_events",
            schema: "quotas");

        migrationBuilder.DropTable(
            name: "quota_definitions",
            schema: "quotas");
    }
}
