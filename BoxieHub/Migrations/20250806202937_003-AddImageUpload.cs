using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoxieHub.Migrations
{
    /// <inheritdoc />
    public partial class _003AddImageUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfilePictureId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProfilePictureId",
                table: "AspNetUsers",
                column: "ProfilePictureId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Images_ProfilePictureId",
                table: "AspNetUsers",
                column: "ProfilePictureId",
                principalTable: "Images",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Images_ProfilePictureId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProfilePictureId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilePictureId",
                table: "AspNetUsers");
        }
    }
}
