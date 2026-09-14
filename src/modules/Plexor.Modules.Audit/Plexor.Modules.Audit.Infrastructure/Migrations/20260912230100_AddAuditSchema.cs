using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "atlas");

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "atlas",
                constraints: static table => table.PrimaryKey("PK_audit_entries", static x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_atlas_audit_entries_actor_id_occurred_at",
                table: "audit_entries",
                columns: ["actor_id", "occurred_at"],
                schema: "atlas",
                descending: [false, true]);

            migrationBuilder.CreateIndex(
                name: "ix_atlas_audit_entries_org_id_action_occurred_at",
                table: "audit_entries",
                columns: ["org_id", "action", "occurred_at"],
                schema: "atlas",
                descending: [false, false, true]);

            migrationBuilder.CreateIndex(
                name: "ix_atlas_audit_entries_org_id_occurred_at",
                table: "audit_entries",
                columns: ["org_id", "occurred_at"],
                schema: "atlas",
                descending: [false, true]);

            // Append-only invariant — REVOKE UPDATE, DELETE on
            // atlas.audit_entries from PUBLIC. Enforces the contract at
            // the database layer so a future ExecuteUpdate/Remove
            // (or an out-of-band write) is refused even if it slips
            // past code review. EF Core can't express REVOKE
            // declaratively (per ef-migrations.md §"one legitimate
            // exception — custom SQL EF can't express").
            migrationBuilder.Sql(
                "REVOKE UPDATE, DELETE ON TABLE atlas.audit_entries FROM PUBLIC;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-grant before dropping so the DROP doesn't fail when
            // the running role owns the revoked privileges via PUBLIC.
            migrationBuilder.Sql(
                "GRANT UPDATE, DELETE ON TABLE atlas.audit_entries TO PUBLIC;");
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "atlas");
        }
    }
}
