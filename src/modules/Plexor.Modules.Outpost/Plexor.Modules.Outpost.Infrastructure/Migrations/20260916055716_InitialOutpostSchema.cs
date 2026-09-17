using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Outpost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialOutpostSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "outpost");

            migrationBuilder.CreateTable(
                name: "node_records",
                schema: "outpost",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    cluster_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostname = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    spec = table.Column<string>(type: "jsonb", nullable: false),
                    iso_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    last_heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    wireguard_public_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    vm_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outpost_node_records_cluster_id_hostname",
                schema: "outpost",
                table: "node_records",
                columns: new[] { "cluster_id", "hostname" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outpost_node_records_cluster_id_status",
                schema: "outpost",
                table: "node_records",
                columns: new[] { "cluster_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_outpost_node_records_last_heartbeat_at",
                schema: "outpost",
                table: "node_records",
                column: "last_heartbeat_at");

            migrationBuilder.CreateIndex(
                name: "ix_outpost_node_records_org_id_status",
                schema: "outpost",
                table: "node_records",
                columns: new[] { "org_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "node_records",
                schema: "outpost");
        }
    }
}
