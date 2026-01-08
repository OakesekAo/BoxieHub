using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxieHub.Migrations
{
    /// <inheritdoc />
    public partial class AddFileUploadStorageColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "Data",
                table: "FileUploads",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "FileUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "FileUploads",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserStorageAccountId",
                table: "FileUploads",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserStorageAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccountIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EncryptedAccessToken = table.Column<string>(type: "text", nullable: true),
                    EncryptedRefreshToken = table.Column<string>(type: "text", nullable: true),
                    TokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    QuotaTotalBytes = table.Column<long>(type: "bigint", nullable: true),
                    QuotaUsedBytes = table.Column<long>(type: "bigint", nullable: true),
                    QuotaLastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStorageAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserStorageAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_Provider",
                table: "FileUploads",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_UserStorageAccountId",
                table: "FileUploads",
                column: "UserStorageAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserStorageAccounts_IsActive",
                table: "UserStorageAccounts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserStorageAccounts_UserId",
                table: "UserStorageAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserStorageAccounts_UserId_Provider",
                table: "UserStorageAccounts",
                columns: new[] { "UserId", "Provider" });

            migrationBuilder.AddForeignKey(
                name: "FK_FileUploads_UserStorageAccounts_UserStorageAccountId",
                table: "FileUploads",
                column: "UserStorageAccountId",
                principalTable: "UserStorageAccounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileUploads_UserStorageAccounts_UserStorageAccountId",
                table: "FileUploads");

            migrationBuilder.DropTable(
                name: "UserStorageAccounts");

            migrationBuilder.DropIndex(
                name: "IX_FileUploads_Provider",
                table: "FileUploads");

            migrationBuilder.DropIndex(
                name: "IX_FileUploads_UserStorageAccountId",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "UserStorageAccountId",
                table: "FileUploads");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Data",
                table: "FileUploads",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);
        }
    }
}
