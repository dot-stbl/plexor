using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Shared.Mtls.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "forge");

            migrationBuilder.CreateTable(
                name: "revoked_certs",
                columns: static table => new
                {
                    serial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                schema: "forge",
                constraints: static table => table.PrimaryKey("PK_revoked_certs", static x => x.serial));

            migrationBuilder.CreateIndex(
                name: "ix_revoked_certs_revoked_at",
                table: "revoked_certs",
                column: "revoked_at",
                schema: "forge");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "revoked_certs",
                schema: "forge");
        }
    }
}
