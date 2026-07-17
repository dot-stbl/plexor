using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Clusters.Infrastructure.Migrations.Clusters
{
    /// <inheritdoc />
    public partial class ClusterRuntimeId_Add : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "runtime_id",
                schema: "forge",
                table: "clusters",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "docker-compose");

            migrationBuilder.CreateTable(
                name: "commands",
                schema: "forge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    command_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commands", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_commands_node_id_status_created_at",
                schema: "forge",
                table: "commands",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "uk_commands_command_id",
                schema: "forge",
                table: "commands",
                column: "command_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commands",
                schema: "forge");

            migrationBuilder.DropColumn(
                name: "runtime_id",
                schema: "forge",
                table: "clusters");
        }
    }
}
