using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Clusters.Infrastructure.Migrations.Clusters
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
                name: "clusters",
                schema: "forge",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    wireguard_public_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    join_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    install_providers = table.Column<string>(type: "jsonb", nullable: false),
                    host_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    runtime_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "docker-compose"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uptime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clusters", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "join_tokens",
                schema: "forge",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    cluster_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    intended_role = table.Column<int>(type: "integer", nullable: false),
                    min_iso_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    redeemed_by_node_id = table.Column<string>(type: "varchar(64)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_join_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_join_tokens_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalSchema: "forge",
                        principalTable: "clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                schema: "forge",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    cluster_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
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
                    table.PrimaryKey("PK_nodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_nodes_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalSchema: "forge",
                        principalTable: "clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clusters_org_id_name",
                schema: "forge",
                table: "clusters",
                columns: new[] { "org_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clusters_org_id_status",
                schema: "forge",
                table: "clusters",
                columns: new[] { "org_id", "status" });

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

            migrationBuilder.CreateIndex(
                name: "ix_join_tokens_cluster_id_status",
                schema: "forge",
                table: "join_tokens",
                columns: new[] { "cluster_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_join_tokens_expires_at",
                schema: "forge",
                table: "join_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_nodes_cluster_id_hostname",
                schema: "forge",
                table: "nodes",
                columns: new[] { "cluster_id", "hostname" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nodes_cluster_id_status",
                schema: "forge",
                table: "nodes",
                columns: new[] { "cluster_id", "status" });

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
                name: "commands",
                schema: "forge");

            migrationBuilder.DropTable(
                name: "join_tokens",
                schema: "forge");

            migrationBuilder.DropTable(
                name: "nodes",
                schema: "forge");

            migrationBuilder.DropTable(
                name: "workloads",
                schema: "forge");

            migrationBuilder.DropTable(
                name: "clusters",
                schema: "forge");
        }
    }
}
