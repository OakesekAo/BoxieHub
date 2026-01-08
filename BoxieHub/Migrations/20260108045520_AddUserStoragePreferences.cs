using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxieHub.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStoragePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserStoragePreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DefaultProvider = table.Column<int>(type: "integer", nullable: false),
                    DefaultStorageAccountId = table.Column<int>(type: "integer", nullable: true),
                    LastUsedProvider = table.Column<int>(type: "integer", nullable: true),
                    LastUsedStorageAccountId = table.Column<int>(type: "integer", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStoragePreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserStoragePreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserStoragePreferences_UserStorageAccounts_DefaultStorageAc~",
                        column: x => x.DefaultStorageAccountId,
                        principalTable: "UserStorageAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserStoragePreferences_DefaultStorageAccountId",
                table: "UserStoragePreferences",
                column: "DefaultStorageAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserStoragePreferences_UserId",
                table: "UserStoragePreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserStoragePreferences");
        }
    }
}
