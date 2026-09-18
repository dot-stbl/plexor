using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Branding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeInstallation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "theme_installations",
                schema: "branding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    theme_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manifest_signature = table.Column<string>(type: "text", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_theme_installations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_branding_theme_installations_org_id",
                schema: "branding",
                table: "theme_installations",
                column: "org_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "theme_installations",
                schema: "branding");
        }
    }
}
