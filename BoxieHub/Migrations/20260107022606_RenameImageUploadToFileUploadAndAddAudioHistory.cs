using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxieHub.Migrations
{
    /// <inheritdoc />
    public partial class RenameImageUploadToFileUploadAndAddAudioHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentItems_Images_UploadId",
                table: "ContentItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Images",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Images");

            migrationBuilder.RenameTable(
                name: "Images",
                newName: "FileUploads");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "FileUploads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Created",
                table: "FileUploads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "FileUploads",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileCategory",
                table: "FileUploads",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "FileUploads",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "FileUploads",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_FileUploads",
                table: "FileUploads",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AudioUploadHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FileUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TonieId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChapterTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ChapterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioUploadHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioUploadHistories_FileUploads_FileUploadId",
                        column: x => x.FileUploadId,
                        principalTable: "FileUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_Created",
                table: "FileUploads",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_FileCategory",
                table: "FileUploads",
                column: "FileCategory");

            migrationBuilder.CreateIndex(
                name: "IX_AudioUploadHistories_Created",
                table: "AudioUploadHistories",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_AudioUploadHistories_FileUploadId",
                table: "AudioUploadHistories",
                column: "FileUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioUploadHistories_Status",
                table: "AudioUploadHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AudioUploadHistories_TonieId_HouseholdId",
                table: "AudioUploadHistories",
                columns: new[] { "TonieId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioUploadHistories_UserId",
                table: "AudioUploadHistories",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentItems_FileUploads_UploadId",
                table: "ContentItems",
                column: "UploadId",
                principalTable: "FileUploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentItems_FileUploads_UploadId",
                table: "ContentItems");

            migrationBuilder.DropTable(
                name: "AudioUploadHistories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FileUploads",
                table: "FileUploads");

            migrationBuilder.DropIndex(
                name: "IX_FileUploads_Created",
                table: "FileUploads");

            migrationBuilder.DropIndex(
                name: "IX_FileUploads_FileCategory",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "FileCategory",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "FileUploads");

            migrationBuilder.RenameTable(
                name: "FileUploads",
                newName: "Images");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Images",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Images",
                table: "Images",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentItems_Images_UploadId",
                table: "ContentItems",
                column: "UploadId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
