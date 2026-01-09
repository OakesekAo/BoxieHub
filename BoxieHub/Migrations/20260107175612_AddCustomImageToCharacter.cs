using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoxieHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomImageToCharacter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomImageId",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_CustomImageId",
                table: "Characters",
                column: "CustomImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_FileUploads_CustomImageId",
                table: "Characters",
                column: "CustomImageId",
                principalTable: "FileUploads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_FileUploads_CustomImageId",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_CustomImageId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "CustomImageId",
                table: "Characters");
        }
    }
}
