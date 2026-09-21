using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Providers.VSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitVSphere : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "outpost");

            migrationBuilder.CreateTable(
                name: "vsphere_inventory_snapshots",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vcenter_moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    datacenter_count = table.Column<int>(type: "integer", nullable: false),
                    cluster_count = table.Column<int>(type: "integer", nullable: false),
                    host_count = table.Column<int>(type: "integer", nullable: false),
                    virtual_machine_count = table.Column<int>(type: "integer", nullable: false),
                    refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "outpost",
                constraints: static table => table.PrimaryKey("PK_vsphere_inventory_snapshots", static x => x.id));

            migrationBuilder.CreateTable(
                name: "vsphere_provisioning_runs",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_template_moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    requested_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    target_folder_moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    result_vm_moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "outpost",
                constraints: static table => table.PrimaryKey("PK_vsphere_provisioning_runs", static x => x.id));

            migrationBuilder.CreateTable(
                name: "vsphere_clusters",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    datacenter_moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    drs_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                schema: "outpost",
                constraints: static table =>
                {
                    table.PrimaryKey("PK_vsphere_clusters", static x => x.id);
                    table.ForeignKey(
                        name: "FK_vsphere_clusters_vsphere_inventory_snapshots_snapshot_id",
                        column: static x => x.snapshot_id,
                        principalTable: "vsphere_inventory_snapshots",
                        principalColumn: "id",
                        principalSchema: "outpost",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vsphere_hosts",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    cluster_moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    connection_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cpu_cores = table.Column<int>(type: "integer", nullable: false),
                    memory_mib = table.Column<long>(type: "bigint", nullable: false)
                },
                schema: "outpost",
                constraints: static table =>
                {
                    table.PrimaryKey("PK_vsphere_hosts", static x => x.id);
                    table.ForeignKey(
                        name: "FK_vsphere_hosts_vsphere_inventory_snapshots_snapshot_id",
                        column: static x => x.snapshot_id,
                        principalTable: "vsphere_inventory_snapshots",
                        principalColumn: "id",
                        principalSchema: "outpost",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vsphere_virtual_machines",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    folder_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    power_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cpu_count = table.Column<int>(type: "integer", nullable: false),
                    memory_mib = table.Column<long>(type: "bigint", nullable: false),
                    host_moref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                schema: "outpost",
                constraints: static table =>
                {
                    table.PrimaryKey("PK_vsphere_virtual_machines", static x => x.id);
                    table.ForeignKey(
                        name: "FK_vsphere_virtual_machines_vsphere_inventory_snapshots_snapsh~",
                        column: static x => x.snapshot_id,
                        principalTable: "vsphere_inventory_snapshots",
                        principalColumn: "id",
                        principalSchema: "outpost",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outpost_vsphere_clusters_snapshot_id_datacenter_moref",
                table: "vsphere_clusters",
                columns: ["snapshot_id", "datacenter_moref"],
                schema: "outpost");

            migrationBuilder.CreateIndex(
                name: "ux_outpost_vsphere_clusters_snapshot_id_moref",
                table: "vsphere_clusters",
                columns: ["snapshot_id", "moref"],
                schema: "outpost",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outpost_vsphere_hosts_snapshot_id_cluster_moref",
                table: "vsphere_hosts",
                columns: ["snapshot_id", "cluster_moref"],
                schema: "outpost");

            migrationBuilder.CreateIndex(
                name: "ux_outpost_vsphere_hosts_snapshot_id_moref",
                table: "vsphere_hosts",
                columns: ["snapshot_id", "moref"],
                schema: "outpost",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outpost_vsphere_inventory_snapshots_refreshed_at",
                table: "vsphere_inventory_snapshots",
                column: "refreshed_at",
                schema: "outpost",
                descending: []);

            migrationBuilder.CreateIndex(
                name: "ix_outpost_vsphere_provisioning_runs_source_template_moref",
                table: "vsphere_provisioning_runs",
                column: "source_template_moref",
                schema: "outpost");

            migrationBuilder.CreateIndex(
                name: "ix_outpost_vsphere_provisioning_runs_started_at",
                table: "vsphere_provisioning_runs",
                column: "started_at",
                schema: "outpost",
                descending: []);

            migrationBuilder.CreateIndex(
                name: "ix_outpost_vsphere_virtual_machines_snapshot_id_folder_path",
                table: "vsphere_virtual_machines",
                columns: ["snapshot_id", "folder_path"],
                schema: "outpost");

            migrationBuilder.CreateIndex(
                name: "ux_outpost_vsphere_virtual_machines_snapshot_id_moref",
                table: "vsphere_virtual_machines",
                columns: ["snapshot_id", "moref"],
                schema: "outpost",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vsphere_clusters",
                schema: "outpost");

            migrationBuilder.DropTable(
                name: "vsphere_hosts",
                schema: "outpost");

            migrationBuilder.DropTable(
                name: "vsphere_provisioning_runs",
                schema: "outpost");

            migrationBuilder.DropTable(
                name: "vsphere_virtual_machines",
                schema: "outpost");

            migrationBuilder.DropTable(
                name: "vsphere_inventory_snapshots",
                schema: "outpost");
        }
    }
}
