using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Branding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "branding");

            migrationBuilder.CreateTable(
                name: "global_theme_config",
                schema: "branding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    brand_logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    brand_favicon_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    default_preset_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    custom_accent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_global_theme_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_theme_config",
                schema: "branding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preset_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    custom_accent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    brand_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    brand_logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    brand_favicon_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_theme_config", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_branding_global_theme_config_singleton",
                schema: "branding",
                table: "global_theme_config",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_branding_org_theme_config_org_id",
                schema: "branding",
                table: "org_theme_config",
                column: "org_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "global_theme_config",
                schema: "branding");

            migrationBuilder.DropTable(
                name: "org_theme_config",
                schema: "branding");
        }
    }
}
