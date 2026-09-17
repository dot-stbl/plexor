using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Storage.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "storage");

            migrationBuilder.CreateTable(
                name: "buckets",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    region = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    object_count = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "storage",
                constraints: static table => table.PrimaryKey("PK_buckets", static x => x.id));

            migrationBuilder.CreateTable(
                name: "volumes",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    size_gb = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "storage",
                constraints: static table => table.PrimaryKey("PK_volumes", static x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_storage_buckets_name_region",
                table: "buckets",
                columns: ["name", "region"],
                schema: "storage",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_storage_buckets_org_id",
                table: "buckets",
                column: "org_id",
                schema: "storage");

            migrationBuilder.CreateIndex(
                name: "ix_storage_volumes_cluster_id_name",
                table: "volumes",
                columns: ["cluster_id", "name"],
                schema: "storage",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_storage_volumes_org_id",
                table: "volumes",
                column: "org_id",
                schema: "storage");

            migrationBuilder.CreateIndex(
                name: "ix_storage_volumes_status",
                table: "volumes",
                column: "status",
                schema: "storage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buckets",
                schema: "storage");

            migrationBuilder.DropTable(
                name: "volumes",
                schema: "storage");
        }
    }
}
