using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Network.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitNetwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "network");

            migrationBuilder.CreateTable(
                name: "floating_ips",
                schema: "network",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_floating_ips", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "load_balancers",
                schema: "network",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    algorithm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_load_balancers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_network_floating_ips_cluster_id",
                schema: "network",
                table: "floating_ips",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_network_floating_ips_org_id",
                schema: "network",
                table: "floating_ips",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_network_floating_ips_status",
                schema: "network",
                table: "floating_ips",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_network_load_balancers_cluster_id_name",
                schema: "network",
                table: "load_balancers",
                columns: new[] { "cluster_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_network_load_balancers_org_id",
                schema: "network",
                table: "load_balancers",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_network_load_balancers_status",
                schema: "network",
                table: "load_balancers",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "floating_ips",
                schema: "network");

            migrationBuilder.DropTable(
                name: "load_balancers",
                schema: "network");
        }
    }
}
