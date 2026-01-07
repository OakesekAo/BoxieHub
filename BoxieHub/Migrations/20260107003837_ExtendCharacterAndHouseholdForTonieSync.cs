using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoxieHub.Migrations
{
    /// <inheritdoc />
    public partial class ExtendCharacterAndHouseholdForTonieSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Characters_HouseholdId",
                table: "Characters");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                table: "Households",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "Households",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Characters",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalCharacterId",
                table: "Characters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChaptersJson",
                table: "Characters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChaptersPresent",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChaptersRemaining",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Characters",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Live",
                table: "Characters",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Private",
                table: "Characters",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "SecondsPresent",
                table: "Characters",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "SecondsRemaining",
                table: "Characters",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Transcoding",
                table: "Characters",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Households_ExternalId",
                table: "Households",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Households_LastSyncedAt",
                table: "Households",
                column: "LastSyncedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_ExternalCharacterId",
                table: "Characters",
                column: "ExternalCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_HouseholdId_Type",
                table: "Characters",
                columns: new[] { "HouseholdId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_LastSyncedAt",
                table: "Characters",
                column: "LastSyncedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Households_ExternalId",
                table: "Households");

            migrationBuilder.DropIndex(
                name: "IX_Households_LastSyncedAt",
                table: "Households");

            migrationBuilder.DropIndex(
                name: "IX_Characters_ExternalCharacterId",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_HouseholdId_Type",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_LastSyncedAt",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "ChaptersJson",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ChaptersPresent",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ChaptersRemaining",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Live",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Private",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SecondsPresent",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SecondsRemaining",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Transcoding",
                table: "Characters");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                table: "Households",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Characters",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalCharacterId",
                table: "Characters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_HouseholdId",
                table: "Characters",
                column: "HouseholdId");
        }
    }
}
