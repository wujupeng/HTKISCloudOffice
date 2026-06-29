using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTKISCloudOffice.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlphaBDeviceAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connection_configs",
                columns: table => new
                {
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    protocol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    hostname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    password_encrypted = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    connection_params = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    is_remote_app = table.Column<bool>(type: "boolean", nullable: false),
                    remote_app_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_configs", x => x.connection_id);
                });

            migrationBuilder.CreateTable(
                name: "device_bindings",
                columns: table => new
                {
                    binding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_token = table.Column<string>(type: "text", nullable: false),
                    device_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_bindings", x => x.binding_id);
                    table.ForeignKey(
                        name: "FK_device_bindings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "connection_allowed_roles",
                columns: table => new
                {
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_allowed_roles", x => new { x.connection_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_connection_allowed_roles_connection_configs_connection_id",
                        column: x => x.connection_id,
                        principalTable: "connection_configs",
                        principalColumn: "connection_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_connection_allowed_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_connection_allowed_roles_role_id",
                table: "connection_allowed_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "idx_conn_configs_is_active",
                table: "connection_configs",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_conn_configs_protocol",
                table: "connection_configs",
                column: "protocol");

            migrationBuilder.CreateIndex(
                name: "idx_conn_configs_sort",
                table: "connection_configs",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "idx_device_bindings_device_id",
                table: "device_bindings",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "idx_device_bindings_is_active",
                table: "device_bindings",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_device_bindings_token_expires",
                table: "device_bindings",
                column: "device_token_expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_device_bindings_user",
                table: "device_bindings",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connection_allowed_roles");

            migrationBuilder.DropTable(
                name: "device_bindings");

            migrationBuilder.DropTable(
                name: "connection_configs");
        }
    }
}
