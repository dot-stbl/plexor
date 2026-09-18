using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Clusters.Infrastructure.Migrations.Clusters
{
    /// <inheritdoc />
    public partial class AddVmLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lifecycle_state",
                schema: "forge",
                table: "workloads",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "provider_vm_id",
                schema: "forge",
                table: "workloads",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workload_lifecycle_events",
                schema: "forge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workload_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    from_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    to_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_vm_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workload_lifecycle_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_workload_lifecycle_events_workloads_workload_id",
                        column: x => x.workload_id,
                        principalSchema: "forge",
                        principalTable: "workloads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workload_lifecycle_events_workload_id_id",
                schema: "forge",
                table: "workload_lifecycle_events",
                columns: new[] { "workload_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workload_lifecycle_events",
                schema: "forge");

            migrationBuilder.DropColumn(
                name: "lifecycle_state",
                schema: "forge",
                table: "workloads");

            migrationBuilder.DropColumn(
                name: "provider_vm_id",
                schema: "forge",
                table: "workloads");
        }
    }
}
