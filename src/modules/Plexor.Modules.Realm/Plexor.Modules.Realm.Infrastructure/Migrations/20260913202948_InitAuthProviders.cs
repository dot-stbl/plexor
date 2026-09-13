using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Realm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitAuthProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "org_auth_provider_configs",
                schema: "realm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    oidc_authority = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    oidc_client_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    oidc_client_secret_protected = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    oidc_scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_auth_provider_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_realm_org_auth_provider_configs_org_id",
                schema: "realm",
                table: "org_auth_provider_configs",
                column: "org_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_auth_provider_configs",
                schema: "realm");
        }
    }
}
