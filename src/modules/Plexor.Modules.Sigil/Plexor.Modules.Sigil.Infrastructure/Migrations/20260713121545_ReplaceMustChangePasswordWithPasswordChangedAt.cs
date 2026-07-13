using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plexor.Modules.Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceMustChangePasswordWithPasswordChangedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "must_change_password",
                schema: "sigil",
                table: "users");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_changed_at",
                schema: "sigil",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_changed_at",
                schema: "sigil",
                table: "users");

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                schema: "sigil",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
