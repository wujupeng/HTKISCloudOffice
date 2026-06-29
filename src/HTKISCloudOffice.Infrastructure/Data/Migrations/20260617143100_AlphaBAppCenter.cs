using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTKISCloudOffice.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlphaBAppCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "icon_id",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "app_favorites",
                columns: table => new
                {
                    favorite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_favorites", x => x.favorite_id);
                    table.ForeignKey(
                        name: "FK_app_favorites_applications_app_id",
                        column: x => x.app_id,
                        principalTable: "applications",
                        principalColumn: "app_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_app_favorites_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_icons",
                columns: table => new
                {
                    icon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    icon_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_icons", x => x.icon_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_favorites_app_id",
                table: "app_favorites",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "IX_app_favorites_user_id",
                table: "app_favorites",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_app_favorites_user_id_app_id",
                table: "app_favorites",
                columns: new[] { "user_id", "app_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_icons_icon_name",
                table: "app_icons",
                column: "icon_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_favorites");

            migrationBuilder.DropTable(
                name: "app_icons");

            migrationBuilder.DropColumn(
                name: "description",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "icon_id",
                table: "applications");
        }
    }
}
