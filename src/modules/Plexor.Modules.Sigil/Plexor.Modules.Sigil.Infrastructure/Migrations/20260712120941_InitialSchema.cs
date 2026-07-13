using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sigil");

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    secret_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "sigil",
                constraints: static table => table.PrimaryKey("PK_api_keys", static x => x.id));

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "sigil",
                constraints: static table => table.PrimaryKey("PK_refresh_tokens", static x => x.id));

            migrationBuilder.CreateTable(
                name: "role_bindings",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "sigil",
                constraints: static table => table.PrimaryKey("PK_role_bindings", static x => x.id));

            migrationBuilder.CreateTable(
                name: "roles",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    built_in = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "sigil",
                constraints: static table => table.PrimaryKey("PK_roles", static x => x.id));

            migrationBuilder.CreateTable(
                name: "signing_keys",
                columns: static table => new
                {
                    kid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    algorithm = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    public_key_pem = table.Column<string>(type: "text", nullable: false),
                    private_key_pem = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    not_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                schema: "sigil",
                constraints: static table => table.PrimaryKey("PK_signing_keys", static x => x.kid));

            migrationBuilder.CreateTable(
                name: "ssh_keys",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    public_key = table.Column<string>(type: "text", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "sigil",
                constraints: static table => table.PrimaryKey("PK_ssh_keys", static x => x.id));

            migrationBuilder.CreateTable(
                name: "users",
                columns: static table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                schema: "sigil",
                constraints: static table => table.PrimaryKey("PK_users", static x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_sigil_api_keys_org_id_revoked_at",
                table: "api_keys",
                columns: ["org_id", "revoked_at"],
                schema: "sigil");

            migrationBuilder.CreateIndex(
                name: "ix_sigil_api_keys_user_id",
                table: "api_keys",
                column: "user_id",
                schema: "sigil");

            migrationBuilder.CreateIndex(
                name: "ix_sigil_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at",
                schema: "sigil");

            migrationBuilder.CreateIndex(
                name: "ix_sigil_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id",
                schema: "sigil");

            migrationBuilder.CreateIndex(
                name: "ix_sigil_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id",
                schema: "sigil");

            migrationBuilder.CreateIndex(
                name: "ix_sigil_role_bindings_role_id",
                table: "role_bindings",
                column: "role_id",
                schema: "sigil");

            migrationBuilder.CreateIndex(
                name: "ix_sigil_role_bindings_user_id",
                table: "role_bindings",
                column: "user_id",
                schema: "sigil");

            migrationBuilder.CreateIndex(
                name: "ix_sigil_role_bindings_user_id_role_id_team_id_folder_id",
                table: "role_bindings",
                columns: ["user_id", "role_id", "team_id", "folder_id"],
                schema: "sigil",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sigil_roles_org_id_name",
                table: "roles",
                columns: ["org_id", "name"],
                schema: "sigil",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sigil_ssh_keys_org_id_fingerprint",
                table: "ssh_keys",
                columns: ["org_id", "fingerprint"],
                schema: "sigil",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sigil_users_org_id_email",
                table: "users",
                columns: ["org_id", "email"],
                schema: "sigil",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sigil_users_org_id_status",
                table: "users",
                columns: ["org_id", "status"],
                schema: "sigil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys",
                schema: "sigil");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "sigil");

            migrationBuilder.DropTable(
                name: "role_bindings",
                schema: "sigil");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "sigil");

            migrationBuilder.DropTable(
                name: "signing_keys",
                schema: "sigil");

            migrationBuilder.DropTable(
                name: "ssh_keys",
                schema: "sigil");

            migrationBuilder.DropTable(
                name: "users",
                schema: "sigil");
        }
    }
}
