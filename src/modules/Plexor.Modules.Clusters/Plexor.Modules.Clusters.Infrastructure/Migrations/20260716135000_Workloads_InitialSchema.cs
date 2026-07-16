using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Clusters.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Workloads_InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workloads",
                schema: "forge",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    cluster_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    assigned_node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    local_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    spec = table.Column<string>(type: "jsonb", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    last_message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    last_reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workloads", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workloads_cluster_id_name",
                schema: "forge",
                table: "workloads",
                columns: new[] { "cluster_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workloads_cluster_id_state_name",
                schema: "forge",
                table: "workloads",
                columns: new[] { "cluster_id", "state", "name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workloads",
                schema: "forge");
        }
    }
}
