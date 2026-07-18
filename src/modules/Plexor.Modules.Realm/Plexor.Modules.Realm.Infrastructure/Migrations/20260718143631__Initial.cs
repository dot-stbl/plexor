using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Realm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "realm");

            migrationBuilder.CreateTable(
                name: "folders",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "realm",
                constraints: static table => table.PrimaryKey("PK_folders", static x => x.id));

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "realm",
                constraints: static table => table.PrimaryKey("PK_organizations", static x => x.id));

            migrationBuilder.CreateTable(
                name: "teams",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "realm",
                constraints: static table => table.PrimaryKey("PK_teams", static x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_realm_folders_org_id",
                table: "folders",
                column: "org_id",
                schema: "realm");

            migrationBuilder.CreateIndex(
                name: "ix_realm_folders_org_id_team_id_slug",
                table: "folders",
                columns: ["org_id", "team_id", "slug"],
                schema: "realm",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_realm_folders_team_id",
                table: "folders",
                column: "team_id",
                schema: "realm");

            migrationBuilder.CreateIndex(
                name: "ix_realm_organizations_slug",
                table: "organizations",
                column: "slug",
                schema: "realm",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_realm_teams_org_id",
                table: "teams",
                column: "org_id",
                schema: "realm");

            migrationBuilder.CreateIndex(
                name: "ix_realm_teams_org_id_slug",
                table: "teams",
                columns: ["org_id", "slug"],
                schema: "realm",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "folders",
                schema: "realm");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "realm");

            migrationBuilder.DropTable(
                name: "teams",
                schema: "realm");
        }
    }
}
