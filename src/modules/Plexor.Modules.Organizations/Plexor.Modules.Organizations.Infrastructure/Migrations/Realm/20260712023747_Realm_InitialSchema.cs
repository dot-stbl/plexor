using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Organizations.Infrastructure.Migrations.Realm
{
    /// <inheritdoc />
    public partial class Realm_InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "realm");

            migrationBuilder.CreateTable(
                name: "folders",
                schema: "realm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_folders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "realm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                schema: "realm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teams", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_realm_folders_org_id",
                schema: "realm",
                table: "folders",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_realm_folders_org_id_team_id_slug",
                schema: "realm",
                table: "folders",
                columns: new[] { "org_id", "team_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_realm_folders_team_id",
                schema: "realm",
                table: "folders",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_realm_organizations_slug",
                schema: "realm",
                table: "organizations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_realm_teams_org_id",
                schema: "realm",
                table: "teams",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_realm_teams_org_id_slug",
                schema: "realm",
                table: "teams",
                columns: new[] { "org_id", "slug" },
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
